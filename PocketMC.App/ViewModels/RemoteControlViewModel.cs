using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.RemoteControl.Services;
using QRCoder;

using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace PocketMC.App.ViewModels;

public partial class RemoteControlViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly RemoteAuthenticationService _authService;
    private readonly LocalNetworkAddressService _networkAddressService;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private int _port;

    [ObservableProperty]
    private bool _requireAuthentication;

    [ObservableProperty]
    private string _password;

    [ObservableProperty]
    private bool _allowRemoteConsoleCommands;

    [ObservableProperty]
    private bool _allowRemotePlayerActions;

    [ObservableProperty]
    private Bitmap? _pairingQrCode;

    [ObservableProperty]
    private string _pairingUrl = "";

    [ObservableProperty]
    private bool _isDownloadingUpdate;
    
    [ObservableProperty]
    private double _updateDownloadProgress;

    [ObservableProperty]
    private bool _isUpdateReady;

    private readonly PocketMC.RemoteControl.Hosting.RemoteDashboardHost _dashboardHost;

    public RemoteControlViewModel(
        ISettingsService settingsService,
        RemoteAuthenticationService authService,
        LocalNetworkAddressService networkAddressService,
        PocketMC.RemoteControl.Hosting.RemoteDashboardHost dashboardHost)
    {
        _settingsService = settingsService;
        _authService = authService;
        _networkAddressService = networkAddressService;
        _dashboardHost = dashboardHost;
        Password = "";

        LoadSettings();
        GenerateQrCode();
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Settings.RemoteControl;
        IsEnabled = settings.Enabled;
        Port = settings.Port == 0 ? 8080 : settings.Port;
        RequireAuthentication = settings.RequireAuthentication;
        AllowRemoteConsoleCommands = settings.AllowRemoteConsoleCommands;
        AllowRemotePlayerActions = settings.AllowRemotePlayerActions;
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        var settings = _settingsService.Settings.RemoteControl;
        settings.Enabled = IsEnabled;
        settings.Port = Port;
        settings.RequireAuthentication = RequireAuthentication;
        settings.AllowRemoteConsoleCommands = AllowRemoteConsoleCommands;
        settings.AllowRemotePlayerActions = AllowRemotePlayerActions;

        if (!string.IsNullOrWhiteSpace(Password))
        {
            settings.PasswordHash = _authService.HashPassword(Password);
        }

        _settingsService.Save();
        GenerateQrCode();

        try
        {
            if (IsEnabled)
            {
                await _dashboardHost.StartServerAsync(Port);
            }
            else
            {
                await _dashboardHost.StopAsync(CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RemoteControl] Failed to toggle server state: {ex.Message}");
        }
    }


    
    [RelayCommand]
    private void RestartApp()
    {
        // Handle app restart logic
    }

    [RelayCommand]
    private async Task CopyUrlAsync()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow?.Clipboard != null)
            {
                await desktop.MainWindow.Clipboard.SetTextAsync(PairingUrl);
            }
        }
    }

    [RelayCommand]
    private void NavigateBack()
    {
        var mainVM = App.Services.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
        if (mainVM != null)
        {
            mainVM.CurrentViewModel = App.Services.GetRequiredService<DashboardViewModel>();
        }
    }

    private void GenerateQrCode()
    {
        var ip = _networkAddressService.GetLocalIPv4Addresses().FirstOrDefault() ?? "localhost";
        PairingUrl = $"http://{ip}:{Port}/";

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(PairingUrl, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeImage = qrCode.GetGraphic(20);

        using var stream = new MemoryStream(qrCodeImage);
        PairingQrCode = new Bitmap(stream);
    }
}
