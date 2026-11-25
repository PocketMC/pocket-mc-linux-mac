using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.App.Services;
using PocketMC.App.ViewModels.Settings;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Configuration;
using PocketMC.Infrastructure.Services;

namespace PocketMC.App.ViewModels;

public sealed record AccentPresetOption(string Name, string Hex)
{
    public IBrush SwatchBrush { get; } = new SolidColorBrush(Color.Parse(Hex));
}

public sealed record DependencyHealthRow(string Name, string Status, string StatusColor, string Details)
{
    public IBrush StatusBrush { get; } = new SolidColorBrush(Color.Parse(StatusColor));
}

public partial class AppSettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly AccentColorService _accentColorService;
    private readonly WallpaperBackdropService _wallpaperService;
    private readonly SettingsBackupService _backupService;
    private readonly IDialogService? _dialogService;
    private readonly IOllamaService? _ollamaService;
    private readonly ThemeManager? _themeManager;

    // 1. App Behavior
    [ObservableProperty] private bool _startMinimizedToTray;
    [ObservableProperty] private bool _minimizeToTrayOnClose;
    [ObservableProperty] private bool _keepComputerAwakeWhileServersRunning = true;
    [ObservableProperty] private bool _enableTelemetry = true;
    [ObservableProperty] private int _consoleBufferSize = 5000;

    // 2. Storage & Data Root
    [ObservableProperty] private string _dataRootPath = string.Empty;
    [ObservableProperty] private string? _externalBackupDirectory;

    // 3. Appearance & Wallpaper Backdrop
    [ObservableProperty] private string _selectedTheme = "System";
    [ObservableProperty] private string _accentColorMode = "Custom";
    [ObservableProperty] private string _customAccentColor = "#008B00";
    [ObservableProperty] private string _windowBackdrop = "BlurredWallpaper";
    [ObservableProperty] private double _wallpaperBlurRadius = 80.0;
    [ObservableProperty] private double _wallpaperTintOpacity = 0.72;
    [ObservableProperty] private string? _customBackgroundImagePath;

    // 4. Notifications
    [ObservableProperty] private bool _enableServerOnlineNotifications = true;
    [ObservableProperty] private bool _enableAgentConnectNotifications = true;
    [ObservableProperty] private bool _enableRemoteControlNotifications = true;
    [ObservableProperty] private bool _enableAiSummaryNotifications = true;

    // 5. Discord Rich Presence + Live Preview
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewIdleVisible))]
    private bool _enableDiscordRpc = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewIdleVisible))]
    private bool _discordRpcShowIdle = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewServerTitle))]
    private bool _discordRpcShowServerName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewActivityLine2))]
    private bool _discordRpcShowServerAddress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewActivityLine2))]
    private bool _discordRpcShowPlayerCount = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewActivityLine1))]
    private bool _discordRpcShowVersion = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewUptimeVisible))]
    private bool _discordRpcShowUptime = true;

    [ObservableProperty] private bool _discordRpcShowSoftwareIcon;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewDownloadVisible))]
    private bool _discordRpcShowDownloadButton = true;

    // 6. Cloud Backups
    public CloudBackupSettingsViewModel? CloudBackupsVm { get; }

    // 7. API Configuration
    [ObservableProperty] private string? _curseForgeApiKey;

    // 8. AI Diagnostics & Session Summarization
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOllamaProvider))]
    private string _aiProvider = "Gemini";

    [ObservableProperty] private string? _aiApiKey;
    [ObservableProperty] private string? _aiModel;
    [ObservableProperty] private string? _aiEndpoint;
    [ObservableProperty] private bool _enableAiSummarization;
    [ObservableProperty] private bool _autoSummarizeOnStop = true;
    [ObservableProperty] private bool _autoSummarizeOnCrash = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLocalOllamaMode))]
    private string _ollamaMode = "Local";

    [ObservableProperty] private string _ollamaDaemonStatus = "Not checked";
    [ObservableProperty] private string _aiKeyValidationStatus = string.Empty;

    // 9. Software Updates
    [ObservableProperty] private string _updateChannel = "Stable";
    [ObservableProperty] private bool _enableAutoUpdates = true;
    [ObservableProperty] private string _updateCheckStatus = string.Empty;

    [ObservableProperty] private string? _statusMessage;

    public string CurrentVersionLabel => $"Installed Version: {AppConfig.AppVersion} ({AppConfig.Channel})";
    public string CustomBgDisplayName => string.IsNullOrWhiteSpace(CustomBackgroundImagePath)
        ? "PocketMC Default Wallpaper"
        : Path.GetFileName(CustomBackgroundImagePath);

    public bool IsOllamaProvider => string.Equals(AiProvider, "Ollama", StringComparison.OrdinalIgnoreCase);
    public bool IsLocalOllamaMode => string.Equals(OllamaMode, "Local", StringComparison.OrdinalIgnoreCase);

    // Live Discord Rich Presence Preview Computeds (matches Windows AppSettingsPage.xaml PreviewOnlineCard/PreviewIdleCard)
    public string PreviewServerTitle => DiscordRpcShowServerName ? "Survival SMP" : "Hosting a Minecraft Server";
    public string PreviewActivityLine1 => DiscordRpcShowVersion ? "Minecraft 1.21.4 (Paper)" : "Playing Minecraft";
    public string PreviewActivityLine2
    {
        get
        {
            var parts = new List<string>();
            if (DiscordRpcShowPlayerCount) parts.Add("3/20 Players");
            if (DiscordRpcShowServerAddress) parts.Add("play.example.ply.gg");
            return parts.Count > 0 ? string.Join(" · ", parts) : "Private Session";
        }
    }
    public bool PreviewUptimeVisible => DiscordRpcShowUptime;
    public bool PreviewDownloadVisible => DiscordRpcShowDownloadButton;
    public bool PreviewIdleVisible => EnableDiscordRpc && DiscordRpcShowIdle;

    public List<string> ThemeOptions { get; } = new() { "System", "Dark", "Light" };
    public List<string> BackdropOptions { get; } = new() { "BlurredWallpaper", "None" };
    public List<string> AccentModeOptions { get; } = new() { "Custom", "Automatic" };
    public List<string> AiProviderOptions { get; } = new() { "Gemini", "OpenAI", "Anthropic", "OpenAI-Compatible", "Ollama" };
    public List<string> OllamaModeOptions { get; } = new() { "Local", "Cloud" };
    public List<string> UpdateChannelOptions { get; } = new() { "Stable", "Beta" };

    public List<AccentPresetOption> AccentColorPresets { get; } = new()
    {
        new("Blue", "#0078D4"),
        new("Dark Blue", "#003E92"),
        new("Teal", "#008272"),
        new("Cyan", "#0099BC"),
        new("PocketMC Green", "#008B00"),
        new("Emerald", "#10893E"),
        new("Yellow", "#986F0B"),
        new("Orange", "#CA5010"),
        new("Red", "#D13438"),
        new("Rose", "#E3008C"),
        new("Purple", "#744DA9"),
        new("Violet", "#B146C2"),
        new("Slate", "#647687"),
        new("Steel", "#525E7D"),
        new("Gold", "#C19C00"),
        new("Coral", "#E74856")
    };

    public ObservableCollection<DependencyHealthRow> DependencyHealthItems { get; } = new();

    public AppSettingsViewModel(
        ISettingsService settingsService,
        AccentColorService accentColorService,
        WallpaperBackdropService wallpaperService,
        SettingsBackupService backupService,
        IDialogService? dialogService = null,
        IOllamaService? ollamaService = null,
        ThemeManager? themeManager = null,
        IEnumerable<ICloudBackupProvider>? cloudProviders = null)
    {
        _settingsService = settingsService;
        _accentColorService = accentColorService;
        _wallpaperService = wallpaperService;
        _backupService = backupService;
        _dialogService = dialogService;
        _ollamaService = ollamaService ?? (App.Services?.GetService(typeof(IOllamaService)) as IOllamaService);
        _themeManager = themeManager ?? (App.Services?.GetService(typeof(ThemeManager)) as ThemeManager);

        var providers = cloudProviders ??
                        (App.Services?.GetService(typeof(IEnumerable<ICloudBackupProvider>)) as IEnumerable<ICloudBackupProvider>);
        if (providers != null)
        {
            CloudBackupsVm = new CloudBackupSettingsViewModel(_settingsService, providers, _dialogService);
        }

        LoadFromSettings();
    }

    partial void OnCustomBackgroundImagePathChanged(string? value)
    {
        OnPropertyChanged(nameof(CustomBgDisplayName));
    }

    partial void OnAiProviderChanged(string value)
    {
        var s = _settingsService.Settings;
        AiApiKey = s.AiApiKeys.TryGetValue(value, out var k) ? k : null;
        AiModel = s.AiModels.TryGetValue(value, out var m) ? m : null;
        AiEndpoint = s.AiEndpoints.TryGetValue(value, out var e) ? e : null;
    }

    public void LoadFromSettings()
    {
        var s = _settingsService.Settings;

        SelectedTheme = s.Theme;
        AccentColorMode = s.AccentColorMode;
        CustomAccentColor = s.CustomAccentColor ?? "#008B00";
        WindowBackdrop = s.WindowBackdrop;
        WallpaperBlurRadius = s.WallpaperBlurRadius;
        WallpaperTintOpacity = s.WallpaperTintOpacity;
        CustomBackgroundImagePath = s.CustomBackgroundImagePath;

        StartMinimizedToTray = s.StartMinimizedToTray;
        MinimizeToTrayOnClose = s.MinimizeToTrayOnClose;
        KeepComputerAwakeWhileServersRunning = s.KeepComputerAwakeWhileServersRunning;
        EnableTelemetry = s.EnableTelemetry;
        ConsoleBufferSize = s.ConsoleBufferSize;

        EnableServerOnlineNotifications = s.EnableServerOnlineNotifications;
        EnableAgentConnectNotifications = s.EnableAgentConnectNotifications;
        EnableRemoteControlNotifications = s.EnableRemoteControlNotifications;
        EnableAiSummaryNotifications = s.EnableAiSummaryNotifications;

        EnableDiscordRpc = s.EnableDiscordRpc;
        DiscordRpcShowIdle = s.DiscordRpc.ShowIdle;
        DiscordRpcShowServerName = s.DiscordRpc.ShowServerName;
        DiscordRpcShowServerAddress = s.DiscordRpc.ShowServerAddress;
        DiscordRpcShowPlayerCount = s.DiscordRpc.ShowPlayerCount;
        DiscordRpcShowVersion = s.DiscordRpc.ShowVersionAndEngine;
        DiscordRpcShowUptime = s.DiscordRpc.ShowUptimeTimer;
        DiscordRpcShowSoftwareIcon = s.DiscordRpc.ShowSoftwareIcon;
        DiscordRpcShowDownloadButton = s.DiscordRpc.ShowDownloadButton;

        AiProvider = s.AiProvider;
        AiApiKey = s.GetCurrentAiKey();
        AiModel = s.GetCurrentAiModel();
        AiEndpoint = s.GetCurrentAiEndpoint();
        EnableAiSummarization = s.EnableAiSummarization;
        AutoSummarizeOnStop = s.AutoSummarizeOnStop;
        AutoSummarizeOnCrash = s.AutoSummarizeOnCrash;
        OllamaMode = s.OllamaMode;

        CurseForgeApiKey = s.CurseForgeApiKey;
        DataRootPath = _settingsService.GetDataRoot();
        ExternalBackupDirectory = s.ExternalBackupDirectory;

        UpdateChannel = s.UpdateChannel;
        EnableAutoUpdates = s.EnableAutoUpdates;
    }

    [RelayCommand]
    public void SaveSettings()
    {
        var s = _settingsService.Settings;

        s.Theme = SelectedTheme;
        s.AccentColorMode = AccentColorMode;
        s.CustomAccentColor = CustomAccentColor;
        s.WindowBackdrop = WindowBackdrop;
        s.WallpaperBlurRadius = WallpaperBlurRadius;
        s.WallpaperTintOpacity = WallpaperTintOpacity;
        s.CustomBackgroundImagePath = CustomBackgroundImagePath;

        s.StartMinimizedToTray = StartMinimizedToTray;
        s.MinimizeToTrayOnClose = MinimizeToTrayOnClose;
        s.KeepComputerAwakeWhileServersRunning = KeepComputerAwakeWhileServersRunning;
        s.EnableTelemetry = EnableTelemetry;
        s.ConsoleBufferSize = ConsoleBufferSize;

        s.EnableServerOnlineNotifications = EnableServerOnlineNotifications;
        s.EnableAgentConnectNotifications = EnableAgentConnectNotifications;
        s.EnableRemoteControlNotifications = EnableRemoteControlNotifications;
        s.EnableAiSummaryNotifications = EnableAiSummaryNotifications;

        s.EnableDiscordRpc = EnableDiscordRpc;
        s.DiscordRpc.Enabled = EnableDiscordRpc;
        s.DiscordRpc.ShowIdle = DiscordRpcShowIdle;
        s.DiscordRpc.ShowServerName = DiscordRpcShowServerName;
        s.DiscordRpc.ShowServerAddress = DiscordRpcShowServerAddress;
        s.DiscordRpc.ShowPlayerCount = DiscordRpcShowPlayerCount;
        s.DiscordRpc.ShowVersionAndEngine = DiscordRpcShowVersion;
        s.DiscordRpc.ShowUptimeTimer = DiscordRpcShowUptime;
        s.DiscordRpc.ShowSoftwareIcon = DiscordRpcShowSoftwareIcon;
        s.DiscordRpc.ShowDownloadButton = DiscordRpcShowDownloadButton;

        s.AiProvider = AiProvider;
        s.EnableAiSummarization = EnableAiSummarization;
        s.AutoSummarizeOnStop = AutoSummarizeOnStop;
        s.AutoSummarizeOnCrash = AutoSummarizeOnCrash;
        s.OllamaMode = OllamaMode;
        if (!string.IsNullOrWhiteSpace(AiApiKey)) s.AiApiKeys[AiProvider] = AiApiKey;
        if (!string.IsNullOrWhiteSpace(AiModel)) s.AiModels[AiProvider] = AiModel;
        if (!string.IsNullOrWhiteSpace(AiEndpoint)) s.AiEndpoints[AiProvider] = AiEndpoint;

        s.CurseForgeApiKey = CurseForgeApiKey;
        s.ExternalBackupDirectory = ExternalBackupDirectory;
        s.UpdateChannel = UpdateChannel;
        s.EnableAutoUpdates = EnableAutoUpdates;

        _settingsService.Save();

        if (_themeManager != null && Enum.TryParse<ThemeMode>(SelectedTheme, ignoreCase: true, out var themeMode))
        {
            _themeManager.ApplyTheme(themeMode);
        }

        if (AccentColorService.IsCustomMode(AccentColorMode) &&
            AccentColorService.TryParseHexColor(CustomAccentColor, out Color color, out _))
        {
            _accentColorService.ApplyCustomAccent(color);
        }
        else
        {
            _accentColorService.ApplySystemAccent();
        }

        var mainVm = App.Services?.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
        mainVm?.RequestBackdropRefresh();

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

    [RelayCommand]
    private async Task BrowseCustomWallpaperAsync()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
            if (topLevel != null)
            {
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Custom Wallpaper Image",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Image Files") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp" } }
                    }
                });

                if (files.Count > 0)
                {
                    CustomBackgroundImagePath = files[0].Path.LocalPath;
                    SaveSettings();
                }
            }
        }
    }

    [RelayCommand]
    private void OpenDataRootFolder()
    {
        try
        {
            string path = _settingsService.GetDataRoot();
            if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
        }
        catch { }
    }

    [RelayCommand]
    private async Task ChangeDataRootAsync()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
            if (topLevel != null)
            {
                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Storage Root Directory",
                    AllowMultiple = false
                });
                if (folders.Count > 0)
                {
                    string newPath = folders[0].Path.LocalPath;
                    Directory.CreateDirectory(newPath);
                    _settingsService.SetDataRoot(newPath);
                    DataRootPath = newPath;
                    StatusMessage = $"Storage root changed to {newPath}";
                }
            }
        }
    }

    [RelayCommand]
    private async Task BrowseExternalBackupAsync()
    {
        if (_dialogService == null) return;
        string? folder = await _dialogService.OpenFolderDialogAsync("Select External Backup Destination");
        if (!string.IsNullOrWhiteSpace(folder))
        {
            ExternalBackupDirectory = folder;
            SaveSettings();
        }
    }

    [RelayCommand]
    private async Task CheckOllamaDaemonAsync()
    {
        if (_ollamaService == null)
        {
            OllamaDaemonStatus = "Ollama service unavailable.";
            return;
        }

        string endpoint = string.IsNullOrWhiteSpace(AiEndpoint) ? "http://localhost:11434" : AiEndpoint;
        OllamaDaemonStatus status = await _ollamaService.CheckDaemonHealthAsync(endpoint);
        OllamaDaemonStatus = status.IsRunning
            ? $"Online (Ollama v{status.Version})"
            : (status.ErrorMessage ?? "Offline");
    }

    [RelayCommand]
    private async Task OpenOllamaModelManagerAsync()
    {
        if (_ollamaService == null) return;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            string endpoint = string.IsNullOrWhiteSpace(AiEndpoint) ? "http://localhost:11434" : AiEndpoint;
            var vm = new OllamaModelManagerViewModel(_ollamaService, endpoint, AiApiKey);
            var dialog = new Views.Intelligence.OllamaModelManagerDialog(vm);
            string? selected = await dialog.ShowDialog<string?>(desktop.MainWindow);
            if (!string.IsNullOrWhiteSpace(selected))
            {
                AiModel = selected;
                SaveSettings();
            }
        }
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        UpdateCheckStatus = "Checking GitHub releases...";
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"PocketMC.App/{AppConfig.AppVersion}");
            string apiUrl = "https://api.github.com/repos/PocketMC/pocket-mc-linux-mac/releases";
            string json = await http.GetStringAsync(apiUrl);
            using var doc = JsonDocument.Parse(json);

            foreach (var rel in doc.RootElement.EnumerateArray())
            {
                bool isPrerelease = rel.TryGetProperty("prerelease", out var pre) && pre.GetBoolean();
                if (string.Equals(UpdateChannel, "Stable", StringComparison.OrdinalIgnoreCase) && isPrerelease)
                {
                    continue;
                }

                string? tag = rel.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() : null;
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    UpdateCheckStatus = string.Equals(tag.TrimStart('v'), AppConfig.AppVersion.TrimStart('v'), StringComparison.OrdinalIgnoreCase)
                        ? $"You are up to date ({AppConfig.AppVersion})."
                        : $"Update available: {tag} (Visit Releases to upgrade)";
                    return;
                }
            }

            UpdateCheckStatus = $"No newer releases on {UpdateChannel} channel.";
        }
        catch (Exception ex)
        {
            UpdateCheckStatus = $"Update check failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CheckDependenciesAsync()
    {
        DependencyHealthItems.Clear();
        DependencyHealthItems.Add(new DependencyHealthRow("Checking...", "Pending", "#A1A1AA", "Probing upstream services"));

        var checks = new (string Name, string Url)[]
        {
            ("Adoptium JDK", AppConfig.HealthCheckAdoptium),
            ("Modrinth API", AppConfig.HealthCheckModrinth),
            ("Playit.gg", AppConfig.HealthCheckPlayit)
        };

        var results = new List<DependencyHealthRow>();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };

        foreach (var (name, url) in checks)
        {
            try
            {
                using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                results.Add(resp.IsSuccessStatusCode
                    ? new DependencyHealthRow(name, "Online", "#10B981", $"HTTP {(int)resp.StatusCode}")
                    : new DependencyHealthRow(name, "Degraded", "#F59E0B", $"HTTP {(int)resp.StatusCode}"));
            }
            catch (Exception ex)
            {
                results.Add(new DependencyHealthRow(name, "Unreachable", "#EF4444", ex.Message));
            }
        }

        Dispatcher.UIThread.Post(() =>
        {
            DependencyHealthItems.Clear();
            foreach (var r in results) DependencyHealthItems.Add(r);
        });
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        var defaults = new Core.Models.Settings();
        SelectedTheme = defaults.Theme;
        AccentColorMode = defaults.AccentColorMode;
        CustomAccentColor = defaults.CustomAccentColor ?? "#008B00";
        WindowBackdrop = defaults.WindowBackdrop;
        WallpaperBlurRadius = defaults.WallpaperBlurRadius;
        WallpaperTintOpacity = defaults.WallpaperTintOpacity;
        CustomBackgroundImagePath = null;
        StartMinimizedToTray = false;
        MinimizeToTrayOnClose = false;
        KeepComputerAwakeWhileServersRunning = true;
        ConsoleBufferSize = 5000;
        SaveSettings();
        StatusMessage = "Settings reset to defaults.";
    }
}
