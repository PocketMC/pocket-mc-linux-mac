using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests;

public class PlayerParserTests
{
    [Fact]
    public void PlayerListParser_ParsesJavaInlineList()
    {
        string line = "[Server thread/INFO]: There are 2 of a max of 20 players online: Steve, Alex";
        var result = PlayerListParser.TryParse(line);

        Assert.NotNull(result);
        Assert.Equal(2, result.OnlinePlayerCount);
        Assert.Equal(20, result.MaxPlayers);
        Assert.Equal(2, result.OnlinePlayerNames.Count);
        Assert.Contains("Steve", result.OnlinePlayerNames);
        Assert.Contains("Alex", result.OnlinePlayerNames);
    }

    [Fact]
    public void PlayerGamemodeLogParser_ParsesGamemodeChange()
    {
        string line = "[Server thread/INFO]: Set Steve's game mode to Creative Mode";
        bool parsed = PlayerGamemodeLogParser.TryParse(line, out var ev);

        Assert.True(parsed);
        Assert.NotNull(ev);
        Assert.Equal("Steve", ev.PlayerName);
        Assert.Equal("creative", ev.Gamemode);
    }
}
