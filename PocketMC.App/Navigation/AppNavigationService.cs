using System;
using PocketMC.App.Interfaces;

namespace PocketMC.App.Navigation;

public sealed class AppNavigationService : IAppNavigationService
{
    private readonly IServiceProvider? _serviceProvider;
    private readonly ControlledNavigationStack _stack = new();
    private IShellHost? _shellHost;

    public event Action? NavigationChanged;

    public bool CanNavigateBack => _stack.CanNavigateBack;
    public bool CanNavigateForward => _stack.CanNavigateForward;
    public string? CurrentBreadcrumb => _stack.Current?.Breadcrumb;
    public DetailRouteKind? CurrentRouteKind => _stack.Current?.RouteKind;

    public AppNavigationService(IServiceProvider? serviceProvider = null)
    {
        _serviceProvider = serviceProvider;
    }

    public void Initialize(IShellHost shellHost)
    {
        _shellHost = shellHost;
    }

    public bool NavigateToDashboard()
    {
        if (_shellHost == null) return false;
        object page = "Dashboard";
        if (_serviceProvider != null)
        {
            var vm = _serviceProvider.GetService(typeof(ViewModels.DashboardViewModel));
            if (vm != null) page = vm;
        }
        _stack.Push(page, "Dashboard", DetailRouteKind.Dashboard, DetailBackNavigation.Dashboard, clearDetailStack: true);
        ApplyCurrent();
        return true;
    }

    public bool NavigateToTunnel()
    {
        if (_shellHost == null) return false;
        object page = "Tunnel";
        if (_serviceProvider != null)
        {
            var vm = _serviceProvider.GetService(typeof(ViewModels.TunnelViewModel));
            if (vm != null) page = vm;
        }
        _stack.Push(page, "Tunnel", DetailRouteKind.Tunnel, DetailBackNavigation.Dashboard, clearDetailStack: true);
        ApplyCurrent();
        return true;
    }

    public bool NavigateToNews()
    {
        if (_shellHost == null) return false;
        object page = "News";
        if (_serviceProvider != null)
        {
            var vm = _serviceProvider.GetService(typeof(ViewModels.NewsViewModel));
            if (vm != null) page = vm;
        }
        _stack.Push(page, "Community News", DetailRouteKind.News, DetailBackNavigation.Dashboard);
        ApplyCurrent();
        return true;
    }

    public bool NavigateToDetailPage(
        object page,
        string breadcrumbLabel,
        DetailRouteKind routeKind,
        DetailBackNavigation backNavigation,
        bool clearDetailStack = false)
    {
        if (_shellHost == null) return false;

        _stack.Push(page, breadcrumbLabel, routeKind, backNavigation, clearDetailStack);
        ApplyCurrent();
        return true;
    }

    public bool NavigateBack()
    {
        // First, check if current page handles keyboard back navigation internally
        if (_stack.Current?.Page is ISupportsKeyboardBackNavigation backHandler && backHandler.HandleBackNavigation())
        {
            return true;
        }

        var prev = _stack.NavigateBack();
        if (prev != null)
        {
            ApplyCurrent();
            return true;
        }

        return false;
    }

    public bool NavigateForward()
    {
        var next = _stack.NavigateForward();
        if (next != null)
        {
            ApplyCurrent();
            return true;
        }

        return false;
    }

    private void ApplyCurrent()
    {
        var entry = _stack.Current;
        if (entry != null && _shellHost != null)
        {
            _shellHost.SetContent(entry.Page);
            _shellHost.UpdateBreadcrumb(entry.Breadcrumb);

            if (entry.Page is ITitleBarContextSource titleBarSource)
            {
                _shellHost.SetTitleBarContext(titleBarSource.TitleBarTitle, titleBarSource.TitleBarSubtitle, titleBarSource.TitleBarStatusDotColor);
            }
            else
            {
                _shellHost.SetTitleBarContext(null, null, null);
            }
        }

        NavigationChanged?.Invoke();
    }
}
