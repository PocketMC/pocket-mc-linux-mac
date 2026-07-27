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
            if (string.IsNullOrWhiteSpace(rootPath)) return "java";
            if (File.Exists(rootPath)) return rootPath;
            if (!Directory.Exists(rootPath)) return Path.Combine(rootPath, "bin", "java");

            try
            {
                // On macOS, the archive contains Contents/Home/bin/java
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    var homePath = Path.Combine(rootPath, "Contents", "Home", "bin", "java");
                    if (File.Exists(homePath)) return homePath;

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
            }
            catch { }

            return Path.Combine(rootPath, "bin", "java");
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
                    bool hasJavaIndicator = fullOutput.Contains("openjdk", StringComparison.OrdinalIgnoreCase) || 
                                           fullOutput.Contains("java", StringComparison.OrdinalIgnoreCase) ||
                                           fullOutput.Contains("hotspot", StringComparison.OrdinalIgnoreCase);

                    if (!hasJavaIndicator || process.ExitCode != 0)
                    {
                        return Task.FromResult(false);
                    }

                    return Task.FromResult(MatchJavaVersion(fullOutput, expectedVersion));
                }
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        private static bool MatchJavaVersion(string fullOutput, string expectedVersion)
        {
            if (string.IsNullOrWhiteSpace(fullOutput)) return false;

            // Pattern for version "1.8.x" or "11.x" or "17.x" or "21.x" or "25-ea"
            var match = System.Text.RegularExpressions.Regex.Match(fullOutput, @"version\s*""(?:1\.)?(\d+)");
            if (match.Success)
            {
                string detectedMajor = match.Groups[1].Value;
                if (string.Equals(detectedMajor, expectedVersion, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Fallback matching
            if (expectedVersion == "8" && (fullOutput.Contains("1.8.") || fullOutput.Contains("\"8."))) return true;
            if (expectedVersion == "11" && (fullOutput.Contains("\"11.") || fullOutput.Contains(" 11."))) return true;
            if (expectedVersion == "17" && (fullOutput.Contains("\"17.") || fullOutput.Contains(" 17."))) return true;
            if (expectedVersion == "21" && (fullOutput.Contains("\"21.") || fullOutput.Contains(" 21."))) return true;
            if (expectedVersion == "25" && (fullOutput.Contains("\"25.") || fullOutput.Contains(" 25."))) return true;

            return false;
        }

        public async Task ProvisionJavaRuntimeAsync(string version, IProgress<double>? progress = null)
        {
            if (await IsJavaRuntimeInstalledAsync(version))
            {
                progress?.Report(1.0);
                return;
            }

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
                            var candidate = content.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                            if (IsValidSha256(candidate))
                            {
                                expectedHash = candidate;
                            }
                        }
                    }
                    catch
                    {
                        // Fallback if checksum download fails
                    }
                }

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

        public async Task<bool> IsJavaRuntimeInstalledAsync(string version)
        {
            try
            {
                var runtimes = _settingsService.Settings.DownloadedRuntimes;
                if (runtimes.TryGetValue("java", out var javaVersions) && javaVersions.TryGetValue(version, out var path))
                {
                    var execPath = GetExecutableFromRoot(path);
                    if (await ValidateJavaRuntimeAsync(execPath, version))
                    {
                        return true;
                    }
                }

                // Check local PocketMC Runtimes directory on disk
                var runtimesDir = Path.Combine(_settingsService.GetSettingsDirectory(), "Runtimes", $"java-{version}");
                if (Directory.Exists(runtimesDir))
                {
                    var localExec = GetExecutableFromRoot(runtimesDir);
                    if (await ValidateJavaRuntimeAsync(localExec, version))
                    {
                        if (!runtimes.ContainsKey("java")) runtimes["java"] = new System.Collections.Generic.Dictionary<string, string>();
                        runtimes["java"][version] = runtimesDir;
                        _settingsService.Save();
                        return true;
                    }
                }

                // System Java Auto-Discovery (JAVA_HOME, /usr/bin/java, /usr/lib/jvm, /Library/Java/JavaVirtualMachines)
                var candidatePaths = new System.Collections.Generic.List<string>();

                var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
                if (!string.IsNullOrEmpty(javaHome))
                {
                    candidatePaths.Add(Path.Combine(javaHome, "bin", "java"));
                    candidatePaths.Add(javaHome);
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    candidatePaths.Add("/usr/bin/java");
                    candidatePaths.Add("/usr/local/bin/java");
                    if (Directory.Exists("/usr/lib/jvm"))
                    {
                        foreach (var dir in Directory.GetDirectories("/usr/lib/jvm"))
                        {
                            candidatePaths.Add(Path.Combine(dir, "bin", "java"));
                        }
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    candidatePaths.Add("/usr/bin/java");
                    if (Directory.Exists("/Library/Java/JavaVirtualMachines"))
                    {
                        foreach (var dir in Directory.GetDirectories("/Library/Java/JavaVirtualMachines"))
                        {
                            candidatePaths.Add(Path.Combine(dir, "Contents", "Home", "bin", "java"));
                        }
                    }
                }

                foreach (var candidate in candidatePaths)
                {
                    if (File.Exists(candidate) && await ValidateJavaRuntimeAsync(candidate, version))
                    {
                        if (!runtimes.ContainsKey("java")) runtimes["java"] = new System.Collections.Generic.Dictionary<string, string>();
                        runtimes["java"][version] = candidate;
                        _settingsService.Save();
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        public Task DeleteJavaRuntimeAsync(string version)
        {
            var runtimes = _settingsService.Settings.DownloadedRuntimes;
            if (runtimes.TryGetValue("java", out var javaVersions) && javaVersions.TryGetValue(version, out var path))
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, true);
                    }
                }
                catch { }

                javaVersions.Remove(version);
                _settingsService.Save();
            }
            return Task.CompletedTask;
        }

        public async Task RegisterCustomJavaRuntimeAsync(string version, string path)
        {
            var execPath = GetExecutableFromRoot(path);
            if (!File.Exists(execPath))
            {
                throw new FileNotFoundException($"Java executable not found at '{execPath}'. Please point to a valid JDK/JRE directory.");
            }

            if (!_settingsService.Settings.DownloadedRuntimes.ContainsKey("java"))
            {
                _settingsService.Settings.DownloadedRuntimes["java"] = new System.Collections.Generic.Dictionary<string, string>();
            }
            _settingsService.Settings.DownloadedRuntimes["java"][version] = path;
            _settingsService.Save();
            await Task.CompletedTask;
        }

        private bool IsValidSha256(string hash)
        {
            if (string.IsNullOrEmpty(hash) || hash.Length != 64) return false;
            foreach (char c in hash)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
