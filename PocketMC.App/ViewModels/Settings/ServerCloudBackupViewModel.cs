using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;

namespace PocketMC.App.ViewModels.Settings
{
    public sealed class ServerCloudBackupTargetViewModel : ObservableObject
    {
        private readonly CloudBackupTarget _target;
        private readonly Action _onChanged;

        public CloudBackupProviderType ProviderType => _target.Provider;

        public bool IsEnabled
        {
            get => _target.Enabled;
            set
            {
                if (_target.Enabled != value)
                {
                    _target.Enabled = value;
                    OnPropertyChanged();
                    _onChanged();
                }
            }
        }

        public int RetentionCount
        {
            get => _target.RetentionCount ?? 5;
            set
            {
                if (_target.RetentionCount != value)
                {
                    _target.RetentionCount = Math.Max(1, value);
                    OnPropertyChanged();
                    _onChanged();
                }
            }
        }

        public bool AutomaticPruningEnabled
        {
            get => _target.AutomaticPruningEnabled && _target.RetentionCount is > 0;
            set
            {
                if (_target.AutomaticPruningEnabled != value)
                {
                    _target.AutomaticPruningEnabled = value;
                    if (value && _target.RetentionCount is not > 0)
                    {
                        _target.RetentionCount = 5;
                    }
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RetentionCount));
                    _onChanged();
                }
            }
        }

        public ServerCloudBackupTargetViewModel(CloudBackupTarget target, Action onChanged)
        {
            _target = target;
            _onChanged = onChanged;
        }
    }

    public sealed class RemoteBackupItemViewModel : ObservableObject
    {
        public CloudBackupProviderType Provider { get; set; }
        public string FileName { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public DateTimeOffset CreatedUtc { get; set; }
        public string ProviderFileId { get; set; } = string.Empty;

        public string SizeText => SizeBytes >= 1024 * 1024
            ? $"{SizeBytes / (1024.0 * 1024.0):F1} MB"
            : $"{SizeBytes / 1024.0:F1} KB";
        public string DateText => CreatedUtc.ToLocalTime().ToString("g");
    }

    public partial class ServerCloudBackupViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly IReadOnlyList<ICloudBackupProvider> _providers;
        private readonly IDialogService _dialogService;
        private readonly BackupService? _backupService;
        private ServerInstance? _instance;
        private Func<bool>? _isRunningCheck;

        public ObservableCollection<ServerCloudBackupTargetViewModel> Targets { get; } = new();
        public ObservableCollection<RemoteBackupItemViewModel> RemoteBackups { get; } = new();

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowNoConnectedProvidersMessage))]
        private bool _isRefreshingProviders;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowNoConnectedProvidersMessage))]
        private bool _isCloudBackupsEnabled;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowNoConnectedProvidersMessage))]
        private bool _hasConnectedProviders;
        [ObservableProperty] private string _providerStatusMessage = "No connected cloud providers. Connect Google Drive, Dropbox, or OneDrive in App Settings.";

        public bool ShowNoConnectedProvidersMessage => IsCloudBackupsEnabled && !IsRefreshingProviders && !HasConnectedProviders;

        public IAsyncRelayCommand RefreshRemoteBackupsCommand { get; }
        public IAsyncRelayCommand<RemoteBackupItemViewModel?> DeleteRemoteBackupCommand { get; }
        public IAsyncRelayCommand<RemoteBackupItemViewModel?> RestoreRemoteBackupCommand { get; }

        public ServerCloudBackupViewModel(
            ISettingsService settingsService,
            IEnumerable<ICloudBackupProvider> providers,
            IDialogService dialogService,
            BackupService? backupService = null,
            Func<bool>? isRunningCheck = null)
        {
            _settingsService = settingsService;
            _providers = providers.ToList();
            _dialogService = dialogService;
            _backupService = backupService;
            _isRunningCheck = isRunningCheck;

            RefreshRemoteBackupsCommand = new AsyncRelayCommand(RefreshAsync);
            DeleteRemoteBackupCommand = new AsyncRelayCommand<RemoteBackupItemViewModel?>(DeleteRemoteBackupAsync);
            RestoreRemoteBackupCommand = new AsyncRelayCommand<RemoteBackupItemViewModel?>(RestoreRemoteBackupAsync);
        }

        public void SetInstance(ServerInstance instance, Func<bool>? isRunningCheck = null)
        {
            _instance = instance;
            if (isRunningCheck != null)
            {
                _isRunningCheck = isRunningCheck;
            }
            _ = RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            _settingsService.Load();
            var settings = _settingsService.Settings;
            IsCloudBackupsEnabled = settings.CloudBackups.EnableCloudBackups;
            Targets.Clear();
            RemoteBackups.Clear();
            HasConnectedProviders = false;

            if (!IsCloudBackupsEnabled)
            {
                ProviderStatusMessage = "Cloud backups are disabled in App Settings.";
                return;
            }

            IsRefreshingProviders = true;
            try
            {
                foreach (ICloudBackupProvider provider in _providers)
                {
                    try
                    {
                        var status = await provider.GetStatusAsync(CancellationToken.None);
                        if (status == CloudBackupConnectionStatus.Connected)
                        {
                            var target = settings.CloudBackups.Targets.FirstOrDefault(t => t.Provider == provider.ProviderType);
                            if (target == null)
                            {
                                target = new CloudBackupTarget { Provider = provider.ProviderType, Enabled = false };
                                settings.CloudBackups.Targets.Add(target);
                            }

                            Targets.Add(new ServerCloudBackupTargetViewModel(target, () => _settingsService.Save()));
                        }
                    }
                    catch
                    {
                    }
                }

                HasConnectedProviders = Targets.Count > 0;
                if (!HasConnectedProviders)
                {
                    ProviderStatusMessage = "No connected cloud providers. Connect Google Drive, Dropbox, or OneDrive in App Settings.";
                    return;
                }

                await LoadRemoteBackupsAsync();
            }
            finally
            {
                IsRefreshingProviders = false;
            }
        }

        private async Task LoadRemoteBackupsAsync()
        {
            if (_instance == null) return;

            IsLoading = true;
            RemoteBackups.Clear();
            try
            {
                foreach (ServerCloudBackupTargetViewModel target in Targets)
                {
                    ICloudBackupProvider? provider = _providers.FirstOrDefault(p => p.ProviderType == target.ProviderType);
                    if (provider == null) continue;

                    try
                    {
                        var items = await provider.ListBackupsAsync(_instance.Id, _instance.Name, CancellationToken.None);
                        foreach (CloudRemoteBackupItem item in items)
                        {
                            RemoteBackups.Add(new RemoteBackupItemViewModel
                            {
                                Provider = item.Provider,
                                FileName = item.FileName,
                                SizeBytes = item.SizeBytes,
                                CreatedUtc = item.CreatedUtc,
                                ProviderFileId = item.ProviderFileId
                            });
                        }
                    }
                    catch
                    {
                    }
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task DeleteRemoteBackupAsync(RemoteBackupItemViewModel? item)
        {
            if (item == null) return;
            ICloudBackupProvider? provider = _providers.FirstOrDefault(p => p.ProviderType == item.Provider);
            if (provider == null) return;

            var confirm = await _dialogService.ShowDialogAsync(
                "Delete Cloud Backup",
                $"Delete remote archive '{item.FileName}' from {item.Provider}?",
                DialogType.Question,
                showCancel: true);

            if (confirm != DialogResult.Ok && confirm != DialogResult.Yes) return;

            await provider.DeleteBackupAsync(item.ProviderFileId, CancellationToken.None);
            RemoteBackups.Remove(item);
        }

        private async Task RestoreRemoteBackupAsync(RemoteBackupItemViewModel? item)
        {
            if (item == null || _instance == null || _backupService == null) return;

            if (_isRunningCheck?.Invoke() == true)
            {
                _dialogService.ShowMessage("Cannot Restore", "Stop the server before restoring a remote cloud backup.", DialogType.Warning);
                return;
            }

            ICloudBackupProvider? provider = _providers.FirstOrDefault(p => p.ProviderType == item.Provider);
            if (provider == null) return;

            string tempZip = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"pocketmc-restore-{Guid.NewGuid():N}.zip");
            try
            {
                await _dialogService.ShowProgressDialogAsync("Restoring Cloud Backup", $"Downloading {item.FileName}...", async progress =>
                {
                    await provider.DownloadBackupAsync(item.ProviderFileId, tempZip, CancellationToken.None, new Progress<double>(p => progress.Report(p)));
                    await _backupService.RestoreBackupAsync(_instance, tempZip);
                });

                _dialogService.ShowMessage("Restore Completed", "Remote cloud backup restored successfully.", DialogType.Information);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage("Restore Failed", ex.Message, DialogType.Error);
            }
            finally
            {
                if (System.IO.File.Exists(tempZip))
                {
                    try { System.IO.File.Delete(tempZip); }
                    catch { }
                }
            }
        }
    }
}
