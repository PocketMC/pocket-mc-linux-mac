using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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
        private readonly ConcurrentDictionary<string, RunningInstanceInfo> _runningInstances = new();

        public event Action<string, string>? StateChanged;

        public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(15);

        public ProcessRunner(
            IJavaService javaService,
            IPHPService phpService,
            IConsoleLogService logService)
        {
            _javaService = javaService;
            _phpService = phpService;
            _logService = logService;
        }

        private class RunningInstanceInfo
        {
            public required Process Process { get; set; }
            public required string State { get; set; }
            public required int Pgid { get; set; }
            public int AutoRestartCount { get; set; }
            public DateTime LastStartAttempt { get; set; }
        }

        public async Task StartAsync(ServerInstance instance)
        {
            if (_runningInstances.ContainsKey(instance.Slug))
            {
                return; // Already running
            }

            TransitionState(instance.Slug, "Starting");

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
            else
            {
                execPath = await _javaService.GetJavaExecutablePathAsync(instance.EngineVersion);
                arguments = $"{instance.JvmArgs} -jar server.jar nogui";
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

            try
            {
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
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                _runningInstances.TryRemove(instance.Slug, out _);
                _logService.WriteLog(instance.Slug, $"[PocketMC Engine] Failed to start server process: {ex.Message}");
                TransitionState(instance.Slug, "Crashed");
            }
        }

        private void HandleProcessExit(ServerInstance instance, RunningInstanceInfo info)
        {
            _runningInstances.TryRemove(instance.Slug, out _);

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
            }
            else
            {
                _logService.WriteLog(instance.Slug, $"[PocketMC Engine] Server crashed with non-zero exit code: {exitCode}");
                TransitionState(instance.Slug, "Crashed");

                // Auto restart check (rate-limited up to 3 times in 5 minutes)
                if (info.AutoRestartCount < 3 && (DateTime.UtcNow - info.LastStartAttempt).TotalMinutes < 5)
                {
                    info.AutoRestartCount++;
                    info.LastStartAttempt = DateTime.UtcNow;
                    _logService.WriteLog(instance.Slug, $"[PocketMC Engine] Auto-restart attempt {info.AutoRestartCount} of 3...");
                    Task.Run(() => StartAsync(instance));
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
                _logService.WriteLog(instance.Slug, "[PocketMC Engine] Graceful stop timed out. Sending SIGKILL to process group...");
                try
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        UnixNative.kill(-info.Pgid, UnixNative.SIGKILL);
                    }
                    else
                    {
                        info.Process.Kill(true);
                    }
                }
                catch (Exception ex)
                {
                    _logService.WriteLog(instance.Slug, $"[PocketMC Engine] Error sending SIGKILL: {ex.Message}");
                }
            }

            TransitionState(instance.Slug, "Stopped");
            _runningInstances.TryRemove(instance.Slug, out _);
        }

        public Task SendCommandAsync(ServerInstance instance, string command)
        {
            if (_runningInstances.TryGetValue(instance.Slug, out var info))
            {
                try
                {
                    info.Process.StandardInput.WriteLine(command);
                    info.Process.StandardInput.Flush();
                }
                catch (Exception ex)
                {
                    _logService.WriteLog(instance.Slug, $"[PocketMC Engine] Failed to write command to stdin: {ex.Message}");
                }
            }
            return Task.CompletedTask;
        }

        private void TransitionState(string slug, string state)
        {
            StateChanged?.Invoke(slug, state);
        }
    }
}
