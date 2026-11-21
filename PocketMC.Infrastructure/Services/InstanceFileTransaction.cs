using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace PocketMC.Infrastructure.Services
{
    /// <summary>
    /// Transactional promotion with durable journal rollback for multi-file operations.
    /// </summary>
    public static class InstanceFileTransaction
    {
        private const string JournalName = ".pocketmc-file-transaction.json";

        public sealed record Replacement(string? StagedPath, string RelativePath);
        public sealed record Entry(string RelativePath, string BackupRelativePath, bool Existed);
        public sealed record Journal(List<Entry> Entries, bool Committed = false);

        public static bool Recover(string serverDir)
        {
            string journalPath = Path.Combine(serverDir, JournalName);
            if (!File.Exists(journalPath)) return false;

            Journal? journal;
            try
            {
                var content = File.ReadAllText(journalPath);
                journal = JsonSerializer.Deserialize<Journal>(content);
            }
            catch
            {
                return false;
            }

            if (journal == null) return false;

            if (!journal.Committed)
            {
                // Rollback entries in reverse order
                foreach (var entry in journal.Entries.AsEnumerable().Reverse())
                {
                    string? target = PathSafety.ValidateContainedPath(serverDir, entry.RelativePath);
                    string? backup = PathSafety.ValidateContainedPath(serverDir, entry.BackupRelativePath);
                    if (target == null || backup == null) continue;

                    if (!entry.BackupRelativePath.StartsWith(".pocketmc-rollback-", StringComparison.Ordinal)) continue;

                    if (Exists(backup))
                    {
                        Delete(target);
                        Move(backup, target);
                    }
                    else if (!entry.Existed)
                    {
                        Delete(target);
                    }
                }
            }

            // Clean up remaining rollback backup files
            foreach (var entry in journal.Entries)
            {
                string? backup = PathSafety.ValidateContainedPath(serverDir, entry.BackupRelativePath);
                if (backup != null)
                {
                    Delete(backup);
                    string? parent = Path.GetDirectoryName(backup);
                    if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any())
                    {
                        try { Directory.Delete(parent); } catch { }
                    }
                }
            }

            try { File.Delete(journalPath); } catch { }
            return true;
        }

        public static void Commit(string serverDir, IReadOnlyList<Replacement> replacements, CancellationToken cancellationToken = default)
        {
            Recover(serverDir);
            string rollback = $".pocketmc-rollback-{Guid.NewGuid():N}";
            var journal = new Journal(new List<Entry>());
            string journalPath = Path.Combine(serverDir, JournalName);

            try
            {
                foreach (var replacement in replacements)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string target = PathSafety.ValidateContainedPath(serverDir, replacement.RelativePath)
                        ?? throw new InvalidDataException($"Unsafe transaction target: {replacement.RelativePath}");

                    if (replacement.StagedPath != null && !Exists(replacement.StagedPath))
                    {
                        throw new FileNotFoundException("Staged replacement file is missing.", replacement.StagedPath);
                    }

                    string relativeBackup = Path.Combine(rollback, journal.Entries.Count.ToString());
                    string backup = PathSafety.ValidateContainedPath(serverDir, relativeBackup)
                        ?? throw new InvalidDataException("Unsafe transaction backup path.");

                    journal.Entries.Add(new Entry(replacement.RelativePath, relativeBackup, Exists(target)));
                    FileUtils.AtomicWriteAllText(journalPath, JsonSerializer.Serialize(journal, new JsonSerializerOptions { WriteIndented = true }));

                    if (Exists(target))
                    {
                        Move(target, backup);
                    }

                    if (replacement.StagedPath != null)
                    {
                        Move(replacement.StagedPath, target);
                    }
                }

                FileUtils.AtomicWriteAllText(journalPath, JsonSerializer.Serialize(journal with { Committed = true }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                Recover(serverDir);
                throw;
            }

            // Cleanup committed rollback artifacts
            Recover(serverDir);
        }

        private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

        private static void Move(string source, string destination)
        {
            string? parent = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            if (Directory.Exists(source))
            {
                Directory.Move(source, destination);
            }
            else if (File.Exists(source))
            {
                File.Move(source, destination, true);
            }
        }

        private static void Delete(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
