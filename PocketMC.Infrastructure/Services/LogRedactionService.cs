using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PocketMC.Core.Services;

namespace PocketMC.Infrastructure.Services
{
    public class LogRedactionService : ILogRedactionService
    {
        private readonly IConsoleLogService _consoleLogService;
        private readonly ISettingsService _settingsService;

        private static readonly Regex AnsiRegex = new(@"\x1B\[[0-9;]*[a-zA-Z]", RegexOptions.Compiled);
        private static readonly Regex IPv4Regex = new(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b", RegexOptions.Compiled);
        private static readonly Regex IPv6Regex = new(@"\b(?:[0-9a-fA-F]{1,4}:){1,7}[0-9a-fA-F]{1,4}\b", RegexOptions.Compiled);
        private static readonly Regex UserPathRegex = new(@"(/Users/|/home/)[^/\\\s\)\:]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Crash signatures
        private static readonly string[] CrashSignatures = new[]
        {
            "at ",
            "Exception in thread",
            "Exception",
            "Error",
            "fatal",
            "Traceback",
            "pocketmine\\utils\\",
            "RuntimeException",
            "NullPointerException",
            "StackOverflowError"
        };

        public LogRedactionService(IConsoleLogService consoleLogService, ISettingsService settingsService)
        {
            _consoleLogService = consoleLogService;
            _settingsService = settingsService;
        }

        public string RedactLog(string content)
        {
            if (string.IsNullOrEmpty(content)) return string.Empty;

            // Remove ANSI escape codes
            var clean = AnsiRegex.Replace(content, string.Empty);

            // Redact IPs
            clean = IPv4Regex.Replace(clean, "<IP-REDACTED>");
            clean = IPv6Regex.Replace(clean, "<IP-REDACTED>");

            // Redact user paths
            clean = UserPathRegex.Replace(clean, "$1<user>");

            return clean;
        }

        public string GetRedactedDiagnosticsLog(string slug)
        {
            var lines = new List<string>();

            // 1. Try to get in-memory logs
            var inMemoryLogs = _consoleLogService.GetLogs(slug);
            if (inMemoryLogs != null && inMemoryLogs.Count > 0)
            {
                lines.AddRange(inMemoryLogs.Select(l => AnsiRegex.Replace(l, string.Empty)));
            }
            else
            {
                // 2. Fall back to reading from disk
                try
                {
                    var instancesRoot = _settingsService.GetInstancesDirectory();
                    var activeLogPath = Path.Combine(instancesRoot, slug, "logs", "pocketmc-latest.log");
                    if (File.Exists(activeLogPath))
                    {
                        var diskLines = File.ReadAllLines(activeLogPath);
                        lines.AddRange(diskLines);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading logs from disk for redaction: {ex.Message}");
                }
            }

            if (lines.Count == 0)
            {
                return "No log lines available.";
            }

            // Clean each line
            for (int i = 0; i < lines.Count; i++)
            {
                lines[i] = RedactLog(lines[i]);
            }

            // Smart tail scan:
            // Scan up to 1000 lines backward. Find the earliest crash signature within that range.
            int maxScanLines = Math.Min(lines.Count, 1000);
            int exceptionStartIndex = -1;

            for (int i = lines.Count - 1; i >= lines.Count - maxScanLines; i--)
            {
                var line = lines[i];
                if (CrashSignatures.Any(sig => line.Contains(sig, StringComparison.OrdinalIgnoreCase)))
                {
                    // Found a potential starting point for exception context
                    exceptionStartIndex = i;
                }
            }

            int startTakeFromIndex;
            if (exceptionStartIndex != -1)
            {
                // We want to capture the exception and everything after it.
                // But we must limit to 1000 lines.
                startTakeFromIndex = exceptionStartIndex;
                int count = lines.Count - startTakeFromIndex;
                if (count > 1000)
                {
                    startTakeFromIndex = lines.Count - 1000;
                }
            }
            else
            {
                // No exception signature found, return last 200 lines
                int countToTake = Math.Min(lines.Count, 200);
                startTakeFromIndex = lines.Count - countToTake;
            }

            var range = lines.Skip(startTakeFromIndex).Take(lines.Count - startTakeFromIndex);
            return string.Join(Environment.NewLine, range);
        }
    }
}
