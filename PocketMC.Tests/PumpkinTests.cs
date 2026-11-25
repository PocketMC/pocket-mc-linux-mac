using System;
using System.IO;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests;

public sealed class PumpkinTests
{
    [Fact]
    public void PumpkinConfiguration_LoadAndSave_PreservesUnknownTomlAndUpdatesPorts()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "pmc-pumpkin-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string initialToml = """
                default_gamemode = "survival"
                default_difficulty = "normal"

                [networking.java]
                enabled = true
                address = "0.0.0.0:25565"
                motd = "Custom Pumpkin"

                [networking.bedrock.nethernet]
                enabled = true
                address = "0.0.0.0:19132"

                [custom_plugin]
                keep_this = "untouched"
                """;
            File.WriteAllText(Path.Combine(tempDir, PumpkinConfiguration.FileName), initialToml);

            var props = PumpkinConfiguration.Load(tempDir);
            Assert.Equal("Custom Pumpkin", props["motd"]);
            Assert.Equal("25565", props["server-port"]);
            Assert.Equal("19132", props["pumpkin-bedrock-port"]);

            props["motd"] = "Updated Title";
            props["server-port"] = "25570";
            props["pumpkin-bedrock-port"] = "19140";
            PumpkinConfiguration.Save(tempDir, props);

            var reloaded = PumpkinConfiguration.Load(tempDir);
            Assert.Equal("Updated Title", reloaded["motd"]);
            Assert.Equal("25570", reloaded["server-port"]);
            Assert.Equal("19140", reloaded["pumpkin-bedrock-port"]);

            string raw = File.ReadAllText(Path.Combine(tempDir, PumpkinConfiguration.FileName));
            Assert.Contains("keep_this = \"untouched\"", raw);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}
