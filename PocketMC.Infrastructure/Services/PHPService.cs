using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Utils;

namespace PocketMC.Infrastructure.Services
{
    public class PHPService : IPHPService
    {
        private readonly ISettingsService _settingsService;
        private readonly HttpClient _httpClient;

        public PHPService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            _httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true });
        }

        public async Task<string> GetPHPExecutablePathAsync(string version)
        {
            var runtimes = _settingsService.Settings.DownloadedRuntimes;
            if (runtimes.TryGetValue("php", out var phpVersions) && phpVersions.TryGetValue(version, out var path))
            {
                var execPath = GetExecutableFromRoot(path);
                if (await ValidatePHPRuntimeAsync(execPath, version))
                {
                    return execPath;
                }
            }

            // On-demand provisioning (D-02)
            await ProvisionPHPRuntimeAsync(version);

            if (runtimes.TryGetValue("php", out phpVersions) && phpVersions.TryGetValue(version, out path))
            {
                return GetExecutableFromRoot(path);
            }

            throw new FileNotFoundException($"Could not provision PHP version {version}.");
        }

        private string GetExecutableFromRoot(string rootPath)
        {
            // First check common path bin/php
            var path1 = Path.Combine(rootPath, "bin", "php");
            if (File.Exists(path1)) return path1;

            // Search recursively for "php" binary
            try
            {
                var files = Directory.GetFiles(rootPath, "php", SearchOption.AllDirectories);
                if (files.Length > 0) return files[0];
            }
            catch
            {
                // Fall through
            }

            return path1;
        }

        public Task<bool> ValidatePHPRuntimeAsync(string executablePath, string expectedVersion)
        {
            if (!File.Exists(executablePath)) return Task.FromResult(false);

            try
            {
                using (var process = new Process())
                {
                    process.StartInfo.FileName = executablePath;
                    process.StartInfo.Arguments = "-v";
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();

                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    // Check if it runs and mentions PHP and the expected major/minor version
                    return Task.FromResult(output.Contains("PHP") && output.Contains(expectedVersion));
                }
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        public async Task ProvisionPHPRuntimeAsync(string version)
        {
            string os;
            string arch;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                os = "MacOS";
                arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x86_64";
            }
            else
            {
                os = "Linux";
                arch = "x86_64"; // Default Linux desktop/server architectures
            }

            string assetName = $"PHP-{version}-{os}-{arch}-PM5.tar.gz";
            string downloadUrl = $"https://github.com/pmmp/PHP-Binaries/releases/download/pm5-php-{version}-latest/{assetName}";

            var downloadsDir = _settingsService.GetDownloadsDirectory();
            var archivePath = Path.Combine(downloadsDir, assetName);

            // Download file
            using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                using (var fs = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fs);
                }
            }

            // Extract
            var runtimesDir = Path.Combine(_settingsService.GetSettingsDirectory(), "Runtimes", $"php-{version}");
            Directory.CreateDirectory(runtimesDir);
            SafeZipExtractor.ExtractTarGz(archivePath, runtimesDir);

            // Clean up archive
            try { File.Delete(archivePath); } catch {}

            // Save to settings
            if (!_settingsService.Settings.DownloadedRuntimes.ContainsKey("php"))
            {
                _settingsService.Settings.DownloadedRuntimes["php"] = new System.Collections.Generic.Dictionary<string, string>();
            }
            _settingsService.Settings.DownloadedRuntimes["php"][version] = runtimesDir;
            _settingsService.Save();
        }
    }
}
