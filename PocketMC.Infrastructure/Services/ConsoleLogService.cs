using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PocketMC.Core.Services;

namespace PocketMC.Infrastructure.Services
{
    public class ConsoleLogService : IConsoleLogService
    {
        private readonly ISettingsService _settingsService;
        private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _buffers = new();
        private readonly ConcurrentDictionary<string, string> _lastWriteDate = new();
        private readonly object _fileLock = new();

        public event Action<string, string>? LogReceived;
        public event Action<string>? LogsCleared;

        private static readonly Regex AnsiRegex = new(@"\x1B\[[0-9;]*[a-zA-Z]", RegexOptions.Compiled);

        public ConsoleLogService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public void WriteLog(string slug, string line)
        {
            // Thread-safe circular buffer capped at 1000 lines
            var queue = _buffers.GetOrAdd(slug, _ => new ConcurrentQueue<string>());
            queue.Enqueue(line);
            while (queue.Count > 1000)
            {
                queue.TryDequeue(out _);
            }

            // Persist plain text to disk
            WriteToDisk(slug, line);

            // Notify listeners
            LogReceived?.Invoke(slug, line);
        }

        public IReadOnlyList<string> GetLogs(string slug)
        {
            if (_buffers.TryGetValue(slug, out var queue))
            {
                return queue.ToArray();
            }
            return Array.Empty<string>();
        }

        public IReadOnlyList<string> SearchLogs(string slug, string query)
        {
            var logs = GetLogs(slug);
            if (string.IsNullOrEmpty(query))
            {
                return logs;
            }

            return logs.Where(line => line.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public void ClearLogs(string slug)
        {
            if (_buffers.TryGetValue(slug, out var queue))
            {
                while (queue.TryDequeue(out _)) { }
            }

            lock (_fileLock)
            {
                try
                {
                    var instancesRoot = _settingsService.GetInstancesDirectory();
                    var activeLogPath = Path.Combine(instancesRoot, slug, "logs", "pocketmc-latest.log");
                    if (File.Exists(activeLogPath))
                    {
                        File.Delete(activeLogPath);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error clearing latest log file on disk: {ex.Message}");
                }
            }

            LogsCleared?.Invoke(slug);
        }

        private void WriteToDisk(string slug, string line)
        {
            lock (_fileLock)
            {
                try
                {
                    var instancesRoot = _settingsService.GetInstancesDirectory();
                    var instancePath = Path.Combine(instancesRoot, slug);
                    if (!Directory.Exists(instancePath))
                    {
                        Directory.CreateDirectory(instancePath);
                    }

                    var logDir = Path.Combine(instancePath, "logs");
                    if (!Directory.Exists(logDir))
                    {
                        Directory.CreateDirectory(logDir);
                    }

                    var activeLogPath = Path.Combine(logDir, "pocketmc-latest.log");
                    var cleanLine = AnsiRegex.Replace(line, string.Empty);
                    var todayStr = DateTime.UtcNow.ToString("yyyy-MM-day");

                    // Check for daily rotation
                    if (File.Exists(activeLogPath))
                    {
                        var lastWrite = File.GetLastWriteTimeUtc(activeLogPath);
                        if (lastWrite.Date != DateTime.UtcNow.Date)
                        {
                            var rotateName = $"pocketmc-{lastWrite:yyyy-MM-dd}.log";
                            var rotatedPath = Path.Combine(logDir, rotateName);
                            if (!File.Exists(rotatedPath))
                            {
                                File.Move(activeLogPath, rotatedPath);
                            }
                            else
                            {
                                File.Delete(activeLogPath); // Clear if rotated file already exists
                            }
                        }
                    }

                    File.AppendAllText(activeLogPath, cleanLine + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error writing log to disk for instance '{slug}': {ex.Message}");
                }
            }
        }
    }
}
