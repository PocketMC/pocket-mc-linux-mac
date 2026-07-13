using System;
using System.IO;
using PocketMC.Core.Services;
using PocketMC.Core.Models;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests
{
    public class ConsoleLogTests : IDisposable
    {
        private readonly string _testTempDir;
        private readonly SettingsService _settingsService;
        private readonly ConsoleLogService _logService;

        public ConsoleLogTests()
        {
            _testTempDir = Path.Combine(Path.GetTempPath(), "ConsoleLogTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testTempDir);

            _settingsService = new SettingsService(_testTempDir);
            _settingsService.Settings.CustomDataRoot = _testTempDir;
            _settingsService.Load();

            _logService = new ConsoleLogService(_settingsService);
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

        [Fact]
        [Trait("Category", "Unit")]
        public void WriteLog_BuffersInCircularQueueAndFilters()
        {
            var slug = "test-log-server";

            for (int i = 1; i <= 1050; i++)
            {
                _logService.WriteLog(slug, $"Log entry line {i}");
            }

            var buffered = _logService.GetLogs(slug);
            Assert.Equal(1000, buffered.Count);
            // Verify oldest elements were dequeued (we have 51 to 1050)
            Assert.Equal("Log entry line 51", buffered[0]);
            Assert.Equal("Log entry line 1050", buffered[999]);

            // Search
            var searchResults = _logService.SearchLogs(slug, "line 1000");
            Assert.Single(searchResults);
            Assert.Equal("Log entry line 1000", searchResults[0]);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void WriteLog_StripsAnsiAndWritesToDisk()
        {
            var slug = "ansi-server";
            // ESC[31m is red color ANSI sequence
            var ansiLine = "\x1B[31m[INFO] Welcome to Minecraft server!\x1B[0m";

            _logService.WriteLog(slug, ansiLine);

            var instancesRoot = _settingsService.GetInstancesDirectory();
            var logFile = Path.Combine(instancesRoot, slug, "logs", "pocketmc-latest.log");

            Assert.True(File.Exists(logFile));
            var text = File.ReadAllText(logFile).Trim();
            Assert.Equal("[INFO] Welcome to Minecraft server!", text);
        }
    }
}
