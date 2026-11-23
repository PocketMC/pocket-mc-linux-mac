using System.Collections.Generic;
using PocketMC.App.Navigation;
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
    }
}
