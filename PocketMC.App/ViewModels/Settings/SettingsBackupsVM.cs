using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;

namespace PocketMC.App.ViewModels.Settings
{
    public partial class BackupItemViewModel : ObservableObject
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public long SizeBytes { get; set; }
        public long? SizeDeltaBytes { get; set; }
        public int Version { get; set; }
        public string Trigger { get; set; } = "Manual";
        public string LocationText { get; set; } = "Local";
        public string ServerType { get; set; } = string.Empty;
        public string ServerVersion { get; set; } = string.Empty;
        public bool HasMetadata { get; set; }

        [ObservableProperty]
        private string? _label;

        [ObservableProperty]
        private string _integrityStatusText = string.Empty;

        public string Name => string.IsNullOrWhiteSpace(Label) ? FileName : Label!;
        public string FullPath => FilePath;
        public string VersionText => Version > 0 ? $"#{Version}" : "-";
        public string TriggerText => Trigger;
        public string FormattedSize => FormatBytes(SizeBytes);
        public string SizeText => FormattedSize;
        public string FormattedDate => CreatedAtUtc.ToLocalTime().ToString("g");
        public string CreatedAtText => FormattedDate;
        public string SizeDeltaText => SizeDeltaBytes == null
            ? string.Empty
            : SizeDeltaBytes >= 0
                ? $"+{FormatBytes(SizeDeltaBytes.Value)}"
                : $"-{FormatBytes(Math.Abs(SizeDeltaBytes.Value))}";

        private static string FormatBytes(long bytes) => bytes switch
        {
            >= 1024L * 1024L * 1024L => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB",
            >= 1024L * 1024L => $"{bytes / (1024.0 * 1024.0):F1} MB",
            _ => $"{bytes / 1024.0:F1} KB"
        };
    }

    public partial class SettingsBackupsVM : ObservableObject
    {
        private ServerInstance? _instance;
        private string _serverDir = string.Empty;
        private readonly BackupService? _backupService;
        private readonly IDialogService _dialogService;
        private readonly Func<bool>? _isRunningCheck;
        private readonly Action _markDirty;

        [ObservableProperty] private int _backupIntervalHours = 24;
        [ObservableProperty] private int _maxBackupsToKeep = 5;
        [ObservableProperty] private bool _automaticBackupPruningEnabled = true;
        [ObservableProperty] private string? _customBackupDirectory;
        [ObservableProperty] private bool _isBackingUp;
        [ObservableProperty] private double _backupProgressValue;
        [ObservableProperty] private bool _isBackupProgressIndeterminate;
        [ObservableProperty] private string _backupStatusText = string.Empty;
        [ObservableProperty] private BackupItemViewModel? _selectedBackup;

        [ObservableProperty] private bool _hasHealthWarning;
        [ObservableProperty] private bool _hasHealthCritical;
        [ObservableProperty] private string _healthWarningText = string.Empty;

        [ObservableProperty] private string _totalBackupsSizeText = "0 KB";
        [ObservableProperty] private string _lastBackupTimeText = "Never";
        [ObservableProperty] private string _diskSpaceText = "Unknown";
        [ObservableProperty] private bool _showDiskWarning;

        public ObservableCollection<BackupItemViewModel> BackupList { get; } = new();

        public IAsyncRelayCommand CreateBackupCommand { get; }
        public IAsyncRelayCommand<string?> RestoreBackupCommand { get; }
        public IAsyncRelayCommand<string?> DeleteBackupCommand { get; }
        public IAsyncRelayCommand BrowseBackupDirCommand { get; }
        public IRelayCommand ResetBackupDirCommand { get; }
        public IRelayCommand<BackupItemViewModel?> SaveLabelCommand { get; }
        public IRelayCommand<BackupItemViewModel?> VerifyIntegrityCommand { get; }

        public SettingsBackupsVM(
            IDialogService dialogService,
            Action markDirty,
            BackupService? backupService = null,
            Func<bool>? isRunningCheck = null)
        {
            _dialogService = dialogService;
            _markDirty = markDirty;
            _backupService = backupService;
            _isRunningCheck = isRunningCheck;

            CreateBackupCommand = new AsyncRelayCommand(CreateBackupAsync);
            RestoreBackupCommand = new AsyncRelayCommand<string?>(RestoreBackupAsync);
            DeleteBackupCommand = new AsyncRelayCommand<string?>(DeleteBackupAsync);
            BrowseBackupDirCommand = new AsyncRelayCommand(BrowseBackupDirAsync);
            ResetBackupDirCommand = new RelayCommand(() => CustomBackupDirectory = null);
            SaveLabelCommand = new RelayCommand<BackupItemViewModel?>(SaveLabel);
            VerifyIntegrityCommand = new RelayCommand<BackupItemViewModel?>(VerifyIntegrity);
        }

        public void SetInstance(ServerInstance instance)
        {
            _instance = instance;
            UpdateServerDir(instance.Path);
        }

        public void UpdateServerDir(string newDir)
        {
            _serverDir = newDir;
            LoadBackups();
        }

        public void LoadBackups()
        {
            BackupList.Clear();
            HasHealthWarning = false;
            HasHealthCritical = false;
            HealthWarningText = string.Empty;
            TotalBackupsSizeText = "0 KB";
            LastBackupTimeText = "Never";
            ShowDiskWarning = false;

            if (string.IsNullOrEmpty(_serverDir))
            {
                return;
            }

            string defaultBackupDir = Path.Combine(_serverDir, "backups");
            bool usingCustom = !string.IsNullOrWhiteSpace(CustomBackupDirectory) && Directory.Exists(CustomBackupDirectory);
            string backupDir = usingCustom ? CustomBackupDirectory! : defaultBackupDir;

            UpdateDiskSpaceMetrics(Directory.Exists(backupDir) ? backupDir : _serverDir);

            var manifest = BackupManifest.Load(_serverDir);
            if (!string.IsNullOrWhiteSpace(manifest.LastFailureReason))
            {
                HasHealthWarning = true;
                HasHealthCritical = true;
                HealthWarningText = $"Last backup attempt failed: {manifest.LastFailureReason}";
            }

            if (!Directory.Exists(backupDir))
            {
                return;
            }

            var files = Directory.GetFiles(backupDir, "*.zip")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTimeUtc)
                .ToList();

            long totalBytes = 0;
            foreach (FileInfo f in files)
            {
                totalBytes += f.Length;
                var entry = manifest.Entries.FirstOrDefault(e => e.FileName.Equals(f.Name, StringComparison.OrdinalIgnoreCase));
                BackupList.Add(new BackupItemViewModel
                {
                    FileName = f.Name,
                    FilePath = f.FullName,
                    CreatedAtUtc = entry?.CreatedAtUtc ?? f.CreationTimeUtc,
                    SizeBytes = f.Length,
                    SizeDeltaBytes = entry?.SizeDeltaBytes,
                    Version = entry?.Version ?? 0,
                    Trigger = entry?.Trigger.ToString() ?? "Manual",
                    LocationText = usingCustom ? "Custom" : "Local",
                    Label = entry?.Label,
                    ServerType = entry?.ServerType ?? (_instance?.EngineType.ToString() ?? string.Empty),
                    ServerVersion = entry?.MinecraftVersion ?? (_instance?.EngineVersion ?? string.Empty),
                    HasMetadata = entry != null,
                    IntegrityStatusText = entry?.IntegrityVerified == true ? "Checksum recorded" : string.Empty
                });
            }

            TotalBackupsSizeText = totalBytes switch
            {
                >= 1024L * 1024L * 1024L => $"{totalBytes / (1024.0 * 1024.0 * 1024.0):F2} GB",
                >= 1024L * 1024L => $"{totalBytes / (1024.0 * 1024.0):F1} MB",
                _ => $"{totalBytes / 1024.0:F1} KB"
            };

            if (BackupList.Count > 0)
            {
                LastBackupTimeText = BackupList[0].CreatedAtText;
            }
        }

        private void UpdateDiskSpaceMetrics(string probePath)
        {
            try
            {
                string? root = Path.GetPathRoot(Path.GetFullPath(probePath));
                if (!string.IsNullOrWhiteSpace(root))
                {
                    var drive = new DriveInfo(root);
                    if (drive.IsReady)
                    {
                        double freeGb = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                        DiskSpaceText = $"{freeGb:F1} GB free";
                        if (freeGb < 2.0)
                        {
                            ShowDiskWarning = true;
                            if (!HasHealthWarning)
                            {
                                HasHealthWarning = true;
                                HealthWarningText = "Low free disk space detected (< 2 GB).";
                            }
                        }
                        return;
                    }
                }
            }
            catch
            {
            }

            DiskSpaceText = "Available";
        }

        private void SaveLabel(BackupItemViewModel? item)
        {
            if (item == null || string.IsNullOrWhiteSpace(_serverDir)) return;

            var manifest = BackupManifest.Load(_serverDir);
            var entry = manifest.Entries.FirstOrDefault(e => e.FileName.Equals(item.FileName, StringComparison.OrdinalIgnoreCase));
            if (entry != null)
            {
                entry.Label = item.Label;
                manifest.Save(_serverDir);
                LoadBackups();
            }
        }

        private void VerifyIntegrity(BackupItemViewModel? item)
        {
            if (item == null || _instance == null || _backupService == null) return;

            bool? ok = _backupService.VerifyBackupIntegrity(_instance, item.FileName, item.FilePath);
            item.IntegrityStatusText = ok switch
            {
                true => "Integrity verified (SHA-256 matches)",
                false => "Checksum mismatch or corrupted archive",
                null => "No checksum recorded for this archive"
            };
        }

        private async Task CreateBackupAsync()
        {
            if (_instance == null || _backupService == null || IsBackingUp) return;

            IsBackingUp = true;
            IsBackupProgressIndeterminate = true;
            BackupStatusText = "Creating backup...";
            BackupProgressValue = 0;

            try
            {
                var progress = new Progress<double>(val =>
                {
                    IsBackupProgressIndeterminate = false;
                    BackupProgressValue = val;
                });
                await _backupService.CreateBackupAsync(
                    _instance,
                    BackupTrigger.Manual,
                    onProgress: msg => BackupStatusText = msg,
                    progress: progress);

                BackupStatusText = "Backup completed successfully.";
                LoadBackups();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage("Backup Failed", ex.Message, DialogType.Error);
                BackupStatusText = "Backup failed.";
            }
            finally
            {
                IsBackingUp = false;
                IsBackupProgressIndeterminate = false;
            }
        }

        private async Task RestoreBackupAsync(string? targetPath)
        {
            string? path = !string.IsNullOrWhiteSpace(targetPath) ? targetPath : SelectedBackup?.FilePath;
            string name = !string.IsNullOrWhiteSpace(targetPath) ? Path.GetFileName(targetPath) : (SelectedBackup?.FileName ?? string.Empty);

            if (string.IsNullOrWhiteSpace(path) || _instance == null || _backupService == null) return;

            if (_isRunningCheck?.Invoke() == true)
            {
                _dialogService.ShowMessage("Cannot Restore", "Please stop the server before restoring a backup.", DialogType.Warning);
                return;
            }

            var confirm = await _dialogService.ShowDialogAsync(
                "Confirm Restore",
                $"Are you sure you want to restore '{name}'? Current world files will be replaced.",
                DialogType.Warning,
                showCancel: true);

            if (confirm != DialogResult.Ok && confirm != DialogResult.Yes) return;

            IsBackingUp = true;
            BackupStatusText = "Restoring backup...";

            try
            {
                await _backupService.RestoreBackupAsync(
                    _instance,
                    path,
                    msg => BackupStatusText = msg);

                _dialogService.ShowMessage("Restore Completed", "Backup restored successfully.", DialogType.Information);
                BackupStatusText = "Restore completed.";
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage("Restore Failed", ex.Message, DialogType.Error);
                BackupStatusText = "Restore failed.";
            }
            finally
            {
                IsBackingUp = false;
            }
        }

        private async Task DeleteBackupAsync(string? targetPath)
        {
            string? path = !string.IsNullOrWhiteSpace(targetPath) ? targetPath : SelectedBackup?.FilePath;
            if (string.IsNullOrWhiteSpace(path)) return;

            string fileName = Path.GetFileName(path);
            var confirm = await _dialogService.ShowDialogAsync(
                "Delete Backup",
                $"Delete backup '{fileName}'?",
                DialogType.Question,
                showCancel: true);

            if (confirm != DialogResult.Ok && confirm != DialogResult.Yes) return;

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                LoadBackups();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage("Delete Failed", ex.Message, DialogType.Error);
            }
        }

        private async Task BrowseBackupDirAsync()
        {
            var dir = await _dialogService.OpenFolderDialogAsync("Select Custom Backup Directory");
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                CustomBackupDirectory = dir;
            }
        }

        partial void OnBackupIntervalHoursChanged(int value) => _markDirty();
        partial void OnMaxBackupsToKeepChanged(int value) => _markDirty();
        partial void OnAutomaticBackupPruningEnabledChanged(bool value) => _markDirty();
        partial void OnCustomBackupDirectoryChanged(string? value)
        {
            _markDirty();
            LoadBackups();
        }
    }
}
