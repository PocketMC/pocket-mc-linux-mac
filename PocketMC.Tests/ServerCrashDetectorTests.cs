using System;
using System.IO;
using PocketMC.Core.Models;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests;

public class ServerCrashDetectorTests
{
    [Fact]
    public void ServerCrashDetector_IntentionalStop_ReturnsCleanExit()
    {
        var context = new ServerCrashContext(
            WorkingDirectory: "dummy",
            ServerType: "paper",
            StateBeforeExit: ServerState.Running,
            IntentionalStop: true,
            ExitCode: 0,
            ProcessStartTime: DateTime.UtcNow,
            OutputBufferLines: new[] { "Stopping server", "Saving worlds" });

        var result = ServerCrashDetector.Analyze(context);

        Assert.False(result.IsCrash);
        Assert.Equal(CrashCategory.None, result.Category);
    }

    [Fact]
    public void ServerCrashDetector_DetectsOutOfMemory()
    {
        var context = new ServerCrashContext(
            WorkingDirectory: "dummy",
            ServerType: "fabric",
            StateBeforeExit: ServerState.Running,
            IntentionalStop: false,
            ExitCode: 1,
            ProcessStartTime: DateTime.UtcNow,
            OutputBufferLines: new[] { "java.lang.OutOfMemoryError: Java heap space" });

        var result = ServerCrashDetector.Analyze(context);

        Assert.True(result.IsCrash);
        Assert.Equal(CrashCategory.OutOfMemory, result.Category);
    }
}
