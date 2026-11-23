using System;
using System.Collections.Generic;
using PocketMC.App.Navigation;
using PocketMC.App.ViewModels;
using PocketMC.Infrastructure.Configuration;
using Xunit;

namespace PocketMC.Tests
{
    public class NavigationTests
    {
        private class TestShellHost : IShellHost
        {
            public object? Content { get; private set; }
            public string? Breadcrumb { get; private set; }
            public string? Title { get; private set; }
            public string? Subtitle { get; private set; }
            public string? StatusDotColor { get; private set; }

            public void SetContent(object page) => Content = page;
            public void UpdateBreadcrumb(string breadcrumb) => Breadcrumb = breadcrumb;
            public void SetTitleBarContext(string? title, string? subtitle, string? statusDotColor)
            {
                Title = title;
                Subtitle = subtitle;
                StatusDotColor = statusDotColor;
            }
        }

        private class SimpleServiceProvider : IServiceProvider
        {
            private readonly Dictionary<Type, object> _services = new();
            public void Register(Type type, object instance) => _services[type] = instance;
            public object? GetService(Type serviceType) => _services.TryGetValue(serviceType, out var s) ? s : null;
        }

        [Fact]
        public void AppNavigationService_PushAndBack_UpdatesStackAndBreadcrumbs()
        {
            var host = new TestShellHost();
            var nav = new AppNavigationService();
            nav.Initialize(host);

            Assert.False(nav.CanNavigateBack);
            Assert.False(nav.CanNavigateForward);

            // Navigate to Dashboard
            nav.NavigateToDashboard();
            Assert.Equal("Dashboard", nav.CurrentBreadcrumb);
            Assert.Equal(DetailRouteKind.Dashboard, nav.CurrentRouteKind);
            Assert.False(nav.CanNavigateBack);

            // Navigate to Detail page
            var dummyPage = new object();
            nav.NavigateToDetailPage(dummyPage, "Server 1 Settings", DetailRouteKind.ServerSettings, DetailBackNavigation.Dashboard);

            Assert.True(nav.CanNavigateBack);
            Assert.Equal("Server 1 Settings", nav.CurrentBreadcrumb);
            Assert.Equal(DetailRouteKind.ServerSettings, nav.CurrentRouteKind);
            Assert.Same(dummyPage, host.Content);

            // Navigate Back
            bool navigatedBack = nav.NavigateBack();
            Assert.True(navigatedBack);
            Assert.False(nav.CanNavigateBack);
            Assert.True(nav.CanNavigateForward);
            Assert.Equal("Dashboard", nav.CurrentBreadcrumb);
        }

        [Fact]
        public void AppNavigationService_WithServiceProvider_PushesResolvedViewModel()
        {
            var sp = new SimpleServiceProvider();
            var host = new TestShellHost();
            var nav = new AppNavigationService(sp);
            nav.Initialize(host);

            // Register dummy objects
            var dummyDashboard = new object();
            var dummyTunnel = new object();
            var dummyNews = new object();

            sp.Register(typeof(DashboardViewModel), dummyDashboard);
            sp.Register(typeof(TunnelViewModel), dummyTunnel);
            sp.Register(typeof(NewsViewModel), dummyNews);

            nav.NavigateToDashboard();
            Assert.Same(dummyDashboard, host.Content);
            Assert.Equal("Dashboard", nav.CurrentBreadcrumb);

            nav.NavigateToTunnel();
            Assert.Same(dummyTunnel, host.Content);
            Assert.Equal("Tunnel", nav.CurrentBreadcrumb);

            nav.NavigateToNews();
            Assert.Same(dummyNews, host.Content);
            Assert.Equal("Community News", nav.CurrentBreadcrumb);
        }

        [Fact]
        public void DashboardActions_SetCallbacks_InvokesConfiguredCallbacks()
        {
            bool settingsOpened = false;
            bool consoleOpened = false;
            bool playersOpened = false;
            bool exportOpened = false;

            var dummyRunner = new FakeProcessRunner();
            var dummyService = new FakeInstanceService();
            var dummyInstance = new PocketMC.Core.Models.ServerInstance { Name = "Test", Slug = "test", Path = "/tmp/test" };
            var card = new InstanceCardViewModel(dummyInstance);

            var actions = new DashboardActionsViewModel(
                dummyRunner,
                dummyService);

            actions.SetCallbacks(
                onOpenSettings: c => settingsOpened = true,
                onOpenConsole: c => consoleOpened = true,
                onOpenPlayers: c => playersOpened = true,
                onOpenExport: c => exportOpened = true);

            actions.OpenSettings(card);
            Assert.True(settingsOpened);

            actions.OpenConsole(card);
            Assert.True(consoleOpened);

            actions.OpenPlayers(card);
            Assert.True(playersOpened);

            actions.OpenExport(card);
            Assert.True(exportOpened);
        }

        [Fact]
        public void AppUrls_HasAuthoritativeDiscordAndProjectLinks()
        {
            Assert.Equal("https://discord.gg/mWdMr8Mc2m", AppUrls.DiscordInvite);
            Assert.Equal("https://pocketmc.github.io/pocket-mc-website/", AppUrls.Website);
            Assert.Equal("https://github.com/PocketMC/pocket-mc-linux-mac", AppUrls.GitHub);
            Assert.Equal("https://buymeacoffee.com/sahaj33", AppUrls.Donation);
            Assert.Equal("https://discord.com/users/524387761541742592", AppUrls.NexViceDiscord);
            Assert.Equal("https://www.youtube.com/@OfficialPocketMC", AppUrls.YouTube);
            Assert.Equal("https://www.reddit.com/r/PocketMC/", AppUrls.Reddit);
            Assert.Equal("https://www.instagram.com/thepocketmc", AppUrls.Instagram);
        }

        [Fact]
        public void DetailRouteKind_ContainsRemoteControlAndRuntimes()
        {
            var host = new TestShellHost();
            var nav = new AppNavigationService();
            nav.Initialize(host);

            var rcPage = new object();
            nav.NavigateToDetailPage(rcPage, "Remote Control", DetailRouteKind.RemoteControl, DetailBackNavigation.Dashboard);
            Assert.Equal(DetailRouteKind.RemoteControl, nav.CurrentRouteKind);
            Assert.Equal("Remote Control", nav.CurrentBreadcrumb);

            var runtimesPage = new object();
            nav.NavigateToDetailPage(runtimesPage, "Runtimes", DetailRouteKind.Runtimes, DetailBackNavigation.Dashboard);
            Assert.Equal(DetailRouteKind.Runtimes, nav.CurrentRouteKind);
            Assert.Equal("Runtimes", nav.CurrentBreadcrumb);
        }
    }
}
