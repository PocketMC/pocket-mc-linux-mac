using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PocketMC.Core.Models
{
    public enum BackupTrigger
    {
        Manual,
        Scheduled
    }

    public class BackupMetadataEntry
    {
        public string FileName { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public BackupTrigger Trigger { get; set; }
        public string? Label { get; set; }
        public string? Notes { get; set; }
        public long SizeBytes { get; set; }
        public string ServerType { get; set; } = string.Empty;
        public string MinecraftVersion { get; set; } = string.Empty;
        public string? Sha256Checksum { get; set; }
        public bool IntegrityVerified { get; set; }
        public long? SizeDeltaBytes { get; set; }
        public int Version { get; set; }
    }

    public class BackupManifest
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public List<BackupMetadataEntry> Entries { get; set; } = new();
        public DateTime? LastFailedBackupUtc { get; set; }
        public string? LastFailureReason { get; set; }

        public static string GetManifestPath(string serverDir)
            => Path.Combine(serverDir, "backups", "backup-manifest.json");

        public static BackupManifest Load(string serverDir)
        {
            var path = GetManifestPath(serverDir);
            if (!File.Exists(path))
                return new BackupManifest();

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<BackupManifest>(json, JsonOptions) ?? new BackupManifest();
            }
            catch
            {
                return new BackupManifest();
            }
        }

        public void Save(string serverDir)
        {
            var path = GetManifestPath(serverDir);
            var dir = Path.GetDirectoryName(path);
            if (dir != null) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(path, json);
        }

        public int GetNextVersion()
        {
            if (Entries.Count == 0) return 1;
            return Entries.Max(e => e.Version) + 1;
        }

        public void PurgeOrphanedEntries(string defaultBackupDir, string? customBackupDir = null)
        {
            Entries.RemoveAll(e =>
            {
                string? defaultPath = ResolveBackupFilePath(defaultBackupDir, e.FileName);
                if (defaultPath != null && File.Exists(defaultPath))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(customBackupDir))
                {
                    string? customPath = ResolveBackupFilePath(customBackupDir, e.FileName);
                    if (customPath != null && File.Exists(customPath))
                    {
                        return false;
                    }
                }

                return true;
            });
        }

        private static string? ResolveBackupFilePath(string backupDir, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
            {
                return null;
            }

            // Simple contained path check to avoid dependency on PathSafety in Core project
            string resolved = Path.GetFullPath(Path.Combine(backupDir, fileName));
            string root = Path.GetFullPath(backupDir);
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString())) root += Path.DirectorySeparatorChar;

            return resolved.StartsWith(root) ? resolved : null;
        }
    }
}
