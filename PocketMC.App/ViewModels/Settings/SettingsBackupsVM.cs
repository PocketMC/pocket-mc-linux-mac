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
    public class BackupItemViewModel : ObservableObject
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public long SizeBytes { get; set; }
        public string FormattedSize => SizeBytes > 1024 * 1024 
            ? $"{(SizeBytes / (1024.0 * 1024.0)):F1} MB" 
            : $"{(SizeBytes / 1024.0):F1} KB";
        public string FormattedDate => CreatedAtUtc.ToLocalTime().ToString("g");
        public string Trigger { get; set; } = "Manual";
        public string? Label { get; set; }
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
        [ObservableProperty] private string _backupStatusText = string.Empty;
        [ObservableProperty] private BackupItemViewModel? _selectedBackup;

        public ObservableCollection<BackupItemViewModel> BackupList { get; } = new();

        public IAsyncRelayCommand CreateBackupCommand { get; }
        public IAsyncRelayCommand RestoreBackupCommand { get; }
        public IAsyncRelayCommand DeleteBackupCommand { get; }
        public IAsyncRelayCommand BrowseBackupDirCommand { get; }

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
            RestoreBackupCommand = new AsyncRelayCommand(RestoreBackupAsync);
            DeleteBackupCommand = new AsyncRelayCommand(DeleteBackupAsync);
            BrowseBackupDirCommand = new AsyncRelayCommand(BrowseBackupDirAsync);
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
            if (string.IsNullOrEmpty(_serverDir)) return;

            string backupDir = !string.IsNullOrEmpty(CustomBackupDirectory) && Directory.Exists(CustomBackupDirectory)
                ? CustomBackupDirectory
                : Path.Combine(_serverDir, "backups");

            if (!Directory.Exists(backupDir)) return;

            var manifest = BackupManifest.Load(_serverDir);
            var files = Directory.GetFiles(backupDir, "*.zip")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTimeUtc);

            foreach (var f in files)
            {
                var entry = manifest.Entries.FirstOrDefault(e => e.FileName.Equals(f.Name, StringComparison.OrdinalIgnoreCase));
                BackupList.Add(new BackupItemViewModel
                {
                    FileName = f.Name,
                    FilePath = f.FullName,
                    CreatedAtUtc = entry?.CreatedAtUtc ?? f.CreationTimeUtc,
                    SizeBytes = f.Length,
                    Trigger = entry?.Trigger.ToString() ?? "Manual",
                    Label = entry?.Label
                });
            }
        }

        private async Task CreateBackupAsync()
        {
            if (_instance == null || _backupService == null || IsBackingUp) return;

            IsBackingUp = true;
            BackupStatusText = "Creating backup...";
            BackupProgressValue = 0;

            try
            {
                var progress = new Progress<double>(val => BackupProgressValue = val);
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
            }
        }

        private async Task RestoreBackupAsync()
        {
            if (SelectedBackup == null || _instance == null || _backupService == null) return;

            if (_isRunningCheck?.Invoke() == true)
            {
                _dialogService.ShowMessage("Cannot Restore", "Please stop the server before restoring a backup.", DialogType.Warning);
                return;
            }

            var confirm = await _dialogService.ShowDialogAsync(
                "Confirm Restore",
                $"Are you sure you want to restore '{SelectedBackup.FileName}'? Current world files will be replaced.",
                DialogType.Warning,
                showCancel: true);

            if (confirm != DialogResult.Ok && confirm != DialogResult.Yes) return;

            IsBackingUp = true;
            BackupStatusText = "Restoring backup...";

            try
            {
                await _backupService.RestoreBackupAsync(
                    _instance,
                    SelectedBackup.FilePath,
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

        private async Task DeleteBackupAsync()
        {
            if (SelectedBackup == null) return;

            var confirm = await _dialogService.ShowDialogAsync(
                "Delete Backup",
                $"Delete backup '{SelectedBackup.FileName}'?",
                DialogType.Question,
                showCancel: true);

            if (confirm != DialogResult.Ok && confirm != DialogResult.Yes) return;

            try
            {
                if (File.Exists(SelectedBackup.FilePath))
                {
                    File.Delete(SelectedBackup.FilePath);
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
