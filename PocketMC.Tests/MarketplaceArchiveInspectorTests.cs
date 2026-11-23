using System.IO;
using System.IO.Compression;
using System.Text;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests;

public class MarketplaceArchiveInspectorTests
{
    [Fact]
    public void IsClientOnlyAddon_WhenFabricModHasClientEnvironment_ReturnsTrue()
    {
        string tempJar = Path.Combine(Path.GetTempPath(), "pocketmc_test_mod_" + Path.GetRandomFileName() + ".jar");

        try
        {
            using (var zip = ZipFile.Open(tempJar, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("fabric.mod.json");
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write("{\"schemaVersion\": 1, \"id\": \"testclientmod\", \"environment\": \"client\"}");
            }

            bool isClientOnly = MarketplaceArchiveInspector.IsClientOnlyAddon(tempJar);

            Assert.True(isClientOnly);
        }
        finally
        {
            if (File.Exists(tempJar))
            {
                File.Delete(tempJar);
            }
        }
    }
}
