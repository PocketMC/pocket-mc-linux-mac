using System.IO;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests;

public class GeyserDetectorTests
{
    [Fact]
    public void IsGeyserInstalled_WhenGeyserJarExistsInPlugins_ReturnsTrue()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "pocketmc_test_geyser_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(tempDir, "plugins"));

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "plugins", "Geyser-Spigot.jar"), "dummy jar");

            var manifestService = new AddonManifestService();
            IGeyserDetector detector = new GeyserDetector(manifestService);

            bool isInstalled = detector.IsGeyserInstalled(tempDir);

            Assert.True(isInstalled);
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
