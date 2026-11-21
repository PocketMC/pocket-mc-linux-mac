using System.IO;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests;

public class GeyserProvisioningServiceTests
{
    [Fact]
    public void PatchGeyserConfigPort_WithExistingConfig_UpdatesPort()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "pocketmc_test_geyser_prov_" + Path.GetRandomFileName());
        string geyserConfigDir = Path.Combine(tempDir, "plugins", "Geyser-Spigot");
        Directory.CreateDirectory(geyserConfigDir);

        try
        {
            string configPath = Path.Combine(geyserConfigDir, "config.yml");
            string initialContent = "bedrock:\r\n  address: 0.0.0.0\r\n  port: 19132\r\n";
            File.WriteAllText(configPath, initialContent);

            var service = new GeyserProvisioningService(null!, null!, null!);
            service.PatchGeyserConfigPort(tempDir, 19135);

            string updatedContent = File.ReadAllText(configPath);
            Assert.Contains("port: 19135", updatedContent);
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
