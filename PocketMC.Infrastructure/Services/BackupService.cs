using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Utils;

namespace PocketMC.Infrastructure.Services
{
    public class LocalBackupResult
    {
        public bool Success { get; set; }
        public string ZipPath { get; set; } = string.Empty;
        public Exception? Error { get; set; }
    }

    public class BackupService : IBackupService
    {
        private static readonly Regex SaveCompletedRegex = new(
            @"(saved the game|saved the world|saved chunks|saved all chunks|all dimensions are saved|world saved)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));

        private readonly IProcessRunner _processRunner;
        private readonly ISettingsService _settingsService;
        private readonly CloudBackupService _cloudBackupService;
        private readonly IConsoleLogService _logService;
        private readonly ILogger<BackupService> _logger;

        private static readonly HashSet<string> SkipFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            "session.lock"
        };

        public BackupService(
            IProcessRunner processRunner,
            ISettingsService settingsService,
            CloudBackupService cloudBackupService,
            IConsoleLogService logService,
            ILogger<BackupService> logger)
        {
            _processRunner = processRunner;
            _settingsService = settingsService;
            _cloudBackupService = cloudBackupService;
            _logService = logService;
            _logger = logger;
        }

        public async Task<BackupMetadataEntry> CreateBackupAsync(
            ServerInstance instance,
            BackupTrigger trigger,
            string? label = null,
            string? notes = null,
            Action<string>? onProgress = null,
            IProgress<double>? progress = null)
        {
            var localResult = await CreateLocalBackupInternalAsync(instance, onProgress, progress);

            if (!localResult.Success || string.IsNullOrEmpty(localResult.ZipPath))
            {
                RecordBackupFailure(instance.Path, localResult.Error?.Message ?? "Unknown error");
                if (localResult.Error != null) throw localResult.Error;
                throw new IOException("Backup failed to produce a valid zip file.");
            }

            onProgress?.Invoke("Recording backup metadata...");
            var entry = RecordBackupMetadata(instance, localResult.ZipPath, trigger, label, notes);

            // External replication
            await ReplicateToExternalDirectoryAsync(instance, localResult.ZipPath, onProgress);

            // Cloud replication
            await _cloudBackupService.UploadBackupToEnabledProvidersAsync(
                instance.Id,
                instance.Name,
                localResult.ZipPath,
                trigger == BackupTrigger.Manual,
                onProgress,
                progress);

            // Update metadata
            instance.LastBackupTime = DateTime.UtcNow;

            // Prune old backups
            var backupDir = GetBackupDirectory(instance);
            PruneOldBackups(backupDir, instance.MaxBackupsToKeep);

            // Purge manifest entries whose files were pruned
            try
            {
                var manifest = BackupManifest.Load(instance.Path);
                string defaultBackupDir = Path.Combine(instance.Path, "backups");
                manifest.PurgeOrphanedEntries(defaultBackupDir, instance.CustomBackupDirectory);
                manifest.Save(instance.Path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to purge orphaned manifest entries.");
            }

            return entry;
        }

        private BackupMetadataEntry RecordBackupMetadata(
            ServerInstance instance,
            string zipPath,
            BackupTrigger trigger,
            string? label = null,
            string? notes = null)
        {
            var manifest = BackupManifest.Load(instance.Path);
            var fi = new FileInfo(zipPath);
            var previousEntry = manifest.Entries
                .OrderByDescending(e => e.Version)
                .FirstOrDefault();

            var entry = new BackupMetadataEntry
            {
                FileName = fi.Name,
                CreatedAtUtc = DateTime.UtcNow,
                Trigger = trigger,
                Label = label,
                Notes = notes,
                SizeBytes = fi.Length,
                ServerType = instance.EngineType.ToString(),
                MinecraftVersion = instance.EngineVersion,
                Version = manifest.GetNextVersion(),
                SizeDeltaBytes = previousEntry != null ? fi.Length - previousEntry.SizeBytes : null,
                Sha256Checksum = ComputeSha256(zipPath),
                IntegrityVerified = true
            };

            manifest.Entries.Add(entry);
            manifest.LastFailedBackupUtc = null;
            manifest.LastFailureReason = null;
            manifest.Save(instance.Path);

            return entry;
        }

        private void RecordBackupFailure(string serverDir, string reason)
        {
            try
            {
                var manifest = BackupManifest.Load(serverDir);
                manifest.LastFailedBackupUtc = DateTime.UtcNow;
                manifest.LastFailureReason = reason;
                manifest.Save(serverDir);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record backup failure.");
            }
        }

        private string? ComputeSha256(string filePath)
        {
            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var sha = SHA256.Create();
                var hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not compute backup checksum for {FilePath}.", filePath);
                return null;
            }
        }

        public bool? VerifyBackupIntegrity(ServerInstance instance, string fileName, string? zipPath = null)
        {
            if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
            {
                return false;
            }

            var manifest = BackupManifest.Load(instance.Path);
            var entry = manifest.Entries.FirstOrDefault(e =>
                string.Equals(e.FileName, fileName, StringComparison.OrdinalIgnoreCase));

            if (entry?.Sha256Checksum == null) return null;

            string fullPath = zipPath ?? Path.Combine(GetBackupDirectory(instance), fileName);
            if (!File.Exists(fullPath)) return false;

            var currentHash = ComputeSha256(fullPath);
            return string.Equals(entry.Sha256Checksum, currentHash, StringComparison.OrdinalIgnoreCase);
        }

        private string GetBackupDirectory(ServerInstance instance)
        {
            if (!string.IsNullOrWhiteSpace(instance.CustomBackupDirectory))
            {
                return instance.CustomBackupDirectory;
            }
            return Path.Combine(instance.Path, "backups");
        }

        private async Task<LocalBackupResult> CreateLocalBackupInternalAsync(
            ServerInstance instance,
            Action<string>? onProgress,
            IProgress<double>? progress)
        {
            string worldDir;
            string worldDisplayName;
            try
            {
                worldDir = ResolveWorldDirectory(instance);
                worldDisplayName = Path.GetRelativePath(instance.Path, worldDir);
            }
            catch (Exception ex)
            {
                return new LocalBackupResult { Success = false, Error = ex };
            }

            if (!Directory.Exists(worldDir))
            {
                return new LocalBackupResult { Success = false, Error = new DirectoryNotFoundException($"World folder '{worldDisplayName}' not found in server directory.") };
            }

            var backupDir = GetBackupDirectory(instance);
            Directory.CreateDirectory(backupDir);

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            string zipPath = Path.Combine(backupDir, $"world-{timestamp}.zip");

            if (File.Exists(zipPath))
            {
                try
                {
                    File.Delete(zipPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete pre-existing backup file at {ZipPath}", zipPath);
                }
            }

            bool isRunning = _processRunner.TryGetRunningInfo(instance.Slug, out _, out var state) && state == "Running";
            var skippedFiles = new List<string>();

            try
            {
                if (isRunning)
                {
                    bool syncSuccess = await TrySyncSaveViaRconAsync(instance.Path, onProgress);

                    if (!syncSuccess)
                    {
                        _logger.LogInformation("RCON sync not available or failed; falling back to console ingestion for server {ServerName}.", instance.Name);

                        onProgress?.Invoke("Disabling auto-save (Console)...");
                        await _processRunner.SendCommandAsync(instance, "save-off");
                        await Task.Delay(500);

                        onProgress?.Invoke("Flushing world to disk (Console)...");
                        int initialLogCount = _logService.GetLogs(instance.Slug).Count;
                        await _processRunner.SendCommandAsync(instance, "save-all");

                        onProgress?.Invoke("Waiting for save to complete...");
                        bool saved = false;
                        for (int i = 0; i < 30; i++) // Up to 15 seconds
                        {
                            await Task.Delay(500);
                            var currentLogs = _logService.GetLogs(instance.Slug);
                            if (currentLogs.Count > initialLogCount)
                            {
                                var newLines = currentLogs.Skip(initialLogCount);
                                if (newLines.Any(line => SaveCompletedRegex.IsMatch(line)))
                                {
                                    saved = true;
                                    break;
                                }
                            }
                        }

                        if (!saved)
                        {
                            _logger.LogWarning("Server {ServerName} did not emit a recognized save confirmation. Proceeding after a short settle delay.", instance.Name);
                            await Task.Delay(TimeSpan.FromSeconds(2));
                        }
                    }
                }

                onProgress?.Invoke("Compressing world...");
                await Task.Run(() => CreateZipWithLockedFileSkip(worldDir, zipPath, skippedFiles, progress));

                var zipInfo = new FileInfo(zipPath);
                if (!zipInfo.Exists || zipInfo.Length == 0)
                {
                    if (File.Exists(zipPath)) File.Delete(zipPath);
                    return new LocalBackupResult { Success = false, Error = new IOException("Backup produced an empty ZIP file.") };
                }

                if (skippedFiles.Count > 0)
                    onProgress?.Invoke($"Backup complete! ({skippedFiles.Count} locked file(s) skipped)");
                else
                    onProgress?.Invoke("Backup complete!");

                return new LocalBackupResult { Success = true, ZipPath = zipPath };
            }
            catch (Exception ex)
            {
                if (File.Exists(zipPath))
                {
                    TryDeleteFile(zipPath, "partial backup ZIP");
                }
                _logger.LogError(ex, "Backup failed for server {ServerName}.", instance.Name);
                return new LocalBackupResult { Success = false, Error = ex };
            }
            finally
            {
                if (isRunning)
                {
                    try
                    {
                        await _processRunner.SendCommandAsync(instance, "save-on");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to re-enable auto-save.");
                    }
                }
            }
        }

        private string ResolveWorldDirectory(ServerInstance instance)
        {
            if (instance.EngineType == EngineType.PocketMine)
            {
                return Path.Combine(instance.Path, "worlds");
            }

            if (instance.EngineType == EngineType.Bedrock)
            {
                string levelName = "Bedrock level";
                if (TryGetProperty(instance.Path, "level-name", out var configuredLevelName) &&
                    !string.IsNullOrWhiteSpace(configuredLevelName))
                {
                    levelName = configuredLevelName.Trim();
                }

                string safeLevelName = ValidateBedrockLevelName(levelName);
                string worldsRoot = Path.Combine(instance.Path, "worlds");
                string? resolved = PathSafety.ValidateContainedPath(worldsRoot, safeLevelName);
                if (resolved == null)
                {
                    throw new InvalidDataException("Bedrock level-name resolves outside the worlds directory. Refusing backup/restore for safety.");
                }

                return resolved;
            }

            return Path.Combine(instance.Path, "world");
        }

        internal static string ValidateBedrockLevelName(string levelName)
        {
            string trimmed = levelName.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                throw new InvalidDataException("Bedrock level-name cannot be empty.");
            }

            if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                trimmed.IndexOfAny(new[] { '/', '\\', ':', '\0', '\r', '\n', '\t' }) >= 0 ||
                PathSafety.ContainsTraversal(trimmed) ||
                Path.IsPathRooted(trimmed))
            {
                throw new InvalidDataException($"Bedrock level-name '{trimmed}' is not safe to use as a world folder name.");
            }

            return trimmed;
        }

        private static bool TryGetProperty(string serverDir, string key, out string? value)
        {
            value = null;
            string propPath = Path.Combine(serverDir, "server.properties");
            if (!File.Exists(propPath)) return false;

            var lines = File.ReadAllLines(propPath);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("#") || !trimmed.Contains("=")) continue;
                var idx = trimmed.IndexOf('=');
                var k = trimmed.Substring(0, idx).Trim();
                if (k.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    value = trimmed.Substring(idx + 1).Trim();
                    return true;
                }
            }
            return false;
        }

        private async Task ReplicateToExternalDirectoryAsync(ServerInstance instance, string zipPath, Action<string>? onProgress)
        {
            var appSettings = _settingsService.Settings;
            if (!string.IsNullOrWhiteSpace(appSettings.ExternalBackupDirectory) && Directory.Exists(appSettings.ExternalBackupDirectory))
            {
                onProgress?.Invoke("Replicating to external storage...");
                try
                {
                    string timestamp = Path.GetFileNameWithoutExtension(zipPath).Replace("world-", "");
                    string externalTarget = Path.Combine(appSettings.ExternalBackupDirectory, instance.Name, "backups");
                    Directory.CreateDirectory(externalTarget);

                    string destinationPath = Path.Combine(externalTarget, $"world-{timestamp}.zip");
                    await Task.Run(() => File.Copy(zipPath, destinationPath, true));
                    _logger.LogInformation("Successfully replicated backup to external location: {Destination}", destinationPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to replicate backup to external directory");
                    onProgress?.Invoke("Warning: External replication failed (Check logs)");
                }
            }
        }

        private async Task<bool> TrySyncSaveViaRconAsync(string serverDir, Action<string>? onProgress)
        {
            try
            {
                if (!TryGetProperty(serverDir, "enable-rcon", out var rconEnabled) || rconEnabled != "true") return false;

                TryGetProperty(serverDir, "rcon.port", out var portStr);
                TryGetProperty(serverDir, "rcon.password", out var password);

                if (string.IsNullOrEmpty(password) || !int.TryParse(portStr ?? "25575", out int port)) return false;

                onProgress?.Invoke("Connecting to RCON...");
                using var rcon = new RconClient("127.0.0.1", port, password);
                await rcon.ConnectAsync();

                onProgress?.Invoke("Syncing via RCON: save-off");
                await rcon.ExecuteCommandAsync("save-off");

                onProgress?.Invoke("Syncing via RCON: save-all");
                await rcon.ExecuteCommandAsync("save-all");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "RCON sync failed.");
                return false;
            }
        }

        private void CreateZipWithLockedFileSkip(string sourceDir, string zipPath, List<string> skippedFiles, IProgress<double>? progress)
        {
            using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            var allFiles = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);

            long totalBytes = 0;
            foreach (var filePath in allFiles)
            {
                if (!SkipFiles.Contains(Path.GetFileName(filePath)))
                {
                    totalBytes += new FileInfo(filePath).Length;
                }
            }

            long copiedBytes = 0;
            byte[] buffer = new byte[81920];

            foreach (var filePath in allFiles)
            {
                string relativePath = Path.GetRelativePath(sourceDir, filePath);
                string fileName = Path.GetFileName(filePath);

                if (SkipFiles.Contains(fileName))
                {
                    skippedFiles.Add(relativePath);
                    continue;
                }

                try
                {
                    var entry = archive.CreateEntry(relativePath, CompressionLevel.Fastest);
                    using var entryStream = entry.Open();
                    using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    
                    int bytesRead;
                    while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        entryStream.Write(buffer, 0, bytesRead);
                        copiedBytes += bytesRead;
                        if (totalBytes > 0)
                        {
                            progress?.Report((double)copiedBytes / totalBytes * 100);
                        }
                    }
                }
                catch (IOException)
                {
                    skippedFiles.Add(relativePath);
                }
                catch (UnauthorizedAccessException)
                {
                    skippedFiles.Add(relativePath);
                }
            }
        }

        public async Task RestoreBackupAsync(
            ServerInstance instance,
            string backupZipPath,
            Action<string>? onProgress = null,
            IProgress<double>? progress = null)
        {
            string worldDir = ResolveWorldDirectory(instance);

            if (string.IsNullOrWhiteSpace(backupZipPath) || !File.Exists(backupZipPath))
            {
                throw new FileNotFoundException("Backup file not found.", backupZipPath);
            }

            onProgress?.Invoke("Verifying backup integrity...");
            try
            {
                using var archive = ZipFile.OpenRead(backupZipPath);
                _ = archive.Entries.Count;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Backup ZIP file is corrupt or cannot be read.", ex);
            }

            var fileName = Path.GetFileName(backupZipPath);
            var integrity = VerifyBackupIntegrity(instance, fileName, backupZipPath);
            if (integrity == false)
            {
                throw new InvalidDataException("Backup ZIP file is corrupt (checksum mismatch).");
            }

            var stageDir = Path.Combine(instance.Path, $".restore-stage-{Guid.NewGuid():N}");
            if (Directory.Exists(stageDir))
            {
                await TryCleanDirectoryAsync(stageDir, "existing staging directory");
            }

            onProgress?.Invoke("Extracting backup to staging area...");
            try
            {
                await SafeZipExtractor.ExtractAsync(backupZipPath, stageDir, (current, total) =>
                {
                    if (total > 0)
                    {
                        double percent = (double)current / total * 100.0;
                        progress?.Report(percent);
                    }
                });
            }
            catch (Exception ex)
            {
                await TryCleanDirectoryAsync(stageDir, "restore staging directory");
                throw new InvalidDataException($"Failed to extract backup ZIP: {ex.Message}", ex);
            }

            onProgress?.Invoke("Validating world structure...");
            if (!HasLevelDat(stageDir) && instance.EngineType != EngineType.PocketMine)
            {
                await TryCleanDirectoryAsync(stageDir, "restore staging directory");
                throw new InvalidDataException("Could not find level.dat in the backup. This doesn't appear to be a valid Minecraft world backup.");
            }

            var backupWorldDir = Path.Combine(instance.Path, $".restore-backup-{DateTime.Now:yyyyMMddHHmmss}");
            if (Directory.Exists(backupWorldDir))
            {
                await TryCleanDirectoryAsync(backupWorldDir, "pre-existing restore backup directory");
            }

            bool oldWorldExisted = Directory.Exists(worldDir);
            if (oldWorldExisted)
            {
                onProgress?.Invoke("Backing up current world...");
                try
                {
                    Directory.Move(worldDir, backupWorldDir);
                }
                catch (Exception ex)
                {
                    await TryCleanDirectoryAsync(stageDir, "restore staging directory");
                    throw new IOException($"Failed to backup current world: {ex.Message}", ex);
                }
            }

            onProgress?.Invoke("Applying restored world...");
            try
            {
                Directory.Move(stageDir, worldDir);
            }
            catch (Exception ex)
            {
                onProgress?.Invoke("Restore failed, rolling back to original world...");
                if (oldWorldExisted && Directory.Exists(backupWorldDir))
                {
                    try
                    {
                        if (Directory.Exists(worldDir))
                        {
                            await TryCleanDirectoryAsync(worldDir, "failed restore world directory");
                        }
                        Directory.Move(backupWorldDir, worldDir);
                    }
                    catch (Exception rollbackEx)
                    {
                        _logger.LogCritical(rollbackEx, "CRITICAL: Failed to roll back original world after restore failure.");
                    }
                }
                await TryCleanDirectoryAsync(stageDir, "restore staging directory");
                throw new IOException($"Failed to move restored world into place: {ex.Message}", ex);
            }

            if (oldWorldExisted && Directory.Exists(backupWorldDir))
            {
                onProgress?.Invoke("Cleaning up backup...");
                try
                {
                    await FileUtils.CleanDirectoryAsync(backupWorldDir);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up backup world directory at {BackupWorldDir}", backupWorldDir);
                }
            }

            onProgress?.Invoke("World restored successfully!");
        }

        private bool HasLevelDat(string dir)
        {
            if (File.Exists(Path.Combine(dir, "level.dat")) || File.Exists(Path.Combine(dir, "level.dat_old")))
                return true;

            foreach (var subDir in Directory.GetDirectories(dir))
            {
                if (HasLevelDat(subDir))
                    return true;
            }

            return false;
        }

        private void PruneOldBackups(string backupDirectory, int maxToKeep)
        {
            var files = new DirectoryInfo(backupDirectory)
                .GetFiles("world-*.zip")
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            for (int i = maxToKeep; i < files.Count; i++)
            {
                try
                {
                    files[i].Delete();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to prune old backup {BackupFile}.", files[i].FullName);
                }
            }
        }

        private async Task TryCleanDirectoryAsync(string directory, string description)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    await FileUtils.CleanDirectoryAsync(directory);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Best-effort cleanup could not clean {Description} at {Directory}.", description, directory);
            }
        }

        private void TryDeleteFile(string path, string description)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Best-effort cleanup could not delete {Description} at {Path}.", description, path);
            }
        }
    }
}
