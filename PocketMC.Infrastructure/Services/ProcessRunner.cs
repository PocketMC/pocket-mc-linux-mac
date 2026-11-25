using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.Infrastructure.Services
{
    public static class UnixNative
    {
        [DllImport("libc", SetLastError = true)]
        public static extern int setpgid(int pid, int pgid);

        [DllImport("libc", SetLastError = true)]
        public static extern int kill(int pid, int sig);

        public const int SIGTERM = 15;
        public const int SIGKILL = 9;
    }

    public class ProcessRunner : IProcessRunner
    {
        private readonly IJavaService _javaService;
        private readonly IPHPService _phpService;
        private readonly IConsoleLogService _logService;
        private readonly InstanceOperationCoordinator _coordinator;
        private readonly ConcurrentDictionary<string, RunningInstanceInfo> _runningInstances = new();
        private readonly ConcurrentDictionary<string, (int Count, DateTime LastAttempt)> _autoRestartStats = new();
        private readonly ConcurrentDictionary<string, string> _states = new();

        public event Action<string, string>? StateChanged;

        public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(15);

        public ProcessRunner(
            IJavaService javaService,
            IPHPService phpService,
            IConsoleLogService logService,
            InstanceOperationCoordinator? coordinator = null)
        {
            _javaService = javaService;
            _phpService = phpService;
            _logService = logService;
            _coordinator = coordinator ?? new InstanceOperationCoordinator();
        }

        private class RunningInstanceInfo
        {
            public required Process Process { get; set; }
            public required string State { get; set; }
            public required int Pgid { get; set; }
            public int AutoRestartCount { get; set; }
            public DateTime LastStartAttempt { get; set; }
        }

        public async Task StartAsync(ServerInstance instance, bool isAutoRestart = false)
        {
            if (_runningInstances.ContainsKey(instance.Slug))
            {
                return; // Already running
            }

            if (!isAutoRestart)
            {
                _autoRestartStats.TryRemove(instance.Slug, out _);
            }

            _logService.ClearLogs(instance.Slug);
            TransitionState(instance.Slug, "Starting");

            try
            {
                KillOrphanedProcessesForInstance(instance);
                InspectSessionLock(instance);

                string execPath = "";
                string arguments = "";

                if (instance.EngineVersion.StartsWith("mock:"))
                {
                    var parts = instance.EngineVersion.Split(':');
                    execPath = parts[1];
                    arguments = parts.Length > 2 ? parts[2] : "";
                }
                else if (instance.EngineType == EngineType.PocketMine)
                {
                    execPath = await _phpService.GetPHPExecutablePathAsync(instance.EngineVersion);
                    arguments = "PocketMine-MP.phar";
                }
                else if (instance.EngineType == EngineType.Bedrock)
                {
                    execPath = Path.Combine(instance.Path, "bedrock_server");
                    arguments = "";
                }
                else if (instance.EngineType == EngineType.Pumpkin)
                {
                    PumpkinConfiguration.PrepareForLaunch(instance.Path);
                    execPath = Path.Combine(instance.Path, PumpkinProvider.GetExecutableFileName());
                    arguments = "";
                }
                else if (instance.EngineType == EngineType.Forge || instance.EngineType == EngineType.NeoForge)
                {
                    string javaVersion = MapMinecraftVersionToJavaVersion(instance.EngineVersion);
                    execPath = await _javaService.GetJavaExecutablePathAsync(javaVersion);

                    // Check for modern Forge/NeoForge unix launch args file
                    var unixArgsFile = Directory.GetFiles(instance.Path, "unix_args.txt", SearchOption.AllDirectories).FirstOrDefault();
                    if (unixArgsFile != null)
                    {
                        string relativePath = Path.GetRelativePath(instance.Path, unixArgsFile);
                        arguments = $"{instance.JvmArgs} @{relativePath}";
                    }
                    else
                    {
                        // Fallback to legacy forge jar execution if installer didn't generate unix_args.txt
                        var forgeJars = Directory.GetFiles(instance.Path, "*.jar")
                            .Select(Path.GetFileName)
                            .Where(f => f != null && f.Contains("forge", StringComparison.OrdinalIgnoreCase) && !f.Contains("installer", StringComparison.OrdinalIgnoreCase))
                            .OrderByDescending(f => f!.Contains("universal", StringComparison.OrdinalIgnoreCase))
                            .ThenByDescending(f => f!.Contains("server", StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        string chosenJar = forgeJars.FirstOrDefault() ?? "server.jar";
                        arguments = $"{instance.JvmArgs} -jar {chosenJar} nogui";
                    }
                }
                else
                {
                    string javaVersion = MapMinecraftVersionToJavaVersion(instance.EngineVersion);
                    execPath = await _javaService.GetJavaExecutablePathAsync(javaVersion);
                    arguments = $"{instance.JvmArgs} -jar server.jar nogui";
                }

                if (instance.EngineType == EngineType.VanillaJava ||
                    instance.EngineType == EngineType.Fabric ||
                    instance.EngineType == EngineType.Paper)
                {
                    string jarPath = Path.Combine(instance.Path, "server.jar");
                    if (!File.Exists(jarPath))
                    {
                        _logService.WriteLog(instance.Slug, $"[PocketMC Engine] server.jar is missing. Downloading {instance.EngineType} Minecraft {instance.EngineVersion}...");
                        if (instance.EngineType == EngineType.VanillaJava)
                        {
                            await DownloadVanillaServerJarAsync(instance.EngineVersion, jarPath);
                        }
                        else if (instance.EngineType == EngineType.Fabric)
                        {
                            await DownloadFabricServerJarAsync(instance.EngineVersion, jarPath);
                        }
                        else if (instance.EngineType == EngineType.Paper)
                        {
                            await DownloadPaperServerJarAsync(instance.EngineVersion, jarPath);
                        }
                        _logService.WriteLog(instance.Slug, "[PocketMC Engine] server.jar downloaded successfully.");
                    }

                    string eulaPath = Path.Combine(instance.Path, "eula.txt");
                    if (!File.Exists(eulaPath))
                    {
                        await File.WriteAllTextAsync(eulaPath, "eula=true\n");
                    }
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = execPath,
                    Arguments = arguments,
                    WorkingDirectory = instance.Path,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = new Process { StartInfo = startInfo };
                process.EnableRaisingEvents = true;

                process.OutputDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        _logService.WriteLog(instance.Slug, args.Data);
                    }
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        _logService.WriteLog(instance.Slug, args.Data);
                    }
                };

                var info = new RunningInstanceInfo
                {
                    Process = process,
                    State = "Starting",
                    Pgid = 0,
                    LastStartAttempt = DateTime.UtcNow
                };

                _runningInstances[instance.Slug] = info;

                process.Exited += (sender, args) =>
                {
                    if (_runningInstances.TryGetValue(instance.Slug, out var runningInfo))
                    {
                        HandleProcessExit(instance, runningInfo);
                    }
                };

                process.Start();
                info.Pgid = process.Id;
                info.State = "Running";

                try
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        UnixNative.setpgid(process.Id, process.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logService.WriteLog(instance.Slug, $"[PocketMC Engine] Warn: setpgid failed: {ex.Message}");
                }

                TransitionState(instance.Slug, "Running");
                _coordinator.SetRunning(instance.Path, true);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                _runningInstances.TryRemove(instance.Slug, out _);
                _coordinator.SetRunning(instance.Path, false);
                _logService.WriteLog(instance.Slug, $"[PocketMC Engine] Failed to start server process: {ex.Message}");
                TransitionState(instance.Slug, "Crashed");
            }
        }

        private string MapMinecraftVersionToJavaVersion(string mcVersion)
        {
            if (Version.TryParse(mcVersion, out var version))
            {
                if (version >= new Version(1, 22, 0)) return "25";
                if (version >= new Version(1, 20, 5)) return "21";
                if (version >= new Version(1, 17, 0)) return "17";
                if (version >= new Version(1, 12, 0)) return "11";
                return "8";
            }
            if (mcVersion.StartsWith("1.22") || mcVersion.StartsWith("1.23") || mcVersion.StartsWith("1.24")) return "25";
            if (mcVersion.StartsWith("1.21")) return "21";
            if (mcVersion.StartsWith("1.20") || mcVersion.StartsWith("1.19") || mcVersion.StartsWith("1.18") || mcVersion.StartsWith("1.17")) return "17";
            return "8";
        }

        private async Task DownloadVanillaServerJarAsync(string mcVersion, string destinationPath)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "PocketMC-App");

            string manifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest.json";
            var manifestStr = await client.GetStringAsync(manifestUrl);
            using var manifestDoc = JsonDocument.Parse(manifestStr);
            var versions = manifestDoc.RootElement.GetProperty("versions");
            
            string? versionMetaUrl = null;
            foreach (var version in versions.EnumerateArray())
            {
                if (version.GetProperty("id").GetString() == mcVersion)
                {
                    versionMetaUrl = version.GetProperty("url").GetString();
                    break;
                }
            }

            if (string.IsNullOrEmpty(versionMetaUrl))
                throw new Exception($"Version {mcVersion} not found in Mojang manifest.");

            var metaStr = await client.GetStringAsync(versionMetaUrl);
            using var metaDoc = JsonDocument.Parse(metaStr);
            var serverDownloadUrl = metaDoc.RootElement
                .GetProperty("downloads")
                .GetProperty("server")
                .GetProperty("url")
                .GetString();

            if (string.IsNullOrEmpty(serverDownloadUrl))
                throw new Exception($"No server download found for Vanilla {mcVersion}.");

            using var response = await client.GetAsync(serverDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs);
        }

        private async Task DownloadFabricServerJarAsync(string mcVersion, string destinationPath)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "PocketMC-App");

            string loaderVersion = "0.15.11";
            try
            {
                var loadersStr = await client.GetStringAsync("https://meta.fabricmc.net/v2/versions/loader");
                using var loadersDoc = JsonDocument.Parse(loadersStr);
                foreach (var item in loadersDoc.RootElement.EnumerateArray())
                {
                    if (item.GetProperty("stable").GetBoolean())
                    {
                        loaderVersion = item.GetProperty("version").GetString() ?? loaderVersion;
                        break;
                    }
                }
            }
            catch { }

            string installerVersion = "1.0.1";
            try
            {
                var installersStr = await client.GetStringAsync("https://meta.fabricmc.net/v2/versions/installer");
                using var installersDoc = JsonDocument.Parse(installersStr);
                foreach (var item in installersDoc.RootElement.EnumerateArray())
                {
                    if (item.GetProperty("stable").GetBoolean())
                    {
                        installerVersion = item.GetProperty("version").GetString() ?? installerVersion;
                        break;
                    }
                }
            }
            catch { }

            string url = $"https://meta.fabricmc.net/v2/versions/loader/{mcVersion}/{loaderVersion}/{installerVersion}/server/jar";
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs);
        }

        private async Task DownloadPaperServerJarAsync(string mcVersion, string destinationPath)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "PocketMC-App");

            string url = $"https://api.papermc.io/v2/projects/paper/versions/{mcVersion}";
            var responseStr = await client.GetStringAsync(url);
            using var doc = JsonDocument.Parse(responseStr);
            var builds = doc.RootElement.GetProperty("builds");
            
            int maxBuild = 0;
            foreach (var build in builds.EnumerateArray())
            {
                int b = build.GetInt32();
                if (b > maxBuild) maxBuild = b;
            }

            if (maxBuild == 0)
                throw new Exception($"No builds found for Paper version {mcVersion}.");

            string downloadUrl = $"https://api.papermc.io/v2/projects/paper/versions/{mcVersion}/builds/{maxBuild}/downloads/paper-{mcVersion}-{maxBuild}.jar";
            using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs);
        }

        private void HandleProcessExit(ServerInstance instance, RunningInstanceInfo info)
        {
            _runningInstances.TryRemove(instance.Slug, out _);
            _coordinator.SetRunning(instance.Path, false);

            int exitCode = 0;
            try
            {
                exitCode = info.Process.ExitCode;
            }
            catch
            {
                // Process disposed or unavailable
            }

            if (exitCode == 0 || info.State == "Stopping" || info.State == "Stopped")
            {
                TransitionState(instance.Slug, "Stopped");
                _autoRestartStats.TryRemove(instance.Slug, out _);
            }
            else
            {
                _logService.WriteLog(instance.Slug, $"[PocketMC Engine] Server crashed with non-zero exit code: {exitCode}");
                TransitionState(instance.Slug, "Crashed");

                // Auto restart check (only if explicitly enabled on instance)
                if (instance.EnableAutoRestart)
                {
                    var now = DateTime.UtcNow;
                    var stats = _autoRestartStats.GetOrAdd(instance.Slug, _ => (0, now));
                    if ((now - stats.LastAttempt).TotalMinutes >= 5)
                    {
                        stats = (0, now);
                    }

                    if (stats.Count < 3)
                    {
                        stats = (stats.Count + 1, now);
                        _autoRestartStats[instance.Slug] = stats;

                        int delaySec = instance.AutoRestartDelaySeconds;
                        _logService.WriteLog(instance.Slug, $"[PocketMC Engine] Auto-restart attempt {stats.Count} of 3 in {delaySec}s...");
                        Task.Run(async () =>
                        {
                            if (delaySec > 0)
                            {
                                await Task.Delay(TimeSpan.FromSeconds(delaySec));
                            }
                            await StartAsync(instance, isAutoRestart: true);
                        });
                    }
                }
            }
        }

        public async Task StopAsync(ServerInstance instance)
        {
            if (!_runningInstances.TryGetValue(instance.Slug, out var info))
            {
                return;
            }

            info.State = "Stopping";
            TransitionState(instance.Slug, "Stopping");
            _autoRestartStats.TryRemove(instance.Slug, out _);

            string stopCommand = "stop";

            if (instance.EngineType == EngineType.VanillaJava || instance.EngineType == EngineType.Paper ||
                instance.EngineType == EngineType.Fabric || instance.EngineType == EngineType.Forge ||
                instance.EngineType == EngineType.NeoForge)
            {
                await SendCommandAsync(instance, "save-all");
                await Task.Delay(500);
            }

            await SendCommandAsync(instance, stopCommand);

            var shutdownTimeout = ShutdownTimeout;
            var exited = false;

            try
            {
                using (var cts = new System.Threading.CancellationTokenSource(shutdownTimeout))
                {
                    await info.Process.WaitForExitAsync(cts.Token);
                    exited = true;
                }
            }
            catch (OperationCanceledException)
            {
                // Graceful stop timed out, escalate to SIGKILL
            }

            if (!exited)
            {
                _logService.WriteLog(instance.Slug, "[PocketMC Engine] Graceful stop timed out. Terminating process tree...");

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    try
                    {
                        UnixNative.kill(-info.Pgid, UnixNative.SIGTERM);
                    }
                    catch (Exception ex)
                    {
                        _logService.WriteLog(instance.Slug, $"[PocketMC Engine] Error sending SIGTERM: {ex.Message}");
                    }

                    try
                    {
                        using var termCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                        await info.Process.WaitForExitAsync(termCts.Token);
                        exited = true;
                    }
                    catch (OperationCanceledException) { }

                    if (!exited)
                    {
                        try
                        {
                            UnixNative.kill(-info.Pgid, UnixNative.SIGKILL);
                        }
                        catch { }
                    }
                }

                // Process tree kill ensuring no orphan remains
                try
                {
                    info.Process.Kill(entireProcessTree: true);
                    await info.Process.WaitForExitAsync();
                }
                catch (Exception ex)
                {
                    _logService.WriteLog(instance.Slug, $"[PocketMC Engine] Process.Kill completed: {ex.Message}");
                }
            }

            TransitionState(instance.Slug, "Stopped");
            _runningInstances.TryRemove(instance.Slug, out _);
            _coordinator.SetRunning(instance.Path, false);
        }

        private void KillOrphanedProcessesForInstance(ServerInstance instance)
        {
            if (string.IsNullOrWhiteSpace(instance.Path) || !Directory.Exists(instance.Path))
            {
                return;
            }

            try
            {
                int currentPid = Environment.ProcessId;

                // 1. Linux /proc scanner
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    string targetPath = Path.GetFullPath(instance.Path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    if (Directory.Exists("/proc"))
                    {
                        foreach (var dir in Directory.GetDirectories("/proc"))
                        {
                            var dirName = Path.GetFileName(dir);
                            if (!int.TryParse(dirName, out int pid) || pid == currentPid || pid <= 1)
                            {
                                continue;
                            }

                            try
                            {
                                string cwdSymlink = Path.Combine(dir, "cwd");
                                var linkTarget = File.ResolveLinkTarget(cwdSymlink, returnFinalTarget: true)?.FullName;
                                if (!string.IsNullOrEmpty(linkTarget))
                                {
                                    string resolvedTarget = Path.GetFullPath(linkTarget).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                                    if (string.Equals(resolvedTarget, targetPath, StringComparison.OrdinalIgnoreCase) ||
                                        resolvedTarget.StartsWith(targetPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                                    {
                                        _logService.WriteLog(instance.Slug, $"[PocketMC Engine] Cleaning up orphaned process {pid} using instance directory...");
                                        try
                                        {
                                            var proc = Process.GetProcessById(pid);
                                            proc.Kill(entireProcessTree: true);
                                            proc.WaitForExit(1500);
                                        }
                                        catch { }
                                    }
                                }
                            }
                            catch
                            {
                                // Ignore unreadable /proc entries
                            }
                        }
                    }
                }

                // 2. Terminate any processes holding session.lock using fuser
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    try
                    {
                        var lockFiles = Directory.GetFiles(instance.Path, "session.lock", SearchOption.AllDirectories);
                        foreach (var lockFile in lockFiles)
                        {
                            try
                            {
                                using var fuserProc = Process.Start(new ProcessStartInfo
                                {
                                    FileName = "fuser",
                                    Arguments = $"-k \"{lockFile}\"",
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true,
                                    UseShellExecute = false,
                                    CreateNoWindow = true
                                });
                                fuserProc?.WaitForExit(1500);
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                _logService.WriteLog(instance.Slug, $"[PocketMC Engine] Note: Process inspection: {ex.Message}");
            }
        }

        private void InspectSessionLock(ServerInstance instance)
        {
            try
            {
                if (Directory.Exists(instance.Path))
                {
                    var lockFile = Directory.GetFiles(instance.Path, "session.lock", SearchOption.AllDirectories).FirstOrDefault();
                    if (lockFile != null)
                    {
                        _logService.WriteLog(instance.Slug, $"[PocketMC Engine] Warning: {Path.GetRelativePath(instance.Path, lockFile)} already exists. PocketMC will not delete it automatically to prevent world corruption. If startup fails, ensure no other process is using this world.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.WriteLog(instance.Slug, $"[PocketMC Engine] Warn: Failed to inspect session.lock: {ex.Message}");
            }
        }

        public System.Collections.Generic.IReadOnlyList<string> GetRunningSlugs()
        {
            return _runningInstances.Keys.ToList();
        }

        public async Task StopAllAsync()
        {
            var runningKeys = _runningInstances.Keys.ToList();
            if (runningKeys.Count == 0) return;

            var tasks = new List<Task>();
            foreach (var slug in runningKeys)
            {
                var dummyInstance = new ServerInstance { Slug = slug };
                tasks.Add(StopAsync(dummyInstance));
            }

            await Task.WhenAll(tasks);
        }

        public Task SendCommandAsync(ServerInstance instance, string command)
        {
            if (_runningInstances.TryGetValue(instance.Slug, out var info))
            {
                try
                {
                    if (info.Process.HasExited)
                    {
                        _runningInstances.TryRemove(instance.Slug, out _);
                        TransitionState(instance.Slug, info.Process.ExitCode == 0 ? "Stopped" : "Crashed");
                        _logService.WriteLog(instance.Slug, "[System] Cannot send command: Server process has terminated.");
                        return Task.CompletedTask;
                    }

                    info.Process.StandardInput.WriteLine(command);
                    info.Process.StandardInput.Flush();
                }
                catch (Exception ex)
                {
                    _logService.WriteLog(instance.Slug, $"[PocketMC Engine] Failed to write command to stdin: {ex.Message}");
                }
            }
            else
            {
                _logService.WriteLog(instance.Slug, "[System] Cannot send command: Server instance is not running.");
            }
            return Task.CompletedTask;
        }

        public bool TryGetRunningInfo(string slug, out int pgid, out string state)
        {
            if (_runningInstances.TryGetValue(slug, out var info))
            {
                try
                {
                    if (info.Process.HasExited)
                    {
                        _runningInstances.TryRemove(slug, out _);
                        TransitionState(slug, info.Process.ExitCode == 0 ? "Stopped" : "Crashed");
                        pgid = 0;
                        state = _states.TryGetValue(slug, out var s) ? s : "Stopped";
                        return false;
                    }
                }
                catch { }

                pgid = info.Pgid;
                state = info.State;
                return true;
            }

            _states.TryGetValue(slug, out var lastState);
            state = lastState ?? "Stopped";
            pgid = 0;
            return false;
        }

        public DateTime? GetSessionStartTime(string slug)
        {
            if (_runningInstances.TryGetValue(slug, out var info))
            {
                return info.LastStartAttempt;
            }
            return null;
        }

        private void TransitionState(string slug, string state)
        {
            _states[slug] = state;
            StateChanged?.Invoke(slug, state);
        }
    }
}
