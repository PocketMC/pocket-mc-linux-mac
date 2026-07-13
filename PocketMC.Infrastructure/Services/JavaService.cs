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
    public class JavaService : IJavaService
    {
        private readonly ISettingsService _settingsService;
        private readonly HttpClient _httpClient;

        public JavaService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            _httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true });
        }

        public async Task<string> GetJavaExecutablePathAsync(string version)
        {
            var runtimes = _settingsService.Settings.DownloadedRuntimes;
            if (runtimes.TryGetValue("java", out var javaVersions) && javaVersions.TryGetValue(version, out var path))
            {
                var execPath = GetExecutableFromRoot(path);
                if (await ValidateJavaRuntimeAsync(execPath, version))
                {
                    return execPath;
                }
            }

            // On-demand provisioning (D-02)
            await ProvisionJavaRuntimeAsync(version);

            if (runtimes.TryGetValue("java", out javaVersions) && javaVersions.TryGetValue(version, out path))
            {
                return GetExecutableFromRoot(path);
            }

            throw new FileNotFoundException($"Could not provision Java version {version}.");
        }

        private string GetExecutableFromRoot(string rootPath)
        {
            // On macOS, the archive contains Contents/Home/bin/java
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var homePath = Path.Combine(rootPath, "Contents", "Home", "bin", "java");
                if (File.Exists(homePath)) return homePath;

                // Adoptium macOS tar.gz sometimes nests a subfolder named jdk-XX.XX.XX-link
                var subdirs = Directory.GetDirectories(rootPath);
                foreach (var subdir in subdirs)
                {
                    var nested = Path.Combine(subdir, "Contents", "Home", "bin", "java");
                    if (File.Exists(nested)) return nested;
                }
            }

            var defaultPath = Path.Combine(rootPath, "bin", "java");
            if (File.Exists(defaultPath)) return defaultPath;

            // Search nested directories
            var rootSubdirs = Directory.GetDirectories(rootPath);
            foreach (var subdir in rootSubdirs)
            {
                var nested = Path.Combine(subdir, "bin", "java");
                if (File.Exists(nested)) return nested;
            }

            return defaultPath;
        }

        public Task<bool> ValidateJavaRuntimeAsync(string executablePath, string expectedVersion)
        {
            if (!File.Exists(executablePath)) return Task.FromResult(false);

            try
            {
                using (var process = new Process())
                {
                    process.StartInfo.FileName = executablePath;
                    process.StartInfo.Arguments = "-version";
                    process.StartInfo.RedirectStandardError = true; // Java outputs version to stderr
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();

                    string err = process.StandardError.ReadToEnd();
                    string outStr = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    string fullOutput = err + outStr;
                    // Check if it runs and prints java version
                    return Task.FromResult(fullOutput.Contains("version") || process.ExitCode == 0);
                }
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        public async Task ProvisionJavaRuntimeAsync(string version)
        {
            string os = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "mac" : "linux";
            string arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "aarch64" : "x64";

            string downloadUrl = $"https://api.adoptium.net/v3/binary/latest/{version}/ga/{os}/{arch}/jdk/hotspot/normal/eclipse";
            var downloadsDir = _settingsService.GetDownloadsDirectory();
            var archivePath = Path.Combine(downloadsDir, $"openjdk-{version}-{os}-{arch}.tar.gz");

            // Download file
            using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                // D-04: Secure hash validation
                // We'll read the final URL to fetch checksum if possible
                var finalUrl = response.RequestMessage?.RequestUri?.ToString();
                string? expectedHash = null;
                if (finalUrl != null)
                {
                    try
                    {
                        var hashResponse = await _httpClient.GetAsync(finalUrl + ".sha256");
                        if (hashResponse.IsSuccessStatusCode)
                        {
                            var content = await hashResponse.Content.ReadAsStringAsync();
                            expectedHash = content.Split(' ')[0].Trim();
                        }
                    }
                    catch
                    {
                        // Fallback if checksum download fails
                    }
                }

                using (var fs = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fs);
                }

                // Verify hash
                if (!string.IsNullOrEmpty(expectedHash))
                {
                    using (var sha = SHA256.Create())
                    using (var fs = File.OpenRead(archivePath))
                    {
                        var hashBytes = sha.ComputeHash(fs);
                        var actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                        if (actualHash != expectedHash.ToLower())
                        {
                            File.Delete(archivePath);
                            throw new InvalidDataException("SHA256 checksum verification failed for JDK archive.");
                        }
                    }
                }
            }

            // Extract
            var runtimesDir = Path.Combine(_settingsService.GetSettingsDirectory(), "Runtimes", $"java-{version}");
            Directory.CreateDirectory(runtimesDir);
            SafeZipExtractor.ExtractTarGz(archivePath, runtimesDir);

            // Clean up archive
            try { File.Delete(archivePath); } catch {}

            // Save to settings
            if (!_settingsService.Settings.DownloadedRuntimes.ContainsKey("java"))
            {
                _settingsService.Settings.DownloadedRuntimes["java"] = new System.Collections.Generic.Dictionary<string, string>();
            }
            _settingsService.Settings.DownloadedRuntimes["java"][version] = runtimesDir;
            _settingsService.Save();
        }
    }
}
