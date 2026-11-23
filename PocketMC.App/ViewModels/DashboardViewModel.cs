using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PocketMC.App.Navigation;
using PocketMC.App.Views;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;
using PocketMC.Platform.Metrics;
using PocketMC.RemoteControl.Services;
using PocketMC.RemoteControl.Tunnels;

namespace PocketMC.App.ViewModels
{
    public partial class DashboardViewModel : ObservableObject, IDisposable
    {
        private readonly IInstanceService _instanceService;
        private readonly IProcessRunner _processRunner;
        private readonly IPlayerService _playerService;
        private readonly ProcessMetricsTracker _metricsTracker;
        private readonly INativeMetricsProvider _metricsProvider;
        private readonly PlayitApiClient _playitClient;
        private readonly LocalNetworkAddressService _localNetworkAddressService;
        private readonly ISettingsService _settingsService;
        private readonly IDialogService? _dialogService;
        private readonly IAppNavigationService? _navigationService;
        private readonly IServiceProvider? _serviceProvider;
        private readonly IGeyserDetector _geyserDetector;
        private readonly ISimpleVoiceChatDetector _voiceChatDetector;
        private readonly System.Timers.Timer _timer;

        private List<TunnelData> _cachedActiveTunnels = new();
        private int _pollCount = 0;

        public DashboardInstanceListViewModel ListVm { get; }
        public DashboardMetricsViewModel MetricsVm { get; }
        public DashboardActionsViewModel ActionsVm { get; }

        public ObservableCollection<InstanceCardViewModel> Instances => ListVm.Instances;
        public bool IsLoading => ListVm.IsLoading;
        public bool HasInstances => Instances.Count > 0;
        public string ServerState => SelectedInstance?.StatusText ?? "Stopped";

        public async Task LoadInstancesAsync()
        {
            await ListVm.LoadInstancesAsync();
            if (SelectedInstance == null && Instances.Count > 0)
            {
                SelectedInstance = Instances[0];
            }
            OnPropertyChanged(nameof(HasInstances));
            OnPropertyChanged(nameof(ServerState));
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ServerState))]
        private InstanceCardViewModel? _selectedInstance;

        public IAsyncRelayCommand NewInstanceCommand { get; }
        public IAsyncRelayCommand RefreshInstancesCommand { get; }
        public IAsyncRelayCommand<InstanceCardViewModel> StartServerCommand { get; }
        public IAsyncRelayCommand<InstanceCardViewModel> StopServerCommand { get; }
        public IAsyncRelayCommand<InstanceCardViewModel> RestartServerCommand { get; }
        public IAsyncRelayCommand<InstanceCardViewModel> DeleteInstanceCommand { get; }
        public IRelayCommand<InstanceCardViewModel> OpenFolderCommand { get; }
        public IAsyncRelayCommand<InstanceCardViewModel> CopyCrashReportCommand { get; }
        public IRelayCommand<InstanceCardViewModel> ExportInstanceCommand { get; }
        public IRelayCommand<InstanceCardViewModel> ServerSettingsCommand { get; }
        public IRelayCommand<InstanceCardViewModel> OpenConsoleCommand { get; }
        public IRelayCommand<InstanceCardViewModel> OpenPlayersCommand { get; }
        public IAsyncRelayCommand<InstanceCardViewModel> TogglePinCommand { get; }
        public IAsyncRelayCommand<InstanceCardViewModel> CopyIpCommand { get; }
        public IAsyncRelayCommand<InstanceCardViewModel> CopyLanIpCommand { get; }
        public IAsyncRelayCommand<InstanceCardViewModel> CopyNumericIpCommand { get; }
        public IAsyncRelayCommand<InstanceCardViewModel> CopyBedrockIpCommand { get; }

        public DashboardViewModel(
            IInstanceService instanceService,
            IProcessRunner processRunner,
            IPlayerService playerService,
            ProcessMetricsTracker metricsTracker,
            INativeMetricsProvider metricsProvider,
            PlayitApiClient playitClient,
            LocalNetworkAddressService localNetworkAddressService,
            ISettingsService settingsService,
            IDialogService? dialogService = null,
            IAppNavigationService? navigationService = null,
            IServiceProvider? serviceProvider = null,
            DashboardInstanceListViewModel? listVm = null,
            DashboardMetricsViewModel? metricsVm = null,
            DashboardActionsViewModel? actionsVm = null,
            IGeyserDetector? geyserDetector = null,
            ISimpleVoiceChatDetector? voiceChatDetector = null)
        {
            _instanceService = instanceService;
            _processRunner = processRunner;
            _playerService = playerService;
            _metricsTracker = metricsTracker;
            _metricsProvider = metricsProvider;
            _playitClient = playitClient;
            _localNetworkAddressService = localNetworkAddressService;
            _settingsService = settingsService;
            _dialogService = dialogService;
            _navigationService = navigationService;
            _serviceProvider = serviceProvider;
            _geyserDetector = geyserDetector ?? new GeyserDetector(new AddonManifestService());
            _voiceChatDetector = voiceChatDetector ?? new SimpleVoiceChatDetector(new AddonManifestService());

            ListVm = listVm ?? new DashboardInstanceListViewModel(instanceService);
            ListVm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(DashboardInstanceListViewModel.IsLoading))
                {
                    OnPropertyChanged(nameof(IsLoading));
                    OnPropertyChanged(nameof(HasInstances));
                }
            };

            MetricsVm = metricsVm ?? new DashboardMetricsViewModel(metricsProvider);

            ActionsVm = actionsVm ?? new DashboardActionsViewModel(
                processRunner,
                instanceService,
                dialogService,
                onOpenSettings: NavigateToSettings,
                onOpenConsole: NavigateToConsole,
                onOpenPlayers: NavigateToPlayers,
                onOpenExport: NavigateToExport,
                navigationService: _navigationService,
                serviceProvider: _serviceProvider);

            ActionsVm.SetCallbacks(
                onOpenSettings: NavigateToSettings,
                onOpenConsole: NavigateToConsole,
                onOpenPlayers: NavigateToPlayers,
                onOpenExport: NavigateToExport);

            NewInstanceCommand = new AsyncRelayCommand(NavigateToNewInstance);
            RefreshInstancesCommand = new AsyncRelayCommand(async () => await ListVm.LoadInstancesAsync());
            StartServerCommand = new AsyncRelayCommand<InstanceCardViewModel>(async card => { if (card != null) await ActionsVm.StartServerAsync(card); });
            StopServerCommand = new AsyncRelayCommand<InstanceCardViewModel>(async card => { if (card != null) await ActionsVm.StopServerAsync(card); });
            RestartServerCommand = new AsyncRelayCommand<InstanceCardViewModel>(async card => { if (card != null) await ActionsVm.RestartServerAsync(card); });
            DeleteInstanceCommand = new AsyncRelayCommand<InstanceCardViewModel>(async card =>
            {
                if (card != null)
                {
                    bool deleted = await ActionsVm.DeleteInstanceAsync(card);
                    if (deleted)
                    {
                        Instances.Remove(card);
                        OnPropertyChanged(nameof(HasInstances));
                    }
                }
            });
            OpenFolderCommand = new RelayCommand<InstanceCardViewModel>(card => { if (card != null) ActionsVm.OpenFolder(card); });
            CopyCrashReportCommand = new AsyncRelayCommand<InstanceCardViewModel>(async card => { if (card != null) await ActionsVm.CopyCrashReportAsync(card); });
            ExportInstanceCommand = new RelayCommand<InstanceCardViewModel>(card => { if (card != null) ActionsVm.OpenExport(card); });
            ServerSettingsCommand = new RelayCommand<InstanceCardViewModel>(card => { if (card != null) ActionsVm.OpenSettings(card); });
            OpenConsoleCommand = new RelayCommand<InstanceCardViewModel>(card => { if (card != null) ActionsVm.OpenConsole(card); });
            OpenPlayersCommand = new RelayCommand<InstanceCardViewModel>(card => { if (card != null) ActionsVm.OpenPlayers(card); });
            TogglePinCommand = new AsyncRelayCommand<InstanceCardViewModel>(async card =>
            {
                if (card != null)
                {
                    await ActionsVm.TogglePinAsync(card);
                    await ListVm.LoadInstancesAsync();
                }
            });

            CopyIpCommand = new AsyncRelayCommand<InstanceCardViewModel>(async card =>
            {
                if (card?.TunnelAddress != null) await CopyToClipboardAsync(card.TunnelAddress);
            });
            CopyLanIpCommand = new AsyncRelayCommand<InstanceCardViewModel>(async card =>
            {
                if (card?.LanAddressDisplayText != null) await CopyToClipboardAsync(card.LanAddressDisplayText);
            });
            CopyNumericIpCommand = new AsyncRelayCommand<InstanceCardViewModel>(async card =>
            {
                if (card?.NumericTunnelAddress != null) await CopyToClipboardAsync(card.NumericTunnelAddress);
            });
            CopyBedrockIpCommand = new AsyncRelayCommand<InstanceCardViewModel>(async card =>
            {
                if (card?.BedrockTunnelAddress != null) await CopyToClipboardAsync(card.BedrockTunnelAddress);
            });

            _processRunner.StateChanged += OnProcessStateChanged;

            _timer = new System.Timers.Timer(2000);
            _timer.Elapsed += OnTimerElapsed;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await ListVm.LoadInstancesAsync();
            if (SelectedInstance == null && Instances.Count > 0)
            {
                SelectedInstance = Instances[0];
            }
            OnPropertyChanged(nameof(HasInstances));
            OnPropertyChanged(nameof(ServerState));
            _timer.Start();
        }

        private void OnProcessStateChanged(string slug, string state)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var card = Instances.FirstOrDefault(c => c.Instance.Slug == slug);
                if (card != null)
                {
                    DateTime? sessionStart = _processRunner.GetSessionStartTime(slug);
                    card.UpdateState(state, sessionStart);
                    OnPropertyChanged(nameof(ServerState));
                }
            });
        }

        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            _ = PollDashboardStateAsync();
        }

        private async Task PollDashboardStateAsync()
        {
            _pollCount++;

            // Throttled tunnel sync
            string? playitSecret = _settingsService.Settings.PlayitPartnerConnection?.SecretKey;
            if (!string.IsNullOrEmpty(playitSecret) && (_cachedActiveTunnels.Count == 0 || _pollCount % 15 == 1))
            {
                try
                {
                    var response = await _playitClient.GetTunnelsAsync();
                    if (response?.Success == true && response.Tunnels != null)
                    {
                        _cachedActiveTunnels = response.Tunnels.Where(t => t.IsEnabled).ToList();
                    }
                }
                catch { /* Ignore API errors during background polling */ }
            }

            foreach (var card in Instances.ToList())
            {
                bool isRunning = _processRunner.TryGetRunningInfo(card.Instance.Slug, out int pgid, out string state);
                if (isRunning && pgid > 0)
                {
                    int playerCount = 0;
                    try
                    {
                        var players = await _playerService.GetOnlinePlayersAsync(card.Instance);
                        playerCount = players?.Count ?? 0;
                    }
                    catch { /* Process might be starting or stopping */ }

                    MetricsVm.ApplyLiveMetrics(card, pgid, playerCount);

                    // Sync tunnels
                    string? javaTunnel = null;
                    string? numericTunnel = null;
                    string? bedrockTunnel = null;
                    string? bedrockNumericTunnel = null;
                    string? lanAddress = null;

                    int port = card.Instance.ServerPort ?? 25565;
                    var localIp = _localNetworkAddressService.GetLocalIPv4Addresses().FirstOrDefault();
                    if (!string.IsNullOrEmpty(localIp))
                    {
                        lanAddress = $"{localIp}:{port}";
                    }

                    if (_cachedActiveTunnels.Count > 0)
                    {
                        var match = _cachedActiveTunnels.FirstOrDefault(t => t.Port == port);
                        if (match != null)
                        {
                            javaTunnel = match.PublicAddress;
                            numericTunnel = match.NumericAddress;
                        }
                    }

                    card.UpdateTunnels(javaTunnel, numericTunnel, bedrockTunnel, bedrockNumericTunnel, lanAddress);
                }
                else
                {
                    card.NotifyUptimeChanged();
                }
            }
        }

        private Task NavigateToNewInstance()
        {
            if (_serviceProvider == null || _navigationService == null) return Task.CompletedTask;
            var vm = _serviceProvider.GetRequiredService<NewInstanceViewModel>();
            _navigationService.NavigateToDetailPage(
                vm,
                "New Instance",
                DetailRouteKind.NewInstance,
                DetailBackNavigation.Dashboard);
            return Task.CompletedTask;
        }

        private void NavigateToSettings(InstanceCardViewModel card)
        {
            if (_serviceProvider == null || _navigationService == null) return;
            var vm = _serviceProvider.GetRequiredService<ServerSettingsViewModel>();
            vm.Initialize(card.Instance);
            _navigationService.NavigateToDetailPage(
                vm,
                $"{card.Name} Settings",
                DetailRouteKind.ServerSettings,
                DetailBackNavigation.Dashboard,
                clearDetailStack: true);
        }

        private void NavigateToConsole(InstanceCardViewModel card)
        {
            if (_serviceProvider == null || _navigationService == null) return;
            var vm = _serviceProvider.GetRequiredService<ServerConsoleViewModel>();
            vm.Initialize(card.Instance);
            _navigationService.NavigateToDetailPage(
                vm,
                $"{card.Name} Console",
                DetailRouteKind.ServerConsole,
                DetailBackNavigation.Dashboard,
                clearDetailStack: true);
        }

        private void NavigateToPlayers(InstanceCardViewModel card)
        {
            if (_serviceProvider == null || _navigationService == null) return;
            var vm = _serviceProvider.GetRequiredService<PlayerManagementViewModel>();
            vm.Initialize(card.Instance);
            _navigationService.NavigateToDetailPage(
                vm,
                $"{card.Name} Players",
                DetailRouteKind.PlayerManagement,
                DetailBackNavigation.Dashboard,
                clearDetailStack: true);
        }

        private void NavigateToExport(InstanceCardViewModel card)
        {
            if (_serviceProvider == null || _navigationService == null) return;
            var vm = new PocketMC.App.ViewModels.Instances.InstanceExportViewModel(card.Instance, _instanceService, _dialogService);
            _navigationService.NavigateToDetailPage(
                vm,
                $"{card.Name} Export",
                Navigation.DetailRouteKind.InstanceExport,
                Navigation.DetailBackNavigation.Dashboard,
                clearDetailStack: true);
        }

        [RelayCommand]
        public void NavigateToImport()
        {
            if (_serviceProvider == null || _navigationService == null) return;
            var vm = new PocketMC.App.ViewModels.Instances.InstanceImportViewModel(_instanceService, _dialogService);
            _navigationService.NavigateToDetailPage(
                vm,
                "Import Instance",
                Navigation.DetailRouteKind.InstanceImport,
                Navigation.DetailBackNavigation.Dashboard);
        }

        private static async Task CopyToClipboardAsync(string text)
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.Clipboard != null)
            {
                await desktop.MainWindow.Clipboard.SetTextAsync(text);
            }
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
            _processRunner.StateChanged -= OnProcessStateChanged;
        }
    }
}
