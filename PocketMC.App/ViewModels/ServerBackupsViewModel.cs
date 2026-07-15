using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;

namespace PocketMC.App.ViewModels
{
    public partial class ServerBackupsViewModel : ObservableObject
    {
        private readonly IBackupService _backupService;
        private readonly ISettingsService _settingsService;
        private readonly IInstanceService _instanceService;
        private readonly IEnumerable<ICloudBackupProvider> _cloudProviders;

        [ObservableProperty]
        private ServerInstance _instance;

        [ObservableProperty]
        private ObservableCollection<BackupMetadataEntry> _backups = new();

        [ObservableProperty]
        private BackupMetadataEntry? _selectedBackup;

        // Settings properties
        [ObservableProperty]
        private int _maxBackupsToKeep;

        [ObservableProperty]
        private string _customBackupDirectory = string.Empty;

        [ObservableProperty]
        private string _externalBackupDirectory = string.Empty;

        // Cloud settings
        [ObservableProperty]
        private bool _enableCloudBackups;

        [ObservableProperty]
        private bool _uploadOnManualBackup;

        [ObservableProperty]
        private bool _uploadOnScheduledBackup;

        [ObservableProperty]
        private bool _isGoogleDriveEnabled;

        [ObservableProperty]
        private bool _isOneDriveEnabled;

        [ObservableProperty]
        private bool _isDropboxEnabled;

        [ObservableProperty]
        private int _googleDriveRetention;

        [ObservableProperty]
        private int _oneDriveRetention;

        [ObservableProperty]
        private int _dropboxRetention;

        // Connection statuses
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsGoogleDriveConnected))]
        [NotifyPropertyChangedFor(nameof(CanConnectGoogleDrive))]
        private string _googleDriveStatus = "Disconnected";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsOneDriveConnected))]
        [NotifyPropertyChangedFor(nameof(CanConnectOneDrive))]
        private string _oneDriveStatus = "Disconnected";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDropboxConnected))]
        [NotifyPropertyChangedFor(nameof(CanConnectDropbox))]
        private string _dropboxStatus = "Disconnected";

        public bool IsGoogleDriveConnected => GoogleDriveStatus == "Connected";
        public bool CanConnectGoogleDrive => !IsGoogleDriveConnected;

        public bool IsOneDriveConnected => OneDriveStatus == "Connected";
        public bool CanConnectOneDrive => !IsOneDriveConnected;

        public bool IsDropboxConnected => DropboxStatus == "Connected";
        public bool CanConnectDropbox => !IsDropboxConnected;

        // UI state
        [ObservableProperty]
        private string _statusText = "Ready";

        [ObservableProperty]
        private double _progressPercent;

        [ObservableProperty]
        private bool _isBusy;

        public ServerBackupsViewModel(
            IBackupService backupService,
            ISettingsService settingsService,
            IInstanceService instanceService,
            IEnumerable<ICloudBackupProvider> cloudProviders)
        {
            _backupService = backupService;
            _settingsService = settingsService;
            _instanceService = instanceService;
            _cloudProviders = cloudProviders;

            // Default fallback
            _instance = new ServerInstance();
        }

        public void Initialize(ServerInstance instance)
        {
            Instance = instance;
            MaxBackupsToKeep = instance.MaxBackupsToKeep;
            CustomBackupDirectory = instance.CustomBackupDirectory ?? string.Empty;

            var settings = _settingsService.Settings;
            ExternalBackupDirectory = settings.ExternalBackupDirectory ?? string.Empty;

            // Cloud settings
            EnableCloudBackups = settings.CloudBackups.EnableCloudBackups;
            UploadOnManualBackup = settings.CloudBackups.UploadOnManualBackup;
            UploadOnScheduledBackup = settings.CloudBackups.UploadOnScheduledBackup;

            var gdTarget = settings.CloudBackups.Targets.FirstOrDefault(t => t.Provider == CloudBackupProviderType.GoogleDrive);
            IsGoogleDriveEnabled = gdTarget?.Enabled ?? false;
            GoogleDriveRetention = gdTarget?.RetentionCount ?? 5;

            var odTarget = settings.CloudBackups.Targets.FirstOrDefault(t => t.Provider == CloudBackupProviderType.OneDrive);
            IsOneDriveEnabled = odTarget?.Enabled ?? false;
            OneDriveRetention = odTarget?.RetentionCount ?? 5;

            var dbTarget = settings.CloudBackups.Targets.FirstOrDefault(t => t.Provider == CloudBackupProviderType.Dropbox);
            IsDropboxEnabled = dbTarget?.Enabled ?? false;
            DropboxRetention = dbTarget?.RetentionCount ?? 5;

            _ = RefreshBackupsListAsync();
            _ = CheckCloudConnectionsAsync();
        }

        private async Task RefreshBackupsListAsync()
        {
            try
            {
                var manifest = BackupManifest.Load(Instance.Path);
                string defaultBackupDir = Path.Combine(Instance.Path, "backups");
                manifest.PurgeOrphanedEntries(defaultBackupDir, Instance.CustomBackupDirectory);
                manifest.Save(Instance.Path);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Backups.Clear();
                    foreach (var entry in manifest.Entries.OrderByDescending(e => e.CreatedAtUtc))
                    {
                        Backups.Add(entry);
                    }
                });
            }
            catch (Exception ex)
            {
                StatusText = $"Failed to load backups list: {ex.Message}";
            }
        }

        private async Task CheckCloudConnectionsAsync()
        {
            foreach (var provider in _cloudProviders)
            {
                try
                {
                    var status = await provider.GetStatusAsync(CancellationToken.None);
                    var statusStr = status.ToString();
                    
                    if (provider.ProviderType == CloudBackupProviderType.GoogleDrive)
                        GoogleDriveStatus = statusStr;
                    else if (provider.ProviderType == CloudBackupProviderType.OneDrive)
                        OneDriveStatus = statusStr;
                    else if (provider.ProviderType == CloudBackupProviderType.Dropbox)
                        DropboxStatus = statusStr;
                }
                catch
                {
                    // Ignore errors during checking status
                }
            }
        }

        [RelayCommand]
        private void NavigateBack()
        {
            var mainVM = App.Services.GetService<MainWindowViewModel>();
            if (mainVM != null)
            {
                mainVM.CurrentViewModel = App.Services.GetRequiredService<DashboardViewModel>();
            }
        }

        [RelayCommand]
        private async Task CreateBackupAsync()
        {
            IsBusy = true;
            ProgressPercent = 0;
            StatusText = "Initializing backup process...";

            try
            {
                var progress = new Progress<double>(p => ProgressPercent = p);
                
                await Task.Run(async () =>
                {
                    await _backupService.CreateBackupAsync(
                        Instance,
                        BackupTrigger.Manual,
                        label: $"Manual Backup {DateTime.Now:yyyy-MM-dd HH:mm}",
                        notes: "Created via PocketMC Dashboard",
                        onProgress: msg => Dispatcher.UIThread.Post(() => StatusText = msg),
                        progress: progress);
                });

                StatusText = "Backup completed successfully!";
                await RefreshBackupsListAsync();
            }
            catch (Exception ex)
            {
                StatusText = $"Backup failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RestoreBackupAsync()
        {
            if (SelectedBackup == null)
            {
                StatusText = "Please select a backup to restore.";
                return;
            }

            IsBusy = true;
            ProgressPercent = 0;
            StatusText = "Initializing restore process...";

            try
            {
                string backupDir = string.IsNullOrWhiteSpace(Instance.CustomBackupDirectory)
                    ? Path.Combine(Instance.Path, "backups")
                    : Instance.CustomBackupDirectory;
                string zipPath = Path.Combine(backupDir, SelectedBackup.FileName);

                var progress = new Progress<double>(p => ProgressPercent = p);

                await Task.Run(async () =>
                {
                    await _backupService.RestoreBackupAsync(
                        Instance,
                        zipPath,
                        onProgress: msg => Dispatcher.UIThread.Post(() => StatusText = msg),
                        progress: progress);
                });

                StatusText = "Restore completed successfully!";
            }
            catch (Exception ex)
            {
                StatusText = $"Restore failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DeleteBackupAsync()
        {
            if (SelectedBackup == null)
            {
                StatusText = "Please select a backup to delete.";
                return;
            }

            try
            {
                string backupDir = string.IsNullOrWhiteSpace(Instance.CustomBackupDirectory)
                    ? Path.Combine(Instance.Path, "backups")
                    : Instance.CustomBackupDirectory;
                string zipPath = Path.Combine(backupDir, SelectedBackup.FileName);

                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }

                var manifest = BackupManifest.Load(Instance.Path);
                manifest.Entries.RemoveAll(e => e.FileName == SelectedBackup.FileName);
                manifest.Save(Instance.Path);

                StatusText = "Backup deleted successfully.";
                await RefreshBackupsListAsync();
            }
            catch (Exception ex)
            {
                StatusText = $"Failed to delete backup: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task VerifyIntegrityAsync()
        {
            if (SelectedBackup == null)
            {
                StatusText = "Please select a backup to verify.";
                return;
            }

            IsBusy = true;
            StatusText = "Verifying backup integrity (calculating checksum)...";

            try
            {
                bool? match = await Task.Run(() => 
                    _backupService.VerifyBackupIntegrity(Instance, SelectedBackup.FileName));

                if (match == true)
                {
                    StatusText = "Integrity check PASSED! Checksum matches manifest.";
                }
                else if (match == false)
                {
                    StatusText = "Integrity check FAILED! Zip file is corrupted or modified.";
                }
                else
                {
                    StatusText = "No checksum available in manifest to verify against.";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Verification failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SaveSettingsAsync()
        {
            try
            {
                // Save instance settings
                Instance.MaxBackupsToKeep = MaxBackupsToKeep;
                Instance.CustomBackupDirectory = string.IsNullOrWhiteSpace(CustomBackupDirectory) ? null : CustomBackupDirectory;
                
                // Save settings in DB / file
                var list = await _instanceService.ListInstancesAsync();
                var matching = list.FirstOrDefault(i => i.Id == Instance.Id);
                if (matching != null)
                {
                    matching.MaxBackupsToKeep = MaxBackupsToKeep;
                    matching.CustomBackupDirectory = Instance.CustomBackupDirectory;
                }

                // Save app settings
                var settings = _settingsService.Settings;
                settings.ExternalBackupDirectory = string.IsNullOrWhiteSpace(ExternalBackupDirectory) ? null : ExternalBackupDirectory;

                settings.CloudBackups.EnableCloudBackups = EnableCloudBackups;
                settings.CloudBackups.UploadOnManualBackup = UploadOnManualBackup;
                settings.CloudBackups.UploadOnScheduledBackup = UploadOnScheduledBackup;

                // Update targets
                UpdateTarget(settings, CloudBackupProviderType.GoogleDrive, IsGoogleDriveEnabled, GoogleDriveRetention);
                UpdateTarget(settings, CloudBackupProviderType.OneDrive, IsOneDriveEnabled, OneDriveRetention);
                UpdateTarget(settings, CloudBackupProviderType.Dropbox, IsDropboxEnabled, DropboxRetention);

                _settingsService.Save();
                StatusText = "Backup settings saved successfully!";
            }
            catch (Exception ex)
            {
                StatusText = $"Failed to save settings: {ex.Message}";
            }
        }

        private void UpdateTarget(Settings settings, CloudBackupProviderType provider, bool enabled, int retention)
        {
            var target = settings.CloudBackups.Targets.FirstOrDefault(t => t.Provider == provider);
            if (target == null)
            {
                target = new CloudBackupTarget { Provider = provider };
                settings.CloudBackups.Targets.Add(target);
            }
            target.Enabled = enabled;
            target.RetentionCount = retention;
        }

        [RelayCommand]
        private async Task ConnectCloudProvider(string providerName)
        {
            var provider = _cloudProviders.FirstOrDefault(p => p.ProviderType.ToString().Equals(providerName, StringComparison.OrdinalIgnoreCase));
            if (provider == null) return;

            IsBusy = true;
            StatusText = $"Connecting to {providerName}... Please approve in your browser.";

            try
            {
                await provider.ConnectAsync(CancellationToken.None);
                StatusText = $"Successfully connected to {providerName}!";
                await CheckCloudConnectionsAsync();
            }
            catch (Exception ex)
            {
                StatusText = $"Connection failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DisconnectCloudProvider(string providerName)
        {
            var provider = _cloudProviders.FirstOrDefault(p => p.ProviderType.ToString().Equals(providerName, StringComparison.OrdinalIgnoreCase));
            if (provider == null) return;

            IsBusy = true;
            StatusText = $"Disconnecting from {providerName}...";

            try
            {
                await provider.DisconnectAsync(CancellationToken.None);
                StatusText = $"Disconnected from {providerName}.";
                await CheckCloudConnectionsAsync();
            }
            catch (Exception ex)
            {
                StatusText = $"Failed to disconnect: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
