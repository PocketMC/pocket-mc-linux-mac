using System;
using System.IO;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests
{
    public class LogRedactionServiceTests : IDisposable
    {
        private readonly string _testTempDir;
        private readonly SettingsService _settingsService;
        private readonly ConsoleLogService _logService;
        private readonly LogRedactionService _redactionService;

        public LogRedactionServiceTests()
        {
            _testTempDir = Path.Combine(Path.GetTempPath(), "LogRedactionServiceTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testTempDir);

            _settingsService = new SettingsService(_testTempDir);
            _settingsService.Settings.CustomDataRoot = _testTempDir;
            _settingsService.Load();

            _logService = new ConsoleLogService(_settingsService);
            _redactionService = new LogRedactionService(_logService, _settingsService);
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
        public void RedactLog_RemovesIPsAndHomePaths_KeepsUsernames()
        {
            var rawLog = "[21:08:54] [Server thread/INFO]: Player Steve joined from IP 192.168.1.15 and port 54321 with IPv6 2001:0db8:85a3:0000:0000:8a2e:0370:7334. Root directory: /home/sahaj33/PocketMC/bin. Config directory: /Users/johndoe/Library/Application Support.";
            var redacted = _redactionService.RedactLog(rawLog);

            // Verify IPs are redacted
            Assert.Contains("<IP-REDACTED>", redacted);
            Assert.DoesNotContain("192.168.1.15", redacted);
            Assert.DoesNotContain("2001:0db8:85a3:0000:0000:8a2e:0370:7334", redacted);

            // Verify paths are redacted
            Assert.Contains("/home/<user>", redacted);
            Assert.Contains("/Users/<user>", redacted);
            Assert.DoesNotContain("sahaj33", redacted);
            Assert.DoesNotContain("johndoe", redacted);

            // Verify player username is kept intact
            Assert.Contains("Steve", redacted);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GetRedactedDiagnosticsLog_CapturesExceptionTail()
        {
            var slug = "crash-server";

            // Enqueue 300 normal lines
            for (int i = 1; i <= 300; i++)
            {
                _logService.WriteLog(slug, $"Normal line {i} with IP 1.2.3.4");
            }

            // Enqueue an exception block
            _logService.WriteLog(slug, "Exception in thread \"main\" java.lang.NullPointerException: Cannot invoke method");
            _logService.WriteLog(slug, "    at com.example.MyClass.run(MyClass.java:45)");
            _logService.WriteLog(slug, "    at com.example.MyClass.main(MyClass.java:12)");

            // Enqueue 10 more lines after exception
            for (int i = 1; i <= 10; i++)
            {
                _logService.WriteLog(slug, $"Post-crash cleanup line {i}");
            }

            var diagnostics = _redactionService.GetRedactedDiagnosticsLog(slug);

            // Should have captured the exception start and subsequent lines
            Assert.Contains("Exception in thread", diagnostics);
            Assert.Contains("at com.example.MyClass.run", diagnostics);
            Assert.Contains("Post-crash cleanup line 10", diagnostics);

            // IPs within the captured diagnostics log should be redacted
            Assert.Contains("<IP-REDACTED>", diagnostics);
            Assert.DoesNotContain("1.2.3.4", diagnostics);
        }
    }
}
