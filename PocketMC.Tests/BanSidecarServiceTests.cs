using System.IO;
using System.Threading.Tasks;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests;

public class BanSidecarServiceTests
{
    [Fact]
    public async Task AddBanAsync_WhenCalled_PersistsToPocketmcBansJsonAndCanBeRetrieved()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "pocketmc_test_bansidecar_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            var service = new BanSidecarService();
            await service.AddBanAsync(tempDir, "Gamer123", "Griefing");

            var banned = await service.GetBannedPlayersAsync(tempDir);

            Assert.Single(banned);
            Assert.Equal("Gamer123", banned[0].Name);
            Assert.Equal("Griefing", banned[0].Reason);
            Assert.True(banned[0].IsSidecar);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
