using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PocketMC.Core.Models;

namespace PocketMC.Infrastructure.Services
{
    public class SummaryStorageService
    {
        private const string SummariesFolder = "summaries";

        public string Save(string serverDir, SessionSummary summary)
        {
            var dir = Path.Combine(serverDir, SummariesFolder);
            Directory.CreateDirectory(dir);

            var timestamp = summary.SessionEnd.ToLocalTime().ToString("yyyy-MM-dd_HH-mm");
            var fileName = $"summary_{timestamp}.json";
            summary.FileName = fileName;

            var filePath = Path.Combine(dir, fileName);

            int counter = 1;
            while (File.Exists(filePath))
            {
                fileName = $"summary_{timestamp}_{counter}.json";
                filePath = Path.Combine(dir, fileName);
                summary.FileName = fileName;
                counter++;
            }

            var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
            FileUtils.AtomicWriteAllText(filePath, json);
            return filePath;
        }

        public List<SessionSummary> ListSummaries(string serverDir)
        {
            var dir = Path.Combine(serverDir, SummariesFolder);
            if (!Directory.Exists(dir))
                return new List<SessionSummary>();

            var summaries = new List<SessionSummary>();

            foreach (var file in Directory.GetFiles(dir, "summary_*.json").OrderByDescending(f => f))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var summary = JsonSerializer.Deserialize<SessionSummary>(json);
                    if (summary != null)
                    {
                        summary.FileName = Path.GetFileName(file);
                        summaries.Add(summary);
                    }
                }
                catch
                {
                    // Skip malformed files
                }
            }

            return summaries;
        }

        public SessionSummary? Read(string serverDir, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
                return null;

            string filePath = Path.Combine(serverDir, SummariesFolder, fileName);
            if (!File.Exists(filePath)) return null;

            try
            {
                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<SessionSummary>(json);
            }
            catch
            {
                return null;
            }
        }

        public bool Delete(string serverDir, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
                return false;

            string filePath = Path.Combine(serverDir, SummariesFolder, fileName);
            if (!File.Exists(filePath)) return false;

            try
            {
                File.Delete(filePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public int GetCount(string serverDir)
        {
            var dir = Path.Combine(serverDir, SummariesFolder);
            if (!Directory.Exists(dir)) return 0;
            return Directory.GetFiles(dir, "summary_*.json").Length;
        }
    }
}
