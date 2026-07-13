using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.Infrastructure.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly string _configDirectory;
        private Settings _settings;

        public Settings Settings => _settings;

        public SettingsService()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                _configDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "PocketMC");
            }
            else
            {
                _configDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "PocketMC");
            }

            Directory.CreateDirectory(_configDirectory);
            _settings = new Settings();
            Load();
        }

        public string GetSettingsDirectory() => _configDirectory;

        private string GetDataRoot()
        {
            if (!string.IsNullOrWhiteSpace(_settings.CustomDataRoot))
            {
                return _settings.CustomDataRoot;
            }
            return _configDirectory;
        }

        public string GetInstancesDirectory() => GetOrCreateSubdir("Instances");
        public string GetBackupsDirectory() => GetOrCreateSubdir("Backups");
        public string GetDownloadsDirectory() => GetOrCreateSubdir("Downloads");
        public string GetCacheDirectory() => GetOrCreateSubdir("Cache");
        public string GetLogsDirectory() => GetOrCreateSubdir("Logs");

        private string GetOrCreateSubdir(string name)
        {
            var path = Path.Combine(GetDataRoot(), name);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Load()
        {
            var filePath = Path.Combine(_configDirectory, "settings.json");
            if (File.Exists(filePath))
            {
                try
                {
                    var json = File.ReadAllText(filePath);
                    _settings = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
                }
                catch
                {
                    _settings = new Settings();
                }
            }
            else
            {
                _settings = new Settings();
                Save();
            }

            // Ensure directories are created
            Directory.CreateDirectory(GetInstancesDirectory());
            Directory.CreateDirectory(GetBackupsDirectory());
            Directory.CreateDirectory(GetDownloadsDirectory());
            Directory.CreateDirectory(GetCacheDirectory());
            Directory.CreateDirectory(GetLogsDirectory());
        }

        public void Save()
        {
            var filePath = Path.Combine(_configDirectory, "settings.json");
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_settings, options);
            File.WriteAllText(filePath, json);
        }
    }
}
