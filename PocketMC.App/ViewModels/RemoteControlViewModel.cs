using System;
using System.Collections.Generic;
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
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Configuration;
using PocketMC.RemoteControl.Models;
using PocketMC.RemoteControl.Services;
using QRCoder;

namespace PocketMC.App.ViewModels;

public sealed partial class RemoteControlViewModel : ObservableObject, IDisposable
{
    public sealed record RemoteAccessModeOption(RemoteAccessMode Mode, string Label);

    public static IReadOnlyList<RemoteAccessModeOption> RemoteAccessModeOptions { get; } =
    [
        new(RemoteAccessMode.CloudflaredQuickTunnel, "Cloudflare Quick Tunnel"),
        new(RemoteAccessMode.PlayitHttpsTunnel, "PlayIt Premium HTTPS")
    ];

    public IReadOnlyList<RemoteAccessModeOption> AccessModes => RemoteAccessModeOptions;

    public RemoteAccessModeOption? SelectedAccessModeOption
    {
        get => AccessModes.FirstOrDefault(m => m.Mode == AccessMode);
        set
        {
            if (value != null && value.Mode != AccessMode)
            {
                AccessMode = value.Mode;
                OnPropertyChanged(nameof(SelectedAccessModeOption));
            }
        }
    }

    private readonly ISettingsService _settingsService;
    private readonly RemoteControlCoordinator _coordinator;
    private readonly RemoteAuthenticationService _authService;
    private readonly LocalNetworkAddressService _networkAddressService;
    private readonly IDialogService? _dialogService;
    private CancellationTokenSource? _statusCts;
    private bool _isRestarting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGeneralTab))]
    [NotifyPropertyChangedFor(nameof(IsUsersTab))]
    private int _selectedTab = 0;

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
    private string? _username;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isAdminCredentialsExpanded;

    public bool IsPasswordSet => !string.IsNullOrEmpty(_settingsService.Settings.RemoteControl.PasswordHash);
    public bool IsPasswordNotSet => string.IsNullOrEmpty(_settingsService.Settings.RemoteControl.PasswordHash);
    public bool IsUsernameNotSet => string.IsNullOrEmpty(_settingsService.Settings.RemoteControl.Username);
    public bool IsOwnerSetupVisible => RequireAuthentication && IsPasswordNotSet;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDiscordNotLinked))]
    private bool _isDiscordLinked;

    public bool IsDiscordNotLinked => !IsDiscordLinked;

    [ObservableProperty]
    private bool _enableDiscordNotifications;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusText))]
    private string _statusText = string.Empty;

    public bool HasStatusText => !string.IsNullOrWhiteSpace(StatusText);

    [ObservableProperty]
    private bool _isStatusError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLocalUrl))]
    private string? _localUrl;

    public bool HasLocalUrl => !string.IsNullOrEmpty(LocalUrl);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPublicUrl))]
    [NotifyPropertyChangedFor(nameof(IsPublicUrlCardVisible))]
    private string? _publicUrl;

    public bool HasPublicUrl => !string.IsNullOrEmpty(PublicUrl);

    [ObservableProperty]
    private string _publicUrlProviderName = "Cloudflare";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPublicUrlCardVisible))]
    private bool _isLoadingPublicUrl;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPublicUrlError))]
    [NotifyPropertyChangedFor(nameof(IsPublicUrlCardVisible))]
    private string? _publicUrlErrorText;

    public bool HasPublicUrlError => !string.IsNullOrEmpty(PublicUrlErrorText);

    public bool IsPublicUrlCardVisible => HasPublicUrl || IsLoadingPublicUrl || HasPublicUrlError;

    [ObservableProperty]
    private Bitmap? _localQrImage;

    [ObservableProperty]
    private Bitmap? _publicQrImage;

    [ObservableProperty]
    private bool _isLocalQrVisible;

    [ObservableProperty]
    private bool _isPublicQrVisible;

    public ObservableCollection<RemoteControlUserViewModel> Users { get; } = new();

    [ObservableProperty]
    private RemoteControlUserViewModel? _selectedUser;

    public RemoteControlViewModel(
        ISettingsService settingsService,
        RemoteControlCoordinator coordinator,
        RemoteAuthenticationService authService,
        LocalNetworkAddressService networkAddressService,
        IDialogService? dialogService = null)
    {
        _settingsService = settingsService;
        _coordinator = coordinator;
        _authService = authService;
        _networkAddressService = networkAddressService;
        _dialogService = dialogService;

        var remote = _settingsService.Settings.RemoteControl;
        _isEnabled = remote.Enabled;
        _port = remote.Port <= 0 ? 25580 : remote.Port;
        _requireAuthentication = remote.RequireAuthentication;
        _username = remote.Username;
        _accessMode = remote.AccessMode == RemoteAccessMode.LanOnly
            ? RemoteAccessMode.CloudflaredQuickTunnel
            : remote.AccessMode;

        _isDiscordLinked = !string.IsNullOrEmpty(_settingsService.Settings.DiscordUserId);
        _enableDiscordNotifications = _settingsService.Settings.EnableDiscordNotifications;

        if (remote.Users != null)
        {
            foreach (var user in remote.Users)
            {
                Users.Add(new RemoteControlUserViewModel(user, () => SaveSettings()));
            }
        }

        UpdateStatus();
    }

    partial void OnIsEnabledChanged(bool value)
    {
        if (value && RequireAuthentication && (IsPasswordNotSet || IsUsernameNotSet))
        {
            _ = HandleEnableWithCredentialsPromptAsync();
        }
        else
        {
            SaveAndRestart();
        }
    }

    private async Task HandleEnableWithCredentialsPromptAsync()
    {
        if (_dialogService == null)
        {
            SaveAndRestart();
            return;
        }

        bool askUsername = IsUsernameNotSet;
        bool askPassword = IsPasswordNotSet;

        while (true)
        {
            var result = await _dialogService.PromptCredentialsAsync(
                "Setup Admin Credentials",
                "Remote Control requires authentication to be secure. Please set up the primary admin account, or turn off authentication to continue without it.",
                askUsername,
                askPassword);

            if (result.Username == null && result.Password == null)
            {
                RequireAuthentication = false;
                SaveAndRestart();
                break;
            }

            if ((askUsername && string.IsNullOrWhiteSpace(result.Username)) ||
                (askPassword && string.IsNullOrWhiteSpace(result.Password)))
            {
                _dialogService.ShowMessage("Invalid Input", "Username and password cannot be empty. Please enter valid credentials.", DialogType.Warning);
                continue;
            }

            if (askUsername) Username = result.Username;
            if (askPassword) Password = result.Password!;
            SaveCredentials();
            SaveAndRestart();
            break;
        }
    }

    partial void OnPortChanged(int value)
    {
        SaveAndRestart();
    }

    partial void OnAccessModeChanged(RemoteAccessMode value)
    {
        OnPropertyChanged(nameof(SelectedAccessModeOption));
        SaveAndRestart();
    }

    partial void OnRequireAuthenticationChanged(bool value)
    {
        if (value)
        {
            _settingsService.Settings.RemoteControl.SecurityStamp = Guid.NewGuid().ToString();
        }
        SaveSettings();
        OnPropertyChanged(nameof(IsOwnerSetupVisible));
    }

    partial void OnEnableDiscordNotificationsChanged(bool value)
    {
        _settingsService.Settings.EnableDiscordNotifications = value;
        _settingsService.Save();
    }

    [RelayCommand]
    private void SaveCredentials()
    {
        if (string.IsNullOrWhiteSpace(Username) && string.IsNullOrWhiteSpace(Password))
        {
            Username = string.Empty;
            Password = string.Empty;
            _settingsService.Settings.RemoteControl.Username = null;
            _settingsService.Settings.RemoteControl.PasswordHash = null;

            _settingsService.Save();
            OnPropertyChanged(nameof(IsPasswordSet));
            OnPropertyChanged(nameof(IsPasswordNotSet));
            OnPropertyChanged(nameof(IsOwnerSetupVisible));
            SetStatus("Admin credentials cleared successfully.", false);
            IsAdminCredentialsExpanded = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(Username))
        {
            SetStatus("Username cannot be empty.", true);
            Username = _settingsService.Settings.RemoteControl.Username ?? "";
            return;
        }

        var trimmedAdminUsername = Username.Trim();

        if (Users.Any(u => string.Equals(u.Username?.Trim(), trimmedAdminUsername, StringComparison.OrdinalIgnoreCase)))
        {
            SetStatus("Admin username cannot be the same as an existing sub-user username.", true);
            Username = _settingsService.Settings.RemoteControl.Username ?? "";
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            if (!string.IsNullOrEmpty(_settingsService.Settings.RemoteControl.PasswordHash))
            {
                Username = trimmedAdminUsername;
                _settingsService.Settings.RemoteControl.Username = trimmedAdminUsername;
                _settingsService.Settings.RemoteControl.SecurityStamp = Guid.NewGuid().ToString();
                _settingsService.Save();
                OnPropertyChanged(nameof(IsPasswordSet));
                OnPropertyChanged(nameof(IsPasswordNotSet));
                OnPropertyChanged(nameof(IsOwnerSetupVisible));
                SetStatus("Admin username updated successfully.", false);
                IsAdminCredentialsExpanded = false;
                return;
            }

            SetStatus("Password cannot be empty.", true);
            return;
        }

        Username = trimmedAdminUsername;
        _settingsService.Settings.RemoteControl.Username = trimmedAdminUsername;
        _settingsService.Settings.RemoteControl.PasswordHash = _authService.HashPassword(Password);
        _settingsService.Settings.RemoteControl.SecurityStamp = Guid.NewGuid().ToString();

        _settingsService.Save();
        OnPropertyChanged(nameof(IsPasswordSet));
        OnPropertyChanged(nameof(IsPasswordNotSet));
        OnPropertyChanged(nameof(IsOwnerSetupVisible));
        SetStatus("Admin credentials saved successfully.", false);
        IsAdminCredentialsExpanded = false;
    }

    [RelayCommand]
    private void AddUser()
    {
        var user = new RemoteControlUser
        {
            Username = $"User_{Users.Count + 1}"
        };
        var userVm = new RemoteControlUserViewModel(user, () => SaveSettings()) { IsEditing = true };
        Users.Add(userVm);
        SelectedUser = userVm;
    }

    [RelayCommand]
    private async Task RemoveUser(RemoteControlUserViewModel? user)
    {
        if (user == null) return;

        if (_dialogService != null)
        {
            var result = await _dialogService.ShowDialogAsync(
                "Remove User",
                $"Are you sure you want to remove the user '{user.Username}'? This action cannot be undone.",
                DialogType.Warning,
                true,
                "Remove",
                "Cancel");

            if (result != DialogResult.Ok && result != DialogResult.Yes)
            {
                return;
            }
        }

        Users.Remove(user);
        SaveSettings();
        SetStatus($"User '{user.Username}' removed successfully.", false);
    }

    [RelayCommand]
    private void SaveUser(RemoteControlUserViewModel? user)
    {
        if (user == null) return;

        if (string.IsNullOrWhiteSpace(user.Username))
        {
            SetStatus("Username cannot be empty.", true);
            if (string.IsNullOrEmpty(user.PasswordHash))
            {
                Users.Remove(user);
            }
            else
            {
                user.Username = user.SavedUsername;
                user.Model.Username = user.SavedUsername;
            }
            return;
        }

        var trimmedUsername = user.Username.Trim();

        var adminUser = _settingsService.Settings.RemoteControl.Username;
        if (!string.IsNullOrEmpty(adminUser) && string.Equals(trimmedUsername, adminUser.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            SetStatus("Sub-user username cannot be the same as the admin username.", true);
            if (string.IsNullOrEmpty(user.PasswordHash))
            {
                Users.Remove(user);
            }
            else
            {
                user.Username = user.SavedUsername;
                user.Model.Username = user.SavedUsername;
            }
            return;
        }

        if (Users.Any(u => u != user && string.Equals(u.Username?.Trim(), trimmedUsername, StringComparison.OrdinalIgnoreCase)))
        {
            SetStatus($"User '{trimmedUsername}' already exists.", true);
            if (string.IsNullOrEmpty(user.PasswordHash))
            {
                Users.Remove(user);
            }
            else
            {
                user.Username = user.SavedUsername;
                user.Model.Username = user.SavedUsername;
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(user.Password))
        {
            if (!string.IsNullOrEmpty(user.PasswordHash))
            {
                user.Username = trimmedUsername;
                user.SavedUsername = trimmedUsername;
                user.Model.Username = trimmedUsername;
                user.IsEditing = false;
                if (!SaveSettings()) return;
                SetStatus($"User '{user.Username}' updated successfully.", false);
                return;
            }

            SetStatus("Password is required to save credentials.", true);
            return;
        }

        var hashed = _authService.HashPassword(user.Password);
        user.Username = trimmedUsername;
        user.SavedUsername = trimmedUsername;
        user.PasswordHash = hashed;
        user.IsEditing = false;

        if (!SaveSettings()) return;
        SetStatus($"User '{user.Username}' credentials saved successfully.", false);
    }

    public bool SaveSettings()
    {
        var settings = _settingsService.Settings;
        if (Port <= 0 || Port > 65535)
        {
            SetStatus("Remote Control port must be between 1 and 65535.", true);
            return false;
        }

        settings.RemoteControl.Enabled = IsEnabled;
        settings.RemoteControl.Port = Port;
        settings.RemoteControl.AccessMode = AccessMode;
        settings.RemoteControl.TunnelProviderId = AccessMode switch
        {
            RemoteAccessMode.CloudflaredQuickTunnel => "cloudflared-quick",
            RemoteAccessMode.PlayitHttpsTunnel => "playit-https",
            _ => "none"
        };
        settings.RemoteControl.RequireAuthentication = RequireAuthentication;

        if (!string.IsNullOrWhiteSpace(Username))
        {
            settings.RemoteControl.Username = Username.Trim();
        }

        settings.RemoteControl.Users = Users.Select(u => u.Model).ToList();
        _settingsService.Save();

        OnPropertyChanged(nameof(IsPasswordSet));
        OnPropertyChanged(nameof(IsPasswordNotSet));
        OnPropertyChanged(nameof(IsOwnerSetupVisible));
        return true;
    }

    public async void SaveAndRestart()
    {
        if (_isRestarting) return;
        if (!SaveSettings()) return;

        _isRestarting = true;
        SetStatus(string.Empty, false);

        try
        {
            if (IsEnabled)
            {
                IsLoadingPublicUrl = true;
                PublicUrlErrorText = null;
                PublicUrl = null;
                PublicUrlProviderName = AccessMode switch
                {
                    RemoteAccessMode.CloudflaredQuickTunnel => "Cloudflare",
                    RemoteAccessMode.PlayitHttpsTunnel => "PlayIt",
                    _ => "Remote"
                };
                await _coordinator.RestartAllAsync();
            }
            else
            {
                await _coordinator.StopAllAsync();
            }
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
        }
        finally
        {
            _isRestarting = false;
            UpdateStatus();
        }
    }

    private void UpdateStatus()
    {
        RemoteDashboardStatus status = _coordinator.GetStatus();
        LocalUrl = status.LocalUrls.FirstOrDefault();

        PublicUrlProviderName = AccessMode switch
        {
            RemoteAccessMode.CloudflaredQuickTunnel => "Cloudflare",
            RemoteAccessMode.PlayitHttpsTunnel => "PlayIt",
            _ => "Remote"
        };
        PublicUrl = status.PublicUrl;
        PublicUrlErrorText = status.TunnelError;
        IsLoadingPublicUrl = false;

        LocalQrImage = GenerateQrCode(LocalUrl);
        PublicQrImage = GenerateQrCode(PublicUrl);

        if (string.IsNullOrEmpty(LocalUrl))
        {
            IsLocalQrVisible = false;
        }
        if (string.IsNullOrEmpty(PublicUrl))
        {
            IsPublicQrVisible = false;
        }
    }

    private static Bitmap? GenerateQrCode(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            byte[] qrCodeBytes = qrCode.GetGraphic(10);

            using var ms = new MemoryStream(qrCodeBytes);
            return new Bitmap(ms);
        }
        catch
        {
            return null;
        }
    }

    private void SetStatus(string message, bool isError, int autoDismissSeconds = 5)
    {
        _statusCts?.Cancel();
        _statusCts?.Dispose();
        _statusCts = null;

        StatusText = message;
        IsStatusError = isError;

        if (!string.IsNullOrEmpty(message) && autoDismissSeconds > 0)
        {
            var cts = new CancellationTokenSource();
            _statusCts = cts;
            _ = ClearStatusAfterDelayAsync(cts.Token, autoDismissSeconds);
        }
    }

    private async Task ClearStatusAfterDelayAsync(CancellationToken token, int seconds)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds), token);
            if (!token.IsCancellationRequested)
            {
                StatusText = string.Empty;
                IsStatusError = false;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    [RelayCommand]
    private void ToggleLocalQr()
    {
        IsLocalQrVisible = !IsLocalQrVisible;
    }

    [RelayCommand]
    private void TogglePublicQr()
    {
        IsPublicQrVisible = !IsPublicQrVisible;
    }

    [RelayCommand]
    private async Task CopyLocalUrlAsync()
    {
        if (!string.IsNullOrEmpty(LocalUrl))
        {
            await SetClipboardTextAsync(LocalUrl);
        }
    }

    [RelayCommand]
    private async Task CopyPublicUrlAsync()
    {
        if (!string.IsNullOrEmpty(PublicUrl))
        {
            await SetClipboardTextAsync(PublicUrl);
        }
    }

    private static async Task SetClipboardTextAsync(string text)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.Clipboard != null)
        {
            await desktop.MainWindow.Clipboard.SetTextAsync(text);
        }
    }

    [RelayCommand]
    private void JoinDiscord()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = AppUrls.DiscordInvite,
                UseShellExecute = true
            });
        }
        catch
        {
        }
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
    }

    public void Dispose()
    {
        _statusCts?.Cancel();
        _statusCts?.Dispose();
        _statusCts = null;
    }
}
