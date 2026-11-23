using System;
using System.IO;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.App.Services;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;

namespace PocketMC.App.ViewModels;

public partial class AppSettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly AccentColorService _accentColorService;
    private readonly WallpaperBackdropService _wallpaperService;
    private readonly SettingsBackupService _backupService;
    private readonly IDialogService? _dialogService;

    [ObservableProperty]
    private string _accentColorMode = "Custom";

    [ObservableProperty]
    private string _customAccentColor = "#008B00";

    [ObservableProperty]
    private string _windowBackdrop = "BlurredWallpaper";

    [ObservableProperty]
    private double _wallpaperBlurRadius = 80.0;

    [ObservableProperty]
    private double _wallpaperTintOpacity = 0.72;

    [ObservableProperty]
    private string? _customBackgroundImagePath;

    [ObservableProperty]
    private bool _startMinimizedToTray;

    [ObservableProperty]
    private bool _minimizeToTrayOnClose;

    [ObservableProperty]
    private bool _keepComputerAwakeWhileServersRunning = true;

    [ObservableProperty]
    private int _consoleBufferSize = 5000;

    [ObservableProperty]
    private bool _enableDiscordRpc = true;

    [ObservableProperty]
    private bool _discordRpcShowServerName;

    [ObservableProperty]
    private bool _discordRpcShowPlayerCount = true;

    [ObservableProperty]
    private bool _discordRpcShowVersion = true;

    [ObservableProperty]
    private bool _discordRpcShowUptime = true;

    [ObservableProperty]
    private bool _discordRpcShowDownloadButton = true;

    [ObservableProperty]
    private string _aiProvider = "Gemini";

    [ObservableProperty]
    private string? _aiApiKey;

    [ObservableProperty]
    private string? _aiModel;

    [ObservableProperty]
    private string? _aiEndpoint;

    [ObservableProperty]
    private bool _enableAiSummarization;

    [ObservableProperty]
    private string? _curseForgeApiKey;

    [ObservableProperty]
    private string _dataRootPath = string.Empty;

    [ObservableProperty]
    private string? _externalBackupDirectory;

    [ObservableProperty]
    private string? _statusMessage;

    public AppSettingsViewModel(
        ISettingsService settingsService,
        AccentColorService accentColorService,
        WallpaperBackdropService wallpaperService,
        SettingsBackupService backupService,
        IDialogService? dialogService = null)
    {
        _settingsService = settingsService;
        _accentColorService = accentColorService;
        _wallpaperService = wallpaperService;
        _backupService = backupService;
        _dialogService = dialogService;

        LoadFromSettings();
    }

    public void LoadFromSettings()
    {
        var s = _settingsService.Settings;

        AccentColorMode = s.AccentColorMode;
        CustomAccentColor = s.CustomAccentColor ?? "#008B00";
        WindowBackdrop = s.WindowBackdrop;
        WallpaperBlurRadius = s.WallpaperBlurRadius;
        WallpaperTintOpacity = s.WallpaperTintOpacity;
        CustomBackgroundImagePath = s.CustomBackgroundImagePath;

        StartMinimizedToTray = s.StartMinimizedToTray;
        MinimizeToTrayOnClose = s.MinimizeToTrayOnClose;
        KeepComputerAwakeWhileServersRunning = s.KeepComputerAwakeWhileServersRunning;
        ConsoleBufferSize = s.ConsoleBufferSize;

        EnableDiscordRpc = s.EnableDiscordRpc;
        DiscordRpcShowServerName = s.DiscordRpc.ShowServerName;
        DiscordRpcShowPlayerCount = s.DiscordRpc.ShowPlayerCount;
        DiscordRpcShowVersion = s.DiscordRpc.ShowVersionAndEngine;
        DiscordRpcShowUptime = s.DiscordRpc.ShowUptimeTimer;
        DiscordRpcShowDownloadButton = s.DiscordRpc.ShowDownloadButton;

        AiProvider = s.AiProvider;
        AiApiKey = s.GetCurrentAiKey();
        AiModel = s.GetCurrentAiModel();
        AiEndpoint = s.GetCurrentAiEndpoint();
        EnableAiSummarization = s.EnableAiSummarization;

        CurseForgeApiKey = s.CurseForgeApiKey;
        DataRootPath = _settingsService.GetDataRoot();
        ExternalBackupDirectory = s.ExternalBackupDirectory;
    }

    [RelayCommand]
    public void SaveSettings()
    {
        var s = _settingsService.Settings;

        s.AccentColorMode = AccentColorMode;
        s.CustomAccentColor = CustomAccentColor;
        s.WindowBackdrop = WindowBackdrop;
        s.WallpaperBlurRadius = WallpaperBlurRadius;
        s.WallpaperTintOpacity = WallpaperTintOpacity;
        s.CustomBackgroundImagePath = CustomBackgroundImagePath;

        s.StartMinimizedToTray = StartMinimizedToTray;
        s.MinimizeToTrayOnClose = MinimizeToTrayOnClose;
        s.KeepComputerAwakeWhileServersRunning = KeepComputerAwakeWhileServersRunning;
        s.ConsoleBufferSize = ConsoleBufferSize;

        s.EnableDiscordRpc = EnableDiscordRpc;
        s.DiscordRpc.Enabled = EnableDiscordRpc;
        s.DiscordRpc.ShowServerName = DiscordRpcShowServerName;
        s.DiscordRpc.ShowPlayerCount = DiscordRpcShowPlayerCount;
        s.DiscordRpc.ShowVersionAndEngine = DiscordRpcShowVersion;
        s.DiscordRpc.ShowUptimeTimer = DiscordRpcShowUptime;
        s.DiscordRpc.ShowDownloadButton = DiscordRpcShowDownloadButton;

        s.AiProvider = AiProvider;
        s.EnableAiSummarization = EnableAiSummarization;
        if (!string.IsNullOrWhiteSpace(AiApiKey)) s.AiApiKeys[AiProvider] = AiApiKey;
        if (!string.IsNullOrWhiteSpace(AiModel)) s.AiModels[AiProvider] = AiModel;
        if (!string.IsNullOrWhiteSpace(AiEndpoint)) s.AiEndpoints[AiProvider] = AiEndpoint;

        s.CurseForgeApiKey = CurseForgeApiKey;
        s.ExternalBackupDirectory = ExternalBackupDirectory;

        _settingsService.Save();

        // Apply visual updates live
        if (AccentColorService.TryParseHexColor(CustomAccentColor, out Color color, out _))
        {
            _accentColorService.ApplyCustomAccent(color);
        }

        StatusMessage = "Settings saved successfully.";
    }

    [RelayCommand]
    public void ApplyPresetColor(string hex)
    {
        CustomAccentColor = hex;
        AccentColorMode = "Custom";
        if (AccentColorService.TryParseHexColor(hex, out Color color, out _))
        {
            _accentColorService.ApplyCustomAccent(color);
        }
        SaveSettings();
    }

    [RelayCommand]
    public void ResetWallpaper()
    {
        CustomBackgroundImagePath = null;
        SaveSettings();
    }
}
