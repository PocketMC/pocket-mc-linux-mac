using PocketMC.App.Services.Console;
using Xunit;

namespace PocketMC.Tests;

public class ConsoleLogFilterTests
{
    [Fact]
    public void ConsoleLogFilter_MatchesSearch_SupportsPositiveAndNegativeKeywords()
    {
        string logLine = "[Server thread/INFO]: Player Steve joined the game";

        Assert.True(ConsoleLogFilter.MatchesSearch(logLine, "Player Steve", false));
        Assert.False(ConsoleLogFilter.MatchesSearch(logLine, "Player Alex", false));
        Assert.True(ConsoleLogFilter.MatchesSearch(logLine, "Player -Alex", false));
        Assert.False(ConsoleLogFilter.MatchesSearch(logLine, "Player -Steve", false));
    }

    [Fact]
    public void LogLineClassifier_ClassifiesChatAndErrors()
    {
        string chat = "[12:00:00 INFO]: <Steve> Hello World";
        Assert.Equal(ConsoleLogLevel.Chat, LogLineClassifier.Classify(chat, ConsoleLogLevel.Info));

        string error = "[12:00:00 ERROR]: Encountered an unexpected exception";
        Assert.Equal(ConsoleLogLevel.Error, LogLineClassifier.Classify(error, ConsoleLogLevel.Info));
    }
}
