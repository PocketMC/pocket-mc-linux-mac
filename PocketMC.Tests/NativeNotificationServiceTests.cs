using System;
using PocketMC.Core.Services;
using PocketMC.Platform.Notifications;
using Xunit;

namespace PocketMC.Tests;

public class NativeNotificationServiceTests
{
    [Fact]
    public void BuildLinuxNotificationCommand_EscapesDoubleQuotesAndBuildsArgs()
    {
        string title = "Server Online: \"My Server\"";
        string message = "Running on port 25565";

        var (fileName, args) = NativeNotificationService.BuildLinuxCommand(title, message, "/path/to/icon.png");

        Assert.Equal("notify-send", fileName);
        Assert.Contains("-a \"PocketMC\"", args);
        Assert.Contains("-i \"/path/to/icon.png\"", args);
        Assert.Contains("\\\"My Server\\\"", args);
    }

    [Fact]
    public void BuildMacScript_FormatsOsascriptDisplayNotification()
    {
        string title = "Server Online";
        string message = "Running smoothly";

        string script = NativeNotificationService.BuildMacScript(title, message);

        Assert.Contains("display notification", script);
        Assert.Contains("with title", script);
        Assert.Contains("subtitle \\\"PocketMC\\\"", script);
    }
}
