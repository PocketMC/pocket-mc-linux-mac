using PocketMC.Infrastructure.Configuration;

namespace PocketMC.Tests;

public class AppConfigTests
{
    [Fact]
    public void EmbeddedConfig_LoadsVersionChannelProxiesAndSocialLinks()
    {
        AppConfig.LoadEmbeddedConfig();

        Assert.Equal("1.0.4.0", AppConfig.AppVersion);
        Assert.Equal("release", AppConfig.Channel);
        Assert.Equal(2, AppConfig.AuthProxies.Count);
        Assert.Contains("https://pocket-mc-proxy-20d5.onrender.com", AppConfig.AuthProxies);
        Assert.Equal(2, AppConfig.TelemetryProxies.Count);
        Assert.Contains("https://pocket-mc-proxy-3fqm.onrender.com/", AppConfig.TelemetryProxies);
        Assert.Equal("https://discord.gg/mWdMr8Mc2m", AppConfig.LinkDiscord);
        Assert.Equal("https://github.com/PocketMC/pocket-mc-linux-mac", AppConfig.LinkGitHub);
    }
}
