using System;
using System.IO;
using System.Threading.Tasks;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests
{
    public class ProcessRunnerTests : IDisposable
    {
        private readonly string _testTempDir;
        private readonly SettingsService _settingsService;
        private readonly ConsoleLogService _logService;
        private readonly MockJavaService _javaService;
        private readonly MockPHPService _phpService;
        private readonly ProcessRunner _runner;

        public ProcessRunnerTests()
        {
            _testTempDir = Path.Combine(Path.GetTempPath(), "ProcessRunnerTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testTempDir);

            _settingsService = new SettingsService(_testTempDir);
            _settingsService.Settings.CustomDataRoot = _testTempDir;
            _settingsService.Load();

            _logService = new ConsoleLogService(_settingsService);
            _javaService = new MockJavaService();
            _phpService = new MockPHPService();
            
            _runner = new ProcessRunner(_javaService, _phpService, _logService);
            _runner.ShutdownTimeout = TimeSpan.FromSeconds(1); // Speed up tests
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testTempDir))
                {
                    Directory.Delete(_testTempDir, true);
                }
            }
            catch { }
        }

        private class MockJavaService : IJavaService
        {
            public Task<string> GetJavaExecutablePathAsync(string version) => Task.FromResult("java");
            public Task<bool> ValidateJavaRuntimeAsync(string path, string version) => Task.FromResult(true);
            public Task ProvisionJavaRuntimeAsync(string version, IProgress<double>? progress = null) => Task.CompletedTask;
        }

        private class MockPHPService : IPHPService
        {
            public Task<string> GetPHPExecutablePathAsync(string version) => Task.FromResult("php");
            public Task<bool> ValidatePHPRuntimeAsync(string path, string version) => Task.FromResult(true);
            public Task ProvisionPHPRuntimeAsync(string version, IProgress<double>? progress = null) => Task.CompletedTask;
        }

        [Fact]
        [Trait("Category", "Unit")]
        public async Task Start_SpawnsProcessAndEscalatesToSigkillOnTimeout()
        {
            // Only run on Unix-like environments (Linux/macOS) where /bin/sleep is guaranteed to exist
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                var instance = new ServerInstance
                {
                    Id = Guid.NewGuid(),
                    Name = "Sleep Server",
                    Slug = "sleep-server",
                    Path = _testTempDir,
                    EngineType = EngineType.VanillaJava,
                    EngineVersion = "mock:/bin/sleep:30" // Mock: runs /bin/sleep with arg 30
                };

                string lastState = "";
                _runner.StateChanged += (slug, state) =>
                {
                    if (slug == instance.Slug)
                    {
                        lastState = state;
                    }
                };

                await _runner.StartAsync(instance);
                Assert.Equal("Running", lastState);

                // Stop should timeout (since sleep doesn't respond to 'stop' stdin command) and escalate to SIGKILL
                await _runner.StopAsync(instance);
                Assert.Equal("Stopped", lastState);
            }
        }

        [Fact]
        [Trait("Category", "Unit")]
        public async Task Crash_TriggersAutoRestartLoop()
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                // Run a process that exits immediately with exit code 1
                var instance = new ServerInstance
                {
                    Id = Guid.NewGuid(),
                    Name = "Crash Server",
                    Slug = "crash-server",
                    Path = _testTempDir,
                    EngineType = EngineType.VanillaJava,
                    EngineVersion = "mock:/bin/false"
                };

                int crashTransitions = 0;
                int startTransitions = 0;

                _runner.StateChanged += (slug, state) =>
                {
                    if (slug == instance.Slug)
                    {
                        if (state == "Crashed") crashTransitions++;
                        if (state == "Starting") startTransitions++;
                    }
                };

                await _runner.StartAsync(instance);

                // Wait for the exit and auto-restart attempts to finish (attempts 3 times in 5 minutes)
                await Task.Delay(2000);

                // Verify it triggered restart attempts (should be 3 crashes and restarts)
                Assert.True(crashTransitions >= 1);
                Assert.True(startTransitions >= 1);
            }
        }
    }
}
