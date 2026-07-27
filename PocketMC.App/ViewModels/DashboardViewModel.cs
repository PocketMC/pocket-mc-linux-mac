using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;
using PocketMC.RemoteControl.Tunnels;
using PocketMC.RemoteControl.Services;

namespace PocketMC.App.ViewModels
{
    public partial class DashboardViewModel : ObservableObject, IDisposable
    {
        private readonly IInstanceService _instanceService;
        private readonly IProcessRunner _processRunner;
        private readonly IPlayerService _playerService;
        private readonly ProcessMetricsTracker _metricsTracker;
        private readonly ThemeManager _themeManager;
        private readonly PlayitApiClient _playitClient;
        private readonly LocalNetworkAddressService _localNetworkAddressService;
        private readonly System.Timers.Timer _timer;

        private readonly Dictionary<string, CircularBuffer<double>> _cpuHistories = new();
        private readonly Dictionary<string, CircularBuffer<double>> _ramHistories = new();

        [ObservableProperty]
        private ObservableCollection<ServerInstance> _instances = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSelected))]
        [NotifyPropertyChangedFor(nameof(SelectedInstanceName))]
        [NotifyPropertyChangedFor(nameof(SelectedInstanceEngineType))]
        [NotifyPropertyChangedFor(nameof(SelectedInstanceEngineVersion))]
        private ServerInstance? _selectedInstance;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartServerCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopServerCommand))]
        private string _serverState = "Stopped";

        [ObservableProperty]
        private double _cpuUsagePercent;

        [ObservableProperty]
        private string _ramUsageFormatted = "0 MB";

        [ObservableProperty]
        private string _portStatusFormatted = "Offline";

        [ObservableProperty]
        private string _configuredPortFormatted = "Unknown";

        [ObservableProperty]
        private int _onlinePlayersCount;

        [ObservableProperty]
        private string _onlinePlayersList = "None";

        [ObservableProperty]
        private string _uptime = "00:00:00";

        [ObservableProperty]
        private IEnumerable<double> _cpuHistory = Enumerable.Repeat(0.0, 30);

        [ObservableProperty]
        private IEnumerable<double> _ramHistory = Enumerable.Repeat(0.0, 30);

        [ObservableProperty]
        private bool _hasInstances;

        [ObservableProperty]
        private string _themeOverride = "System";

        public string SelectedInstanceName => SelectedInstance?.Name ?? string.Empty;

        public string SelectedInstanceEngineType => SelectedInstance?.EngineType.ToString() ?? string.Empty;

        public string SelectedInstanceEngineVersion => SelectedInstance?.EngineVersion ?? string.Empty;

        [ObservableProperty]
        private bool _hasPlayitTunnels;

        [ObservableProperty]
        private string? _javaTunnelAddress;

        [ObservableProperty]
        private bool _showJavaTunnel;

        [ObservableProperty]
        private string? _bedrockTunnelAddress;

        [ObservableProperty]
        private bool _showBedrockTunnel;

        [ObservableProperty]
        private string? _voiceChatTunnelAddress;

        [ObservableProperty]
        private bool _showVoiceChatTunnel;

        [ObservableProperty]
        private bool _isCreatingTunnels;

        [ObservableProperty]
        private string? _lanAddressDisplayText;

        [ObservableProperty]
        private bool _hasLanAddress;

        [ObservableProperty]
        private bool _showBedrockIp;

        private readonly ISettingsService _settingsService;
        private readonly RemoteTunnelManager _tunnelManager;
        private List<PocketMC.RemoteControl.Tunnels.TunnelData> _cachedActiveTunnels = new();
        private int _pollCount = 0;

        public bool IsSelected => SelectedInstance != null;

        public DashboardViewModel(
            IInstanceService instanceService,
            IProcessRunner processRunner,
            IPlayerService playerService,
            ProcessMetricsTracker metricsTracker,
            ThemeManager themeManager,
            PlayitApiClient playitClient,
            LocalNetworkAddressService localNetworkAddressService,
            ISettingsService settingsService,
            RemoteTunnelManager tunnelManager)
        {
            _instanceService = instanceService;
            _processRunner = processRunner;
            _playerService = playerService;
            _metricsTracker = metricsTracker;
            _themeManager = themeManager;
            _playitClient = playitClient;
            _localNetworkAddressService = localNetworkAddressService;
            _settingsService = settingsService;
            _tunnelManager = tunnelManager;

            _processRunner.StateChanged += OnProcessStateChanged;

            _timer = new System.Timers.Timer(2000);
            _timer.Elapsed += OnTimerElapsed;

            // Load persisted theme settings
            ThemeOverride = _settingsService.Settings.Theme ?? "System";
            var mode = ThemeOverride switch
            {
                "Light" => ThemeMode.Light,
                "Dark" => ThemeMode.Dark,
                _ => ThemeMode.System
            };
            _themeManager.ApplyTheme(mode);
            
            // Initial load
            _ = LoadInstancesAsync();
        }

        private void OnProcessStateChanged(string slug, string state)
        {
            if (SelectedInstance != null && SelectedInstance.Slug == slug)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    ServerState = state;
                    StartServerCommand.NotifyCanExecuteChanged();
                    StopServerCommand.NotifyCanExecuteChanged();
                });
            }
        }

        public async Task LoadInstancesAsync()
        {
            var list = await _instanceService.ListInstancesAsync();
            Action updateAction = () =>
            {
                var currentSlug = SelectedInstance?.Slug ?? _settingsService.Settings.LastSelectedInstanceSlug;

                Instances.Clear();
                foreach (var inst in list)
                {
                    Instances.Add(inst);
                }
                HasInstances = Instances.Count > 0;

                ServerInstance? target = null;
                if (!string.IsNullOrEmpty(currentSlug))
                {
                    target = Instances.FirstOrDefault(i => i.Slug == currentSlug);
                }

                if (target == null && HasInstances)
                {
                    target = Instances[0];
                }

                SelectedInstance = target;
                OnPropertyChanged(nameof(SelectedInstanceName));
                OnPropertyChanged(nameof(SelectedInstanceEngineType));
                OnPropertyChanged(nameof(SelectedInstanceEngineVersion));
                OnPropertyChanged(nameof(IsSelected));

                if (!_timer.Enabled)
                {
                    _timer.Start();
                }
            };

            if (Dispatcher.UIThread.CheckAccess())
            {
                updateAction();
            }
            else
            {
                Dispatcher.UIThread.Post(updateAction);
            }
        }

        partial void OnSelectedInstanceChanged(ServerInstance? value)
        {
            if (value != null)
            {
                _settingsService.Settings.LastSelectedInstanceSlug = value.Slug;
                _settingsService.Save();
            }

            if (value == null)
            {
                ServerState = "Stopped";
                CpuUsagePercent = 0;
                RamUsageFormatted = "0 MB";
                PortStatusFormatted = "Offline";
                ConfiguredPortFormatted = "Unknown";
                OnlinePlayersCount = 0;
                OnlinePlayersList = "None";
                Uptime = "00:00:00";
                CpuHistory = Enumerable.Repeat(0.0, 30);
                RamHistory = Enumerable.Repeat(0.0, 30);
                ShowBedrockIp = false;
                ShowJavaTunnel = false;
                ShowBedrockTunnel = false;
                HasPlayitTunnels = false;
                HasLanAddress = false;
                return;
            }

            // Sync current state
            if (_processRunner.TryGetRunningInfo(value.Slug, out _, out string state))
            {
                ServerState = state;
            }
            else
            {
                ServerState = "Stopped";
            }

            // Load histories
            CpuHistory = GetOrCreateHistory(_cpuHistories, value.Slug).ToList();
            RamHistory = GetOrCreateHistory(_ramHistories, value.Slug).ToList();

            StartServerCommand.NotifyCanExecuteChanged();
            StopServerCommand.NotifyCanExecuteChanged();

            // Force poll immediately
            _ = PollMetricsAsync();
        }

        private CircularBuffer<double> GetOrCreateHistory(Dictionary<string, CircularBuffer<double>> dict, string slug)
        {
            if (!dict.TryGetValue(slug, out var buffer))
            {
                buffer = new CircularBuffer<double>(30);
                for (int i = 0; i < 30; i++)
                {
                    buffer.Add(0.0);
                }
                dict[slug] = buffer;
            }
            return buffer;
        }

        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            _ = PollMetricsAsync();
        }

        private async Task PollMetricsAsync()
        {
            var selected = SelectedInstance;
            if (selected == null) return;

            // Query process run info
            bool isRunning = _processRunner.TryGetRunningInfo(selected.Slug, out int pgid, out string state);
            
            double cpu = 0.0;
            long ramBytes = 0;
            bool portListens = false;
            int playerCount = 0;
            string playerList = "None";
            string uptimeStr = "00:00:00";
            
            // Port checking
            int port = GetServerPort(selected);
            bool isUdp = (selected.EngineType == EngineType.PocketMine || selected.EngineType == EngineType.Bedrock);
            
            // Check for geyser presence (Geyser*.jar in plugins or mods)
            bool hasGeyser = false;
            bool isJava = selected.EngineType != EngineType.Bedrock && selected.EngineType != EngineType.PocketMine;
            if (isJava)
            {
                string pluginsDir = Path.Combine(selected.Path, "plugins");
                string modsDir = Path.Combine(selected.Path, "mods");
                hasGeyser = (Directory.Exists(pluginsDir) && Directory.GetFiles(pluginsDir, "Geyser*.jar").Any()) ||
                            (Directory.Exists(modsDir) && Directory.GetFiles(modsDir, "Geyser*.jar").Any());
            }

            bool showBedrock = selected.EngineType == EngineType.Bedrock || selected.EngineType == EngineType.PocketMine || hasGeyser;
            bool showJava = isJava;

            if (isRunning && pgid > 0)
            {
                try
                {
                    // CPU and Memory
                    var metrics = _metricsTracker.GetGroupMetrics(pgid);
                    cpu = metrics.CpuPercentage;
                    ramBytes = metrics.MemoryBytes;

                    portListens = PortProber.IsPortListening(port, isUdp);

                    // Online players
                    var players = await _playerService.GetOnlinePlayersAsync(selected);
                    if (players != null && players.Count > 0)
                    {
                        playerCount = players.Count;
                        playerList = string.Join(", ", players);
                    }

                    // Uptime
                    using var proc = Process.GetProcessById(pgid);
                    if (proc != null && !proc.HasExited)
                    {
                        var diff = DateTime.UtcNow - proc.StartTime.ToUniversalTime();
                        uptimeStr = $"{(int)diff.TotalHours:D2}:{diff.Minutes:D2}:{diff.Seconds:D2}";
                    }
                }
                catch
                {
                    // Process exited during query
                }
            }

            // Sync Tunnels data (throttled to avoid HTTP 429 rate limits)
            _pollCount++;
            string? playitSecret = _settingsService.Settings.PlayitPartnerConnection?.SecretKey;
            if (!string.IsNullOrEmpty(playitSecret) && (_cachedActiveTunnels == null || _cachedActiveTunnels.Count == 0 || _pollCount % 15 == 1))
            {
                try
                {
                    var response = await _playitClient.GetTunnelsAsync();
                    if (response != null && response.Success && response.Tunnels != null)
                    {
                        _cachedActiveTunnels = response.Tunnels.Where(t => t.IsEnabled).ToList();
                    }
                }
                catch { /* Ignore API errors during polling */ }
            }
            List<PocketMC.RemoteControl.Tunnels.TunnelData> activeTunnels = _cachedActiveTunnels ?? new();

            // Update Histories
            var cpuBuf = GetOrCreateHistory(_cpuHistories, selected.Slug);
            cpuBuf.Add(cpu);
            var cpuList = cpuBuf.ToList();

            var ramBuf = GetOrCreateHistory(_ramHistories, selected.Slug);
            double totalMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            if (totalMemory <= 0) totalMemory = 16.0 * 1024 * 1024 * 1024;
            double ramPercent = ((double)ramBytes / totalMemory) * 100.0;
            ramPercent = Math.Clamp(ramPercent, 0, 100);
            ramBuf.Add(ramPercent);
            var ramList = ramBuf.ToList();

            Dispatcher.UIThread.Post(() =>
            {
                // Double check selection hasn't changed
                if (SelectedInstance?.Slug == selected.Slug)
                {
                    ServerState = state;
                    CpuUsagePercent = Math.Round(cpu, 1);
                    RamUsageFormatted = FormatBytes(ramBytes);
                    PortStatusFormatted = portListens ? "Listening" : "Offline";
                    ConfiguredPortFormatted = $"{GetServerPort(selected)} ({(selected.EngineType == EngineType.PocketMine || selected.EngineType == EngineType.Bedrock ? "UDP" : "TCP")})";
                    OnlinePlayersCount = playerCount;
                    OnlinePlayersList = playerList;
                    Uptime = uptimeStr;
                    CpuHistory = cpuList;
                    RamHistory = ramList;

                    // IP Setup
                    ShowBedrockIp = showBedrock;
                    
                     if (activeTunnels.Count > 0)
                    {
                        HasPlayitTunnels = true;
                        if (showJava)
                        {
                            var javaTunnel = activeTunnels.FirstOrDefault(t => t.TunnelType == "minecraft-java" && t.Port == port);
                            if (javaTunnel != null)
                            {
                                JavaTunnelAddress = javaTunnel.PublicAddress;
                                ShowJavaTunnel = true;
                            }
                            else
                            {
                                ShowJavaTunnel = false;
                            }
                        }
                        else
                        {
                            ShowJavaTunnel = false;
                        }
                        
                        if (showBedrock)
                        {
                            var bedrockTunnel = activeTunnels.FirstOrDefault(t => t.TunnelType == "minecraft-bedrock");
                            if (bedrockTunnel != null)
                            {
                                BedrockTunnelAddress = bedrockTunnel.PublicAddress;
                                ShowBedrockTunnel = true;
                            }
                            else
                            {
                                ShowBedrockTunnel = false;
                            }
                        }
                        else
                        {
                            ShowBedrockTunnel = false;
                        }

                        var voiceTunnel = activeTunnels.FirstOrDefault(t => t.TunnelType == "mc-simple-voice-chat" || (t.TunnelType != null && t.TunnelType.Contains("voice")));
                        if (voiceTunnel != null)
                        {
                            VoiceChatTunnelAddress = voiceTunnel.PublicAddress;
                            ShowVoiceChatTunnel = true;
                        }
                        else
                        {
                            ShowVoiceChatTunnel = false;
                        }
                    }
                    else
                    {
                        HasPlayitTunnels = false;
                        ShowJavaTunnel = false;
                        ShowBedrockTunnel = false;
                        ShowVoiceChatTunnel = false;
                    }
                    
                    // Lan IP
                    if (isRunning)
                    {
                        HasLanAddress = true;
                        var ips = _localNetworkAddressService.GetLocalIPv4Addresses().ToList();
                        if (ips.Count > 0)
                        {
                            LanAddressDisplayText = $"{ips.First()}:{port}";
                        }
                        else
                        {
                            LanAddressDisplayText = $"127.0.0.1:{port}";
                        }
                    }
                    else
                    {
                        HasLanAddress = false;
                    }
                }
            });
        }

        private string FormatBytes(long bytes)
        {
            if (bytes == 0) return "0 MB";
            double mb = bytes / (1024.0 * 1024.0);
            return $"{mb:F1} MB";
        }

        private int GetServerPort(ServerInstance instance)
        {
            string propPath = Path.Combine(instance.Path, "server.properties");
            if (File.Exists(propPath))
            {
                try
                {
                    var lines = File.ReadAllLines(propPath);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("server-port="))
                        {
                            if (int.TryParse(line.Split('=')[1].Trim(), out int port))
                            {
                                return port;
                            }
                        }
                    }
                }
                catch { }
            }
            return (instance.EngineType == EngineType.PocketMine || instance.EngineType == EngineType.Bedrock) ? 19132 : 25565;
        }

        [RelayCommand(CanExecute = nameof(CanStart))]
        private async Task StartServerAsync()
        {
            if (SelectedInstance != null)
            {
                await _processRunner.StartAsync(SelectedInstance);

                // Auto create / verify Playit tunnels on server start
                if (_playitClient.HasPartnerConnection())
                {
                    _ = CreateOrToggleTunnelsAsync();
                }
                else
                {
                    bool promptConnect = await PocketMC.App.Views.ConfirmationDialogWindow.ShowAsync(
                        "Playit.gg Remote Tunnel",
                        "Would you like to connect your Playit.gg account to automatically expose this server to the internet?",
                        "Connect Account");

                    if (promptConnect)
                    {
                        var wizard = new PocketMC.App.Views.PlayitSetupWizardWindow
                        {
                            DataContext = new PlayitSetupWizardViewModel()
                        };

                        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
                        {
                            await wizard.ShowDialog(desktop.MainWindow);
                            if (_playitClient.HasPartnerConnection())
                            {
                                _ = CreateOrToggleTunnelsAsync();
                            }
                        }
                    }
                }
            }
        }

        private bool CanStart() => SelectedInstance != null && (ServerState == "Stopped" || ServerState == "Crashed");

        [RelayCommand(CanExecute = nameof(CanStop))]
        private async Task StopServerAsync()
        {
            if (SelectedInstance != null)
            {
                await _processRunner.StopAsync(SelectedInstance);
            }
        }

        private bool CanStop() => SelectedInstance != null && ServerState == "Running";

        [RelayCommand]
        private async Task DeleteServerAsync()
        {
            if (SelectedInstance != null)
            {
                var target = SelectedInstance;
                bool confirmed = await PocketMC.App.Views.ConfirmationDialogWindow.ShowAsync(
                    "Delete Server Instance",
                    $"Are you sure you want to delete server instance '{target.Name}'?\nAll world data, configs, and installed plugins will be deleted permanently.",
                    "Delete Server");

                if (!confirmed) return;

                await _instanceService.DeleteInstanceAsync(target.Slug);
                SelectedInstance = null;
                await LoadInstancesAsync();
            }
        }

        [RelayCommand]
        private void ToggleTheme()
        {
            if (ThemeOverride == "System")
            {
                ThemeOverride = "Light";
                _themeManager.ApplyTheme(ThemeMode.Light);
            }
            else if (ThemeOverride == "Light")
            {
                ThemeOverride = "Dark";
                _themeManager.ApplyTheme(ThemeMode.Dark);
            }
            else
            {
                ThemeOverride = "System";
                _themeManager.ApplyTheme(ThemeMode.System);
            }

            _settingsService.Settings.Theme = ThemeOverride;
            _settingsService.Save();
        }

        [RelayCommand]
        private void NavigateToNewInstance()
        {
            var mainVM = App.Services.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
            if (mainVM != null)
            {
                mainVM.CurrentViewModel = App.Services.GetRequiredService<NewInstanceViewModel>();
            }
        }

        [RelayCommand]
        private void NavigateToConsole()
        {
            if (SelectedInstance == null) return;
            var mainVM = App.Services.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
            if (mainVM != null)
            {
                var consoleVM = App.Services.GetRequiredService<ServerConsoleViewModel>();
                consoleVM.Initialize(SelectedInstance);
                mainVM.CurrentViewModel = consoleVM;
            }
        }

        [RelayCommand]
        private void NavigateToSettings()
        {
            if (SelectedInstance == null) return;
            var mainVM = App.Services.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
            if (mainVM != null)
            {
                var settingsVM = App.Services.GetRequiredService<ServerSettingsViewModel>();
                settingsVM.Initialize(SelectedInstance);
                mainVM.CurrentViewModel = settingsVM;
            }
        }

        [RelayCommand]
        private void NavigateToPlayers()
        {
            if (SelectedInstance == null) return;
            var mainVM = App.Services.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
            if (mainVM != null)
            {
                var playersVM = App.Services.GetRequiredService<PlayerManagementViewModel>();
                playersVM.Initialize(SelectedInstance);
                mainVM.CurrentViewModel = playersVM;
            }
        }

        [RelayCommand]
        private void NavigateToMarketplace()
        {
            if (SelectedInstance == null) return;
            var mainVM = App.Services.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
            if (mainVM != null)
            {
                var marketplaceVM = App.Services.GetRequiredService<MarketplaceViewModel>();
                marketplaceVM.Initialize(SelectedInstance);
                mainVM.CurrentViewModel = marketplaceVM;
            }
        }

        [RelayCommand]
        private void NavigateToBackups()
        {
            if (SelectedInstance == null) return;
            var mainVM = App.Services.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
            if (mainVM != null)
            {
                var backupsVM = App.Services.GetRequiredService<ServerBackupsViewModel>();
                backupsVM.Initialize(SelectedInstance);
                mainVM.CurrentViewModel = backupsVM;
            }
        }

        [RelayCommand]
        private void NavigateToTunnels()
        {
            var mainVM = App.Services.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
            if (mainVM != null)
            {
                var tunnelsVM = App.Services.GetRequiredService<TunnelViewModel>();
                _ = tunnelsVM.RefreshAllAsync();
                mainVM.CurrentViewModel = tunnelsVM;
            }
        }

        [RelayCommand]
        private void NavigateToRemoteControl()
        {
            var mainVM = App.Services.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
            if (mainVM != null)
            {
                mainVM.CurrentViewModel = App.Services.GetRequiredService<RemoteControlViewModel>();
            }
        }

        [RelayCommand]
        private void NavigateToJavaManagement()
        {
            var mainVM = App.Services.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
            if (mainVM != null)
            {
                mainVM.CurrentViewModel = App.Services.GetRequiredService<JavaManagementViewModel>();
            }
        }

        [RelayCommand]
        private void NavigateToAbout()
        {
            var mainVM = App.Services.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
            if (mainVM != null)
            {
                mainVM.CurrentViewModel = App.Services.GetRequiredService<AboutViewModel>();
            }
        }

        [RelayCommand]
        private async Task CopyAddressAsync(string? address)
        {
            if (string.IsNullOrWhiteSpace(address)) return;
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(desktop.MainWindow);
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(address);
                }
            }
        }

        [RelayCommand]
        private void OpenFolder(ServerInstance? instance)
        {
            var target = instance ?? SelectedInstance;
            if (target != null && Directory.Exists(target.Path))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = OperatingSystem.IsMacOS() ? "open" : "xdg-open",
                        Arguments = target.Path,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    try
                    {
                        Process.Start(OperatingSystem.IsMacOS() ? "open" : "xdg-open", target.Path);
                    }
                    catch { }
                }
            }
        }

        [RelayCommand]
        private async Task CreateOrToggleTunnelsAsync()
        {
            if (SelectedInstance == null) return;
            var selected = SelectedInstance;

            bool isLinked = _playitClient.HasPartnerConnection();
            if (!isLinked)
            {
                var wizard = new PocketMC.App.Views.PlayitSetupWizardWindow
                {
                    DataContext = new PlayitSetupWizardViewModel()
                };

                if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
                {
                    await wizard.ShowDialog(desktop.MainWindow);
                    isLinked = _playitClient.HasPartnerConnection();
                }

                if (!isLinked) return;
            }

            IsCreatingTunnels = true;
            try
            {
                await _tunnelManager.StartTunnelAsync("playit-https");
                await _playitClient.EnsureAgentIdAsync();

                // Fetch existing tunnels to prevent duplicates
                var listResponse = await _playitClient.GetTunnelsAsync();
                var existingTunnels = listResponse?.Tunnels ?? new List<PocketMC.RemoteControl.Tunnels.TunnelData>();

                int port = GetServerPort(selected);
                bool isJava = selected.EngineType != EngineType.Bedrock && selected.EngineType != EngineType.PocketMine;

                bool HasTunnel(string type, int p)
                {
                    return existingTunnels.Any(t =>
                        string.Equals(t.TunnelType, type, StringComparison.OrdinalIgnoreCase) &&
                        (t.Port == p || t.Name?.Equals($"{selected.Name}-{(type.Contains("java") ? "java" : "bedrock")}", StringComparison.OrdinalIgnoreCase) == true));
                }

                // 1. Create Java or Bedrock tunnel if not present
                if (isJava)
                {
                    if (!HasTunnel("minecraft-java", port))
                    {
                        await _playitClient.CreateTunnelAsync($"{selected.Name}-java", "minecraft-java", port);
                    }
                }
                else
                {
                    if (!HasTunnel("minecraft-bedrock", port))
                    {
                        await _playitClient.CreateTunnelAsync($"{selected.Name}-bedrock", "minecraft-bedrock", port);
                    }
                }

                // 2. Check for Geyser if Java server
                if (isJava)
                {
                    string pluginsDir = Path.Combine(selected.Path, "plugins");
                    string modsDir = Path.Combine(selected.Path, "mods");
                    bool hasGeyser = (Directory.Exists(pluginsDir) && Directory.GetFiles(pluginsDir, "*Geyser*.jar", SearchOption.TopDirectoryOnly).Any()) ||
                                     (Directory.Exists(modsDir) && Directory.GetFiles(modsDir, "*Geyser*.jar", SearchOption.TopDirectoryOnly).Any());
                    if (hasGeyser && !HasTunnel("minecraft-bedrock", 19132))
                    {
                        await _playitClient.CreateTunnelAsync($"{selected.Name}-bedrock", "minecraft-bedrock", 19132);
                    }

                    // 3. Check for Simple Voice Chat
                    bool hasVoiceChat = (Directory.Exists(pluginsDir) && Directory.GetFiles(pluginsDir, "*voice*chat*.jar", SearchOption.AllDirectories).Any()) ||
                                         (Directory.Exists(modsDir) && Directory.GetFiles(modsDir, "*voice*chat*.jar", SearchOption.AllDirectories).Any());
                    if (hasVoiceChat && !HasTunnel("mc-simple-voice-chat", 24454))
                    {
                        await _playitClient.CreateTunnelAsync($"{selected.Name}-voicechat", "mc-simple-voice-chat", 24454);
                    }
                }

                _cachedActiveTunnels.Clear();
                _pollCount = 0;
                await PollMetricsAsync();
            }
            catch { }
            finally
            {
                IsCreatingTunnels = false;
            }
        }

        public void Dispose()
        {
            _timer.Dispose();
            _processRunner.StateChanged -= OnProcessStateChanged;
            GC.SuppressFinalize(this);
        }
    }
}
