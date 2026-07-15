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
    }
}
