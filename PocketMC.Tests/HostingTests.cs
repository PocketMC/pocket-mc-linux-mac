using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PocketMC.Core.Services;
using PocketMC.App;
using Xunit;

namespace PocketMC.Tests
{
    public class HostingTests
    {
        [Fact]
        public void TestDiContainerSetup()
        {
            using (var host = Program.CreateHostBuilder(new string[0]).Build())
            {
                var settingsService = host.Services.GetService<ISettingsService>();
                var secretStore = host.Services.GetService<ISecretStore>();

                Assert.NotNull(settingsService);
                Assert.NotNull(secretStore);
            }
        }

        [Fact]
        public void Host_CanResolveAllRegisteredViewModelsWithoutAmbiguity()
        {
            using var host = Program.CreateHostBuilder(System.Array.Empty<string>()).Build();
            var sp = host.Services;

            // ViewModels
            Assert.NotNull(sp.GetRequiredService<PocketMC.App.ViewModels.MainWindowViewModel>());
            Assert.NotNull(sp.GetRequiredService<PocketMC.App.ViewModels.DashboardViewModel>());
            Assert.NotNull(sp.GetRequiredService<PocketMC.App.ViewModels.NewInstanceViewModel>());
            Assert.NotNull(sp.GetRequiredService<PocketMC.App.ViewModels.ServerConsoleViewModel>());
            Assert.NotNull(sp.GetRequiredService<PocketMC.App.ViewModels.ServerSettingsViewModel>());
            Assert.NotNull(sp.GetRequiredService<PocketMC.App.ViewModels.PlayerManagementViewModel>());
            Assert.NotNull(sp.GetRequiredService<PocketMC.App.ViewModels.ServerBackupsViewModel>());
            Assert.NotNull(sp.GetRequiredService<PocketMC.App.ViewModels.MarketplaceViewModel>());
            Assert.NotNull(sp.GetRequiredService<PocketMC.App.ViewModels.AiDiagnosticViewModel>());
            Assert.NotNull(sp.GetRequiredService<PocketMC.App.ViewModels.RemoteControlViewModel>());
            Assert.NotNull(sp.GetRequiredService<PocketMC.App.ViewModels.TunnelViewModel>());
            Assert.NotNull(sp.GetRequiredService<PocketMC.App.ViewModels.PortsMapViewModel>());
            Assert.NotNull(sp.GetRequiredService<PocketMC.App.ViewModels.JavaManagementViewModel>());
            Assert.NotNull(sp.GetRequiredService<PocketMC.App.ViewModels.AboutViewModel>());
            Assert.NotNull(sp.GetRequiredService<PocketMC.App.ViewModels.NewsViewModel>());
            Assert.NotNull(sp.GetRequiredService<PocketMC.App.ViewModels.AppSettingsViewModel>());
            Assert.NotNull(sp.GetRequiredService<PocketMC.App.ViewModels.RootDirectorySetupViewModel>());
            Assert.NotNull(sp.GetRequiredService<PocketMC.App.ViewModels.Instances.InstanceImportViewModel>());
        }
    }
}
