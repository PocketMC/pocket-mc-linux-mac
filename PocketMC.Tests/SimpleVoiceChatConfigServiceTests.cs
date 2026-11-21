using System.IO;
using PocketMC.Core.Models;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests;

public class SimpleVoiceChatConfigServiceTests
{
    [Fact]
    public void TryPatchVoiceHost_WithExistingVoicechatProperties_UpdatesHostPreservingComments()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "pocketmc_test_voicechat_" + Path.GetRandomFileName());
        string configDir = Path.Combine(tempDir, "config", "voicechat");
        Directory.CreateDirectory(configDir);

        try
        {
            string configPath = Path.Combine(configDir, "voicechat-server.properties");
            string initialContent = "# Simple Voice Chat Server Properties\r\nport=24454\r\nbind_address=*\r\nvoice_host=old.example.com\r\n";
            File.WriteAllText(configPath, initialContent);

            bool patched = SimpleVoiceChatConfigService.TryPatchVoiceHost(tempDir, "voice.example.com");

            Assert.True(patched);
            string updated = File.ReadAllText(configPath);
            Assert.Contains("voice_host=voice.example.com", updated);
            Assert.Contains("port=24454", updated);
            Assert.Contains("# Simple Voice Chat Server Properties", updated);
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
