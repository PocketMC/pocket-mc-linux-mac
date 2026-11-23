using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.App.Navigation;
using PocketMC.Core.Services;

namespace PocketMC.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IShellHost
{
    private readonly IAppNavigationService _navigationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISettingsService _settingsService;

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
    private string? _globalHealthStatusText = "Host Healthy";

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
    private string _selectedNavSection = "Dashboard";

    public MainWindowViewModel(
        IAppNavigationService navigationService,
        IServiceProvider serviceProvider,
        ISettingsService settingsService,
        DashboardViewModel dashboardViewModel)
    {
        _navigationService = navigationService;
        _serviceProvider = serviceProvider;
        _settingsService = settingsService;
        _currentViewModel = dashboardViewModel;

        _navigationService.Initialize(this);
        _navigationService.NavigationChanged += OnNavigationChanged;
    }

    private void OnNavigationChanged()
    {
        CanNavigateBack = _navigationService.CanNavigateBack;
        CanNavigateForward = _navigationService.CanNavigateForward;
        CurrentBreadcrumb = _navigationService.CurrentBreadcrumb ?? "Dashboard";

        if (_navigationService.CurrentRouteKind.HasValue)
        {
            SelectedNavSection = _navigationService.CurrentRouteKind.Value.ToString();
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
            _navigationService.NavigateToDetailPage(vm, "Remote Control", DetailRouteKind.Tunnel, DetailBackNavigation.Dashboard, clearDetailStack: true);
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
            _navigationService.NavigateToDetailPage(vm, "Runtimes", DetailRouteKind.Settings, DetailBackNavigation.Dashboard);
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
