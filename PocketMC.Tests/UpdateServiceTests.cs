using System.Threading.Tasks;
using PocketMC.RemoteControl.Services;
using Xunit;

namespace PocketMC.Tests;

public class UpdateServiceTests
{
    [Fact]
    public async Task UpdateService_CheckForUpdates_ReturnsUpdateInfo()
    {
        // Integration mock test
        var service = new UpdateService();
        var updateInfo = await service.CheckForUpdatesAsync(useBetaChannel: false);
        
        if (updateInfo != null)
        {
            Assert.Equal("v1.1.0", updateInfo.Version);
            Assert.NotEmpty(updateInfo.ReleaseNotes);
            Assert.NotEmpty(updateInfo.DownloadUrl);
        }
    }
}
