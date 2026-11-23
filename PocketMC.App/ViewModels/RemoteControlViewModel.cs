using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Configuration;
using PocketMC.RemoteControl.Hosting;
using PocketMC.RemoteControl.Services;
using QRCoder;

namespace PocketMC.App.ViewModels;

public partial class RemoteControlViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly RemoteAuthenticationService _authService;
    private readonly LocalNetworkAddressService _networkAddressService;
    private readonly RemoteDashboardHost? _dashboardHost;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGeneralTab))]
    [NotifyPropertyChangedFor(nameof(IsUsersTab))]
    private int _selectedTab;

    public bool IsGeneralTab
    {
        get => SelectedTab == 0;
        set { if (value) SelectedTab = 0; }
    }

    public bool IsUsersTab
    {
        get => SelectedTab == 1;
        set { if (value) SelectedTab = 1; }
    }

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private int _port;

    [ObservableProperty]
    private RemoteAccessMode _accessMode;

    [ObservableProperty]
    private bool _requireAuthentication;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _allowRemoteConsoleCommands;

    [ObservableProperty]
    private bool _allowRemotePlayerActions;

    [ObservableProperty]
    private string _localUrl = string.Empty;

    public bool HasLocalUrl => !string.IsNullOrWhiteSpace(LocalUrl);

    [ObservableProperty]
    private bool _isLocalQrVisible;

    [ObservableProperty]
    private Bitmap? _localQrImage;

    [ObservableProperty]
    private string _publicUrl = string.Empty;

    public bool HasPublicUrl => !string.IsNullOrWhiteSpace(PublicUrl);

    [ObservableProperty]
    private bool _isLoadingPublicUrl;

    [ObservableProperty]
    private bool _hasPublicUrlError;

    [ObservableProperty]
    private string _publicUrlErrorText = string.Empty;

    [ObservableProperty]
    private bool _isPublicQrVisible;

    [ObservableProperty]
    private Bitmap? _publicQrImage;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public bool HasStatusText => !string.IsNullOrWhiteSpace(StatusText);

    [ObservableProperty]
    private bool _isStatusError;

    [ObservableProperty]
    private bool _isDiscordLinked;

    [ObservableProperty]
    private bool _enableDiscordNotifications;

    [ObservableProperty]
    private Bitmap? _pairingQrCode;

    [ObservableProperty]
    private string _pairingUrl = string.Empty;

    public ObservableCollection<RemoteControlUserViewModel> Users { get; } = new();

    [ObservableProperty]
    private RemoteControlUserViewModel? _selectedUser;

    public IRelayCommand AddUserCommand { get; }
    public IRelayCommand<RemoteControlUserViewModel> RemoveUserCommand { get; }
    public IRelayCommand<RemoteControlUserViewModel> SaveUserCommand { get; }
    public IRelayCommand ToggleLocalQrCommand { get; }
    public IAsyncRelayCommand CopyLocalUrlCommand { get; }
    public IRelayCommand TogglePublicQrCommand { get; }
    public IAsyncRelayCommand CopyPublicUrlCommand { get; }
    public IRelayCommand JoinDiscordCommand { get; }

    public RemoteControlViewModel(
        ISettingsService settingsService,
        RemoteAuthenticationService authService,
        LocalNetworkAddressService networkAddressService,
        RemoteDashboardHost? dashboardHost = null)
    {
        _settingsService = settingsService;
        _authService = authService;
        _networkAddressService = networkAddressService;
        _dashboardHost = dashboardHost;

        AddUserCommand = new RelayCommand(AddUser);
        RemoveUserCommand = new RelayCommand<RemoteControlUserViewModel>(RemoveUser);
        SaveUserCommand = new RelayCommand<RemoteControlUserViewModel>(SaveUser);
        ToggleLocalQrCommand = new RelayCommand(() => IsLocalQrVisible = !IsLocalQrVisible);
        CopyLocalUrlCommand = new AsyncRelayCommand(CopyLocalUrlAsync);
        TogglePublicQrCommand = new RelayCommand(() => IsPublicQrVisible = !IsPublicQrVisible);
        CopyPublicUrlCommand = new AsyncRelayCommand(CopyPublicUrlAsync);
        JoinDiscordCommand = new RelayCommand(() => OpenUrl(AppUrls.DiscordInvite));

        LoadSettings();
        GenerateQrCode();
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Settings.RemoteControl;
        IsEnabled = settings.Enabled;
        Port = settings.Port == 0 ? 25580 : settings.Port;
        AccessMode = settings.AccessMode;
        RequireAuthentication = settings.RequireAuthentication;
        AllowRemoteConsoleCommands = settings.AllowRemoteConsoleCommands;
        AllowRemotePlayerActions = settings.AllowRemotePlayerActions;

        IsDiscordLinked = !string.IsNullOrEmpty(_settingsService.Settings.DiscordUserId);
        EnableDiscordNotifications = _settingsService.Settings.EnableDiscordNotifications;

        Users.Clear();
        if (settings.Users != null)
        {
            foreach (var user in settings.Users)
            {
                Users.Add(new RemoteControlUserViewModel(user));
            }
        }
    }

    private void AddUser()
    {
        var user = new RemoteControlUser
        {
            Username = $"User_{Users.Count + 1}"
        };
        var vm = new RemoteControlUserViewModel(user) { IsEditing = true };
        Users.Add(vm);
        SelectedUser = vm;
    }

    private void RemoveUser(RemoteControlUserViewModel? user)
    {
        if (user != null)
        {
            Users.Remove(user);
            SaveUsersToSettings();
        }
    }

    private void SaveUser(RemoteControlUserViewModel? user)
    {
        if (user != null)
        {
            user.ApplyToModel();
            if (!string.IsNullOrWhiteSpace(user.Password))
            {
                user.Model.PasswordHash = _authService.HashPassword(user.Password);
            }
            user.IsEditing = false;
            SaveUsersToSettings();
        }
    }

    private void SaveUsersToSettings()
    {
        var settings = _settingsService.Settings.RemoteControl;
        settings.Users = Users.Select(u => u.Model).ToList();
        _settingsService.Save();
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        var settings = _settingsService.Settings.RemoteControl;
        settings.Enabled = IsEnabled;
        settings.Port = Port;
        settings.AccessMode = AccessMode;
        settings.RequireAuthentication = RequireAuthentication;
        settings.AllowRemoteConsoleCommands = AllowRemoteConsoleCommands;
        settings.AllowRemotePlayerActions = AllowRemotePlayerActions;
        settings.Users = Users.Select(u => u.Model).ToList();

        if (!string.IsNullOrWhiteSpace(Password))
        {
            settings.PasswordHash = _authService.HashPassword(Password);
        }

        _settingsService.Settings.EnableDiscordNotifications = EnableDiscordNotifications;
        _settingsService.Save();
        GenerateQrCode();

        if (_dashboardHost != null)
        {
            try
            {
                if (IsEnabled)
                {
                    await _dashboardHost.StartServerAsync(Port);
                    StatusText = $"Remote dashboard online on port {Port}";
                    IsStatusError = false;
                }
                else
                {
                    await _dashboardHost.StopAsync(CancellationToken.None);
                    StatusText = "Remote dashboard stopped";
                    IsStatusError = false;
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Failed to update host: {ex.Message}";
                IsStatusError = true;
            }
        }
    }

    [RelayCommand]
    private async Task CopyUrlAsync()
    {
        await SetClipboardTextAsync(PairingUrl);
    }

    private async Task CopyLocalUrlAsync()
    {
        await SetClipboardTextAsync(LocalUrl);
    }

    private async Task CopyPublicUrlAsync()
    {
        await SetClipboardTextAsync(PublicUrl);
    }

    private static async Task SetClipboardTextAsync(string text)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.Clipboard != null)
        {
            await desktop.MainWindow.Clipboard.SetTextAsync(text);
        }
    }

    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    private void NavigateBack()
    {
        var navService = App.Services?.GetService(typeof(PocketMC.App.Navigation.IAppNavigationService)) as PocketMC.App.Navigation.IAppNavigationService;
        if (navService?.CanNavigateBack == true)
        {
            navService.NavigateBack();
        }
        else if (navService != null)
        {
            navService.NavigateToDashboard();
        }
        else if (App.Services != null)
        {
            var mainVM = App.Services.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
            if (mainVM != null)
            {
                mainVM.CurrentViewModel = App.Services.GetRequiredService<DashboardViewModel>();
            }
        }
    }

    private void GenerateQrCode()
    {
        var ip = _networkAddressService.GetLocalIPv4Addresses().FirstOrDefault() ?? "127.0.0.1";
        LocalUrl = $"http://{ip}:{Port}/";
        PairingUrl = LocalUrl;

        try
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(PairingUrl, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            var qrCodeImage = qrCode.GetGraphic(20);

            using var stream = new MemoryStream(qrCodeImage);
            PairingQrCode = new Bitmap(stream);
            LocalQrImage = PairingQrCode;
        }
        catch (InvalidOperationException)
        {
            // Headless / non-UI unit test runner
            PairingQrCode = null;
            LocalQrImage = null;
        }
    }
}
