using System;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Platform.Integrations;
using Xunit;

namespace PocketMC.Tests;

public class DiscordRpcServiceTests
{
    [Fact]
    public void BuildIdlePresence_WhenShowDownloadButtonEnabled_ContainsDownloadButton()
    {
        var settings = new DiscordRpcSettings
        {
            Enabled = true,
            ShowIdle = true,
            ShowDownloadButton = true
        };

        var presence = DiscordRpcService.BuildIdlePresence(settings);

        Assert.NotNull(presence);
        Assert.Equal("Managing Servers", presence.Details);
        Assert.Equal("Idle", presence.State);
        Assert.NotNull(presence.Buttons);
        Assert.Single(presence.Buttons);
        Assert.Contains("Download PocketMC", presence.Buttons[0].Label);
    }

    [Fact]
    public void BuildServerPresence_WithServerNameAndVersion_FormatsPresenceDetails()
    {
        var settings = new DiscordRpcSettings
        {
            Enabled = true,
            ShowServerName = true,
            ShowVersionAndEngine = true,
            ShowPlayerCount = true,
            ShowDownloadButton = true
        };

        var presence = DiscordRpcService.BuildServerPresence(
            "Survival SMP",
            "1.20.4",
            "Paper",
            5,
            isOnline: true,
            settings);

        Assert.NotNull(presence);
        Assert.Equal("Survival SMP", presence.Details);
        Assert.Contains("Paper 1.20.4", presence.State);
        Assert.Contains("5 online", presence.State);
    }

    [Fact]
    public void BuildServerPresence_WhenShowServerNameFalse_UsesGenericDetails()
    {
        var settings = new DiscordRpcSettings
        {
            Enabled = true,
            ShowServerName = false,
            ShowVersionAndEngine = false,
            ShowPlayerCount = false
        };

        var presence = DiscordRpcService.BuildServerPresence(
            "Secret Server",
            "1.20.4",
            "Paper",
            0,
            isOnline: true,
            settings);

        Assert.Equal("Minecraft Server", presence.Details);
        Assert.Equal("Online", presence.State);
    }
}
