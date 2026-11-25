using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.App.ViewModels.Settings;

public partial class CloudProviderViewModel : ObservableObject
{
    private readonly ICloudBackupProvider _provider;
    private readonly IDialogService? _dialogService;

    public CloudBackupProviderType ProviderType => _provider.ProviderType;

    public string DisplayName => ProviderType switch
    {
        CloudBackupProviderType.GoogleDrive => "Google Drive",
        CloudBackupProviderType.OneDrive => "OneDrive",
        CloudBackupProviderType.Dropbox => "Dropbox",
        _ => ProviderType.ToString()
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnected))]
    [NotifyPropertyChangedFor(nameof(StatusDotBrush))]
    private CloudBackupConnectionStatus _status;

    [ObservableProperty]
    private string _accountInfo = "Not connected";

    [ObservableProperty]
    private bool _isBusy;

    public bool IsConnected => Status == CloudBackupConnectionStatus.Connected;
    public Avalonia.Media.IBrush StatusDotBrush => Status switch
    {
        CloudBackupConnectionStatus.Connected => Avalonia.Media.Brushes.MediumSeaGreen,
        CloudBackupConnectionStatus.Expired => Avalonia.Media.Brushes.Goldenrod,
        CloudBackupConnectionStatus.Error => Avalonia.Media.Brushes.IndianRed,
        _ => Avalonia.Media.Brushes.Gray
    };

    public IAsyncRelayCommand ConnectCommand { get; }
    public IAsyncRelayCommand DisconnectCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }

    public CloudProviderViewModel(ICloudBackupProvider provider, IDialogService? dialogService = null)
    {
        _provider = provider;
        _dialogService = dialogService;
        ConnectCommand = new AsyncRelayCommand(ConnectAsync);
        DisconnectCommand = new AsyncRelayCommand(DisconnectAsync);
        RefreshCommand = new AsyncRelayCommand(RefreshStatusAsync);
    }

    public async Task RefreshStatusAsync()
    {
        IsBusy = true;
        try
        {
            var account = await _provider.GetAccountAsync(CancellationToken.None);
            if (account != null && account.Status == CloudBackupConnectionStatus.Connected)
            {
                Status = CloudBackupConnectionStatus.Connected;
                AccountInfo = $"Connected as {account.Email ?? account.DisplayName}";
            }
            else
            {
                Status = account?.Status ?? await _provider.GetStatusAsync(CancellationToken.None);
                AccountInfo = Status == CloudBackupConnectionStatus.Expired
                    ? "Session expired. Please reconnect."
                    : "Not connected";
            }
        }
        catch
        {
            Status = CloudBackupConnectionStatus.Error;
            AccountInfo = "Could not check account status";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ConnectAsync()
    {
        IsBusy = true;
        try
        {
            await _provider.ConnectAsync(CancellationToken.None);
            await RefreshStatusAsync();
            _dialogService?.ShowMessage("Connected", $"Successfully connected to {DisplayName}.", DialogType.Information);
        }
        catch (Exception ex)
        {
            _dialogService?.ShowMessage("Connection Failed", $"Failed to connect to {DisplayName}: {ex.Message}", DialogType.Error);
            await RefreshStatusAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DisconnectAsync()
    {
        IsBusy = true;
        try
        {
            await _provider.DisconnectAsync(CancellationToken.None);
            await RefreshStatusAsync();
        }
        catch (Exception ex)
        {
            _dialogService?.ShowMessage("Disconnect Failed", ex.Message, DialogType.Error);
            await RefreshStatusAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public partial class CloudBackupSettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IEnumerable<ICloudBackupProvider> _providers;
    private readonly IDialogService? _dialogService;

    public ObservableCollection<CloudProviderViewModel> ProviderViewModels { get; } = new();

    [ObservableProperty] private bool _enableCloudBackups;
    [ObservableProperty] private bool _uploadOnManualBackup;
    [ObservableProperty] private bool _uploadOnScheduledBackup;

    partial void OnEnableCloudBackupsChanged(bool value) => Persist();
    partial void OnUploadOnManualBackupChanged(bool value) => Persist();
    partial void OnUploadOnScheduledBackupChanged(bool value) => Persist();

    public CloudBackupSettingsViewModel(
        ISettingsService settingsService,
        IEnumerable<ICloudBackupProvider> providers,
        IDialogService? dialogService = null)
    {
        _settingsService = settingsService;
        _providers = providers;
        _dialogService = dialogService;

        var cfg = _settingsService.Settings.CloudBackups;
        _enableCloudBackups = cfg.EnableCloudBackups;
        _uploadOnManualBackup = cfg.UploadOnManualBackup;
        _uploadOnScheduledBackup = cfg.UploadOnScheduledBackup;

        foreach (var provider in _providers)
        {
            var vm = new CloudProviderViewModel(provider, _dialogService);
            ProviderViewModels.Add(vm);
            _ = vm.RefreshStatusAsync();
        }
    }

    private void Persist()
    {
        var cfg = _settingsService.Settings.CloudBackups;
        cfg.EnableCloudBackups = EnableCloudBackups;
        cfg.UploadOnManualBackup = UploadOnManualBackup;
        cfg.UploadOnScheduledBackup = UploadOnScheduledBackup;
        _settingsService.Save();
    }
}
