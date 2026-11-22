using System;
using PocketMC.App.Interfaces;

namespace PocketMC.App.Navigation;

public sealed class AppNavigationService : IAppNavigationService
{
    private readonly ControlledNavigationStack _stack = new();
    private IShellHost? _shellHost;

    public event Action? NavigationChanged;

    public bool CanNavigateBack => _stack.CanNavigateBack;
    public bool CanNavigateForward => _stack.CanNavigateForward;
    public string? CurrentBreadcrumb => _stack.Current?.Breadcrumb;
    public DetailRouteKind? CurrentRouteKind => _stack.Current?.RouteKind;

    public void Initialize(IShellHost shellHost)
    {
        _shellHost = shellHost;
    }

    public bool NavigateToDashboard()
    {
        if (_shellHost == null) return false;
        // Dashboard is the primary root: navigate back until root or push dashboard
        _stack.Push("Dashboard", "Dashboard", DetailRouteKind.Dashboard, DetailBackNavigation.Dashboard, clearDetailStack: true);
        ApplyCurrent();
        return true;
    }

    public bool NavigateToTunnel()
    {
        if (_shellHost == null) return false;
        _stack.Push("Tunnel", "Tunnel", DetailRouteKind.Tunnel, DetailBackNavigation.Dashboard, clearDetailStack: true);
        ApplyCurrent();
        return true;
    }

    public bool NavigateToNews()
    {
        if (_shellHost == null) return false;
        _stack.Push("News", "Community News", DetailRouteKind.News, DetailBackNavigation.Dashboard);
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
