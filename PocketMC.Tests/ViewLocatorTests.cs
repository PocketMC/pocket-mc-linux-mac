using System;
using PocketMC.App;
using PocketMC.App.ViewModels;
using PocketMC.App.ViewModels.Instances;
using PocketMC.App.Views;
using PocketMC.App.Views.Instances;
using Xunit;

namespace PocketMC.Tests
{
    public class ViewLocatorTests
    {
        [Theory]
        [InlineData(typeof(NewInstanceViewModel), typeof(NewInstancePage))]
        [InlineData(typeof(ServerSettingsViewModel), typeof(ServerSettingsPage))]
        [InlineData(typeof(ServerConsoleViewModel), typeof(ServerConsolePage))]
        [InlineData(typeof(PlayerManagementViewModel), typeof(PlayerManagementPage))]
        [InlineData(typeof(ServerBackupsViewModel), typeof(ServerBackupsPage))]
        [InlineData(typeof(MarketplaceViewModel), typeof(MarketplacePage))]
        [InlineData(typeof(AiDiagnosticViewModel), typeof(AiDiagnosticPage))]
        [InlineData(typeof(RemoteControlViewModel), typeof(RemoteControlPage))]
        [InlineData(typeof(TunnelViewModel), typeof(TunnelPage))]
        [InlineData(typeof(PortsMapViewModel), typeof(PortsMapPage))]
        [InlineData(typeof(JavaManagementViewModel), typeof(JavaManagementPage))]
        [InlineData(typeof(AboutViewModel), typeof(AboutPage))]
        [InlineData(typeof(InstanceExportViewModel), typeof(InstanceExportPage))]
        [InlineData(typeof(InstanceImportViewModel), typeof(InstanceImportPage))]
        [InlineData(typeof(DashboardViewModel), typeof(DashboardView))]
        public void ViewLocator_ResolvesExpectedViewType_ForViewModel(Type viewModelType, Type expectedViewType)
        {
            var resolvedType = ViewLocator.ResolveViewType(viewModelType);
            Assert.Equal(expectedViewType, resolvedType);
        }

        [Fact]
        public void ViewLocator_MatchesObservableObjects()
        {
            var locator = new ViewLocator();
            Assert.True(locator.Match(new FakeObservableObject()));
            Assert.False(locator.Match(new object()));
            Assert.False(locator.Match(null));
        }

        private class FakeObservableObject : CommunityToolkit.Mvvm.ComponentModel.ObservableObject { }
    }
}

