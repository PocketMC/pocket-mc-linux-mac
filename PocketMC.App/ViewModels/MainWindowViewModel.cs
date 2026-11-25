using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.App.Navigation;
using PocketMC.Core.Services;

namespace PocketMC.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IShellHost, IDisposable
{
    private readonly IAppNavigationService _navigationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISettingsService _settingsService;
    private readonly INativeMetricsProvider? _metricsProvider;
    private readonly System.Timers.Timer? _metricsTimer;

    [ObservableProperty]
    private object _currentViewModel;

    [ObservableProperty]
    private string _currentBreadcrumb = "Dashboard";

    [ObservableProperty]
    private bool _canNavigateBack;

    [ObservableProperty]
    private bool _canNavigateForward;

    [ObservableProperty]
    private string? _titleBarTitle;

    [ObservableProperty]
    private string? _titleBarStatusText;

    [ObservableProperty]
    private string? _titleBarStatusColor = "#2ECC71";

    [ObservableProperty]
    private bool _isTitleBarContextVisible;

    [ObservableProperty]
    private string? _globalHealthStatusText = "System RAM: Calculating...";

    [ObservableProperty]
    private string _globalHealthStatusColor = "#D4D4D8";

    [ObservableProperty]
    private bool _isGlobalHealthVisible = true;

    [ObservableProperty]
    private bool _isUpdateBannerVisible;

    [ObservableProperty]
    private string? _updateStatusMessage;

    [ObservableProperty]
    private bool _isUpdateDownloading;

    [ObservableProperty]
    private double _updateDownloadPercent;

    [ObservableProperty]
    private bool _isUpdateReadyToApply;

    [ObservableProperty]
    private int _unreadNewsCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SidebarWidth))]
    private bool _isPaneOpen = true;

    public double SidebarWidth => IsPaneOpen ? 224 : 56;

    [ObservableProperty]
    private bool _isWallpaperActive;

    public event Action? BackdropRefreshRequested;

    public void RequestBackdropRefresh() => BackdropRefreshRequested?.Invoke();

    [ObservableProperty]
    private string _selectedNavSection = "Dashboard";

    public bool IsDashboardSelected => SelectedNavSection == "Dashboard";
    public bool IsTunnelSelected => SelectedNavSection == "Tunnel";
    public bool IsRemoteControlSelected => SelectedNavSection == "RemoteControl";
    public bool IsNewsSelected => SelectedNavSection == "News";
    public bool IsSettingsSelected => SelectedNavSection == "Settings";
    public bool IsRuntimesSelected => SelectedNavSection == "Runtimes";
    public bool IsAboutSelected => SelectedNavSection == "About";

    partial void OnSelectedNavSectionChanged(string value)
    {
        OnPropertyChanged(nameof(IsDashboardSelected));
        OnPropertyChanged(nameof(IsTunnelSelected));
        OnPropertyChanged(nameof(IsRemoteControlSelected));
        OnPropertyChanged(nameof(IsNewsSelected));
        OnPropertyChanged(nameof(IsSettingsSelected));
        OnPropertyChanged(nameof(IsRuntimesSelected));
        OnPropertyChanged(nameof(IsAboutSelected));
    }

    private readonly PocketMC.Infrastructure.News.NewsService? _newsService;
    private bool _disposed;

    public bool HasUnreadNews => UnreadNewsCount > 0;

    partial void OnUnreadNewsCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasUnreadNews));
    }

    public MainWindowViewModel(
        IAppNavigationService navigationService,
        IServiceProvider serviceProvider,
        ISettingsService settingsService,
        DashboardViewModel dashboardViewModel,
        INativeMetricsProvider? metricsProvider = null)
    {
        _navigationService = navigationService;
        _serviceProvider = serviceProvider;
        _settingsService = settingsService;
        _currentViewModel = dashboardViewModel;
        _metricsProvider = metricsProvider ?? serviceProvider.GetService(typeof(INativeMetricsProvider)) as INativeMetricsProvider;
        _newsService = serviceProvider.GetService(typeof(PocketMC.Infrastructure.News.NewsService)) as PocketMC.Infrastructure.News.NewsService;

        _navigationService.Initialize(this);
        _navigationService.NavigationChanged += OnNavigationChanged;
        _navigationService.NavigateToDetailPage(dashboardViewModel, "Dashboard", DetailRouteKind.Dashboard, DetailBackNavigation.Dashboard, clearDetailStack: true);

        if (_newsService != null)
        {
            _newsService.NewsStateChanged += OnNewsStateChanged;
            RefreshUnreadNewsCount();
        }

        UpdateSystemMemoryMetrics();
        _metricsTimer = new System.Timers.Timer(2500);
        _metricsTimer.Elapsed += (s, e) => UpdateSystemMemoryMetrics();
        _metricsTimer.AutoReset = true;
        _metricsTimer.Start();
    }

    private void OnNewsStateChanged() => RefreshUnreadNewsCount();

    public void RefreshUnreadNewsCount()
    {
        if (_newsService == null || _disposed) return;

        int unread = _newsService.GetUnreadNewsCount();
        if (Avalonia.Application.Current == null || Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            UnreadNewsCount = unread;
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (!_disposed)
                {
                    UnreadNewsCount = unread;
                }
            });
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _metricsTimer?.Stop();
        _metricsTimer?.Dispose();
        _navigationService.NavigationChanged -= OnNavigationChanged;
        if (_newsService != null)
        {
            _newsService.NewsStateChanged -= OnNewsStateChanged;
        }
    }

    private void UpdateSystemMemoryMetrics()
    {
        if (_metricsProvider == null) return;
        try
        {
            var (totalBytes, availBytes) = _metricsProvider.GetSystemMemoryMetrics();
            long usedBytes = Math.Max(0, totalBytes - availBytes);
            double usedGb = Math.Round(usedBytes / (1024.0 * 1024.0 * 1024.0), 1);
            double totalGb = Math.Round(totalBytes / (1024.0 * 1024.0 * 1024.0), 1);
            bool isHighUsage = totalBytes > 0 && ((double)usedBytes / totalBytes) > 0.9;

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                GlobalHealthStatusText = $"System RAM: {usedGb:0.0} / {totalGb:0.0} GB";
                GlobalHealthStatusColor = isHighUsage ? "#C42B1C" : "#D4D4D8";
            });
        }
        catch
        {
            // Ignore metrics sampling exception
        }
    }

    private void OnNavigationChanged()
    {
        CanNavigateBack = _navigationService.CanNavigateBack;
        CanNavigateForward = _navigationService.CanNavigateForward;
        CurrentBreadcrumb = _navigationService.CurrentBreadcrumb ?? "Dashboard";

        if (_navigationService.CurrentRouteKind.HasValue)
        {
            var kind = _navigationService.CurrentRouteKind.Value;
            SelectedNavSection = kind switch
            {
                DetailRouteKind.Dashboard => "Dashboard",
                DetailRouteKind.Tunnel => "Tunnel",
                DetailRouteKind.RemoteControl => "RemoteControl",
                DetailRouteKind.News => "News",
                DetailRouteKind.Settings => "Settings",
                DetailRouteKind.Runtimes => "Runtimes",
                DetailRouteKind.About => "About",
                _ => SelectedNavSection
            };
        }
    }

    public void SetContent(object page)
    {
        CurrentViewModel = page;
    }

    public void UpdateBreadcrumb(string breadcrumb)
    {
        CurrentBreadcrumb = breadcrumb;
    }

    public void SetTitleBarContext(string? title, string? subtitle, string? statusDotColor)
    {
        TitleBarTitle = title;
        TitleBarStatusText = subtitle;
        TitleBarStatusColor = statusDotColor ?? "#2ECC71";
        IsTitleBarContextVisible = !string.IsNullOrEmpty(title);
    }

    [RelayCommand]
    public void NavigateDashboard()
    {
        SelectedNavSection = "Dashboard";
        var vm = (DashboardViewModel)_serviceProvider.GetService(typeof(DashboardViewModel))! ?? (DashboardViewModel)CurrentViewModel;
        _navigationService.NavigateToDetailPage(vm, "Dashboard", DetailRouteKind.Dashboard, DetailBackNavigation.Dashboard, clearDetailStack: true);
    }

    [RelayCommand]
    public void NavigateTunnel()
    {
        SelectedNavSection = "Tunnel";
        var vm = _serviceProvider.GetService(typeof(TunnelViewModel));
        if (vm != null)
        {
            _navigationService.NavigateToDetailPage(vm, "Tunnel", DetailRouteKind.Tunnel, DetailBackNavigation.Dashboard, clearDetailStack: true);
        }
    }

    [RelayCommand]
    public void NavigateRemoteControl()
    {
        SelectedNavSection = "RemoteControl";
        var vm = _serviceProvider.GetService(typeof(RemoteControlViewModel));
        if (vm != null)
        {
            _navigationService.NavigateToDetailPage(vm, "Remote Control", DetailRouteKind.RemoteControl, DetailBackNavigation.Dashboard, clearDetailStack: true);
        }
    }

    [RelayCommand]
    public void NavigateSettings()
    {
        SelectedNavSection = "Settings";
        var vm = _serviceProvider.GetService(typeof(AppSettingsViewModel));
        if (vm != null)
        {
            _navigationService.NavigateToDetailPage(vm, "Settings", DetailRouteKind.Settings, DetailBackNavigation.Dashboard);
        }
    }

    [RelayCommand]
    public void NavigateRuntimes()
    {
        SelectedNavSection = "Runtimes";
        var vm = _serviceProvider.GetService(typeof(JavaManagementViewModel));
        if (vm != null)
        {
            _navigationService.NavigateToDetailPage(vm, "Runtimes", DetailRouteKind.Runtimes, DetailBackNavigation.Dashboard);
        }
    }

    [RelayCommand]
    public void NavigateAbout()
    {
        SelectedNavSection = "About";
        var vm = _serviceProvider.GetService(typeof(AboutViewModel));
        if (vm != null)
        {
            _navigationService.NavigateToDetailPage(vm, "About", DetailRouteKind.About, DetailBackNavigation.Dashboard);
        }
    }

    [RelayCommand]
    public void NavigateNews()
    {
        SelectedNavSection = "News";
        var newsVm = (NewsViewModel?)_serviceProvider.GetService(typeof(NewsViewModel));
        if (newsVm != null)
        {
            _navigationService.NavigateToDetailPage(newsVm, "Community News", DetailRouteKind.News, DetailBackNavigation.Dashboard);
        }
        else
        {
            // Fallback: Shows What's New changelog dialog if NewsViewModel is not available
            var whatsNewService = (Infrastructure.WhatsNew.WhatsNewService?)_serviceProvider.GetService(typeof(Infrastructure.WhatsNew.WhatsNewService));
            if (whatsNewService != null)
            {
                var window = new Views.WhatsNewWindow(whatsNewService);
                if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
                {
                    window.ShowDialog(desktop.MainWindow);
                }
                else
                {
                    window.Show();
                }
            }
        }
    }

    [RelayCommand]
    public void RestartAndApplyUpdate()
    {
        // Dismiss banner and restart application
        IsUpdateBannerVisible = false;
    }

    [RelayCommand]
    public void DismissUpdateBanner()
    {
        IsUpdateBannerVisible = false;
    }

    [RelayCommand]
    public void TogglePane()
    {
        IsPaneOpen = !IsPaneOpen;
    }

    [RelayCommand]
    public void NavigateBack()
    {
        _navigationService.NavigateBack();
    }

    [RelayCommand]
    public void NavigateForward()
    {
        _navigationService.NavigateForward();
    }
}
