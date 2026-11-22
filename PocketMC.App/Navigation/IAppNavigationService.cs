using System;

namespace PocketMC.App.Navigation;

public enum DetailRouteKind
{
    Dashboard,
    Tunnel,
    News,
    NewInstance,
    ServerSettings,
    PluginBrowser,
    MapBrowser,
    ServerConsole,
    PlayerManagement,
    ImageCrop,
    PortsMap,
    InstanceImport,
    InstanceExport,
    PlayitNetworkStatus,
    AiDiagnostic,
    WhatsNew,
    Settings,
    About
}

public enum DetailBackNavigation
{
    Dashboard,
    Tunnel,
    PreviousDetail
}

public sealed class NavigationEntry
{
    public object Page { get; }
    public string Breadcrumb { get; }
    public DetailRouteKind RouteKind { get; }
    public DetailBackNavigation BackNavigation { get; }

    public NavigationEntry(object page, string breadcrumb, DetailRouteKind routeKind, DetailBackNavigation backNavigation)
    {
        Page = page;
        Breadcrumb = breadcrumb;
        RouteKind = routeKind;
        BackNavigation = backNavigation;
    }
}

public interface IShellHost
{
    void SetContent(object page);
    void UpdateBreadcrumb(string breadcrumb);
    void SetTitleBarContext(string? title, string? subtitle, string? statusDotColor);
}

public interface IAppNavigationService
{
    void Initialize(IShellHost shellHost);
    bool NavigateToDashboard();
    bool NavigateToTunnel();
    bool NavigateToNews();
    bool NavigateToDetailPage(
        object page,
        string breadcrumbLabel,
        DetailRouteKind routeKind,
        DetailBackNavigation backNavigation,
        bool clearDetailStack = false);
    bool NavigateBack();
    bool NavigateForward();
    bool CanNavigateBack { get; }
    bool CanNavigateForward { get; }
    string? CurrentBreadcrumb { get; }
    DetailRouteKind? CurrentRouteKind { get; }
    event Action? NavigationChanged;
}
