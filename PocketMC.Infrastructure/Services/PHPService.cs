using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Configuration;
using PocketMC.Infrastructure.Utils;

namespace PocketMC.Infrastructure.Services
{
    public sealed record PhpDownloadSpec(
        string Version,
        string Tag,
        string AssetPattern,
        string AssetFileName,
        string FallbackDownloadUrl);

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

        public static PhpDownloadSpec ResolveDownloadSpec(string version, bool isMacOs, Architecture arch)
        {
            string os = isMacOs ? "MacOS" : "Linux";
            string archName = isMacOs && arch == Architecture.Arm64
                ? "arm64"
                : (!isMacOs && arch == Architecture.Arm64 ? "aarch64" : "x86_64");

            bool isLegacyPm4 = string.Equals(version, "8.0", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(version, "8.1", StringComparison.OrdinalIgnoreCase);

            string majorTagPrefix = isLegacyPm4 ? "pm4-php" : "pm5-php";
            string pmSuffix = isLegacyPm4 ? "PM4" : "PM5";
            string tag = $"{majorTagPrefix}-{version}-latest";
            string assetPattern = $"{os}-{archName}-{pmSuffix}";
            string fileName = isLegacyPm4
                ? $"PHP-{os}-{archName}-{pmSuffix}.tar.gz"
                : $"PHP-{version}-{os}-{archName}-{pmSuffix}.tar.gz";

            string releasesBase = AppConfig.ProviderPhpReleases;
            string directBase = releasesBase.Replace("api.github.com/repos", "github.com", StringComparison.OrdinalIgnoreCase);
            string fallbackUrl = $"{directBase}/download/{tag}/{fileName}";

            return new PhpDownloadSpec(version, tag, assetPattern, fileName, fallbackUrl);
        }

        public string? GetPHPRuntimeDirectory(string version)
        {
            var runtimes = _settingsService.Settings.DownloadedRuntimes;
            if (runtimes.TryGetValue("php", out var phpVersions) &&
                phpVersions.TryGetValue(version, out var configuredPath) &&
                !string.IsNullOrWhiteSpace(configuredPath))
            {
                return Directory.Exists(configuredPath) ? configuredPath : null;
            }

            string managedDir = Path.Combine(_settingsService.GetSettingsDirectory(), "Runtimes", $"php-{version}");
            return Directory.Exists(managedDir) ? managedDir : null;
        }

        public async Task<bool> IsPHPRuntimeInstalledAsync(string version)
        {
            string? dir = GetPHPRuntimeDirectory(version);
            if (string.IsNullOrWhiteSpace(dir))
            {
                return false;
            }

            string execPath = GetExecutableFromRoot(dir);
            if (!await ValidatePHPRuntimeAsync(execPath, version))
            {
                return false;
            }

            var runtimes = _settingsService.Settings.DownloadedRuntimes;
            if (!runtimes.ContainsKey("php"))
            {
                runtimes["php"] = new Dictionary<string, string>();
            }

            if (!runtimes["php"].ContainsKey(version))
            {
                runtimes["php"][version] = dir;
                _settingsService.Save();
            }

            return true;
        }

        public Task DeletePHPRuntimeAsync(string version)
        {
            var runtimes = _settingsService.Settings.DownloadedRuntimes;
            if (runtimes.TryGetValue("php", out var phpVersions))
            {
                if (phpVersions.TryGetValue(version, out var configuredDir) &&
                    !string.IsNullOrWhiteSpace(configuredDir) &&
                    Directory.Exists(configuredDir))
                {
                    try
                    {
                        Directory.Delete(configuredDir, true);
                    }
                    catch
                    {
                    }
                }

                phpVersions.Remove(version);
                _settingsService.Save();
            }

            string managedDir = Path.Combine(_settingsService.GetSettingsDirectory(), "Runtimes", $"php-{version}");
            if (Directory.Exists(managedDir))
            {
                try
                {
                    Directory.Delete(managedDir, true);
                }
                catch
                {
                }
            }

            return Task.CompletedTask;
        }

        public async Task ProvisionPHPRuntimeAsync(string version, IProgress<double>? progress = null)
        {
            var runtimes = _settingsService.Settings.DownloadedRuntimes;
            if (runtimes.TryGetValue("php", out var phpVersions) && phpVersions.TryGetValue(version, out var existingPath))
            {
                var execPath = GetExecutableFromRoot(existingPath);
                if (await ValidatePHPRuntimeAsync(execPath, version))
                {
                    progress?.Report(1.0);
                    return;
                }
            }

            var spec = ResolveDownloadSpec(
                version,
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX),
                RuntimeInformation.OSArchitecture);

            string downloadUrl = spec.FallbackDownloadUrl;
            string assetName = spec.AssetFileName;

            try
            {
                var releaseJson = await _httpClient.GetFromJsonAsync<JsonObject>($"{AppConfig.ProviderPhpReleases}/tags/{spec.Tag}");
                var assets = releaseJson?["assets"] as JsonArray;
                if (assets != null)
                {
                    var match = assets.FirstOrDefault(a =>
                        a is JsonObject obj &&
                        obj["name"]?.ToString().Contains(spec.AssetPattern, StringComparison.OrdinalIgnoreCase) == true &&
                        obj["name"]?.ToString().EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) == true &&
                        obj["name"]?.ToString().Contains("symbol", StringComparison.OrdinalIgnoreCase) != true &&
                        obj["name"]?.ToString().Contains("debug", StringComparison.OrdinalIgnoreCase) != true) as JsonObject;

                    string? resolvedUrl = match?["browser_download_url"]?.ToString();
                    string? resolvedName = match?["name"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(resolvedUrl))
                    {
                        downloadUrl = resolvedUrl;
                    }
                    if (!string.IsNullOrWhiteSpace(resolvedName))
                    {
                        assetName = resolvedName;
                    }
                }
            }
            catch
            {
            }

            var downloadsDir = _settingsService.GetDownloadsDirectory();
            Directory.CreateDirectory(downloadsDir);
            var archivePath = Path.Combine(downloadsDir, assetName);

            // Download file
            using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                using (var downloadStream = await response.Content.ReadAsStreamAsync())
                using (var fs = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    var totalReadBytes = 0L;
                    var bytesRead = 0;

                    while ((bytesRead = await downloadStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fs.WriteAsync(buffer, 0, bytesRead);
                        totalReadBytes += bytesRead;

                        if (totalBytes > 0 && progress != null)
                        {
                            progress.Report((double)totalReadBytes / totalBytes);
                        }
                    }
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
