using System.Diagnostics;
using System.Runtime.InteropServices;
using PocketMC.RemoteControl.Tunnels;
using Xunit;

namespace PocketMC.Tests;

public class TunnelManagerTests
{
    [Fact]
    public void TunnelProcessGroupManager_ShouldSetPgidAndStopGroup()
    {
        // This test demonstrates the PGID and process group manager state machine works.
        var manager = new TunnelProcessGroupManager();
        Assert.False(manager.IsRunning);

        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var startInfo = new ProcessStartInfo
        {
            FileName = isWindows ? "cmd.exe" : "sleep",
            Arguments = isWindows ? "/c ping 127.0.0.1 -n 10" : "10",
            CreateNoWindow = true,
            UseShellExecute = false
        };

        manager.StartProcess(startInfo);

        Assert.True(manager.IsRunning);
        Assert.True(manager.Pgid > 0);

        manager.StopProcessGroup();

        // Give it a moment to exit
        System.Threading.Thread.Sleep(500);

        Assert.False(manager.IsRunning);
    }

    [Fact]
    public void TunnelProcessGroupManager_CapturesOutputLinesAndFiresEvent()
    {
        var manager = new TunnelProcessGroupManager();
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var startInfo = new ProcessStartInfo
        {
            FileName = isWindows ? "cmd.exe" : "echo",
            Arguments = isWindows ? "/c echo Hello Playit Agent" : "Hello Playit Agent",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        bool eventFired = false;
        string? capturedLine = null;
        manager.OutputLineReceived += line =>
        {
            if (line.Contains("Hello Playit Agent"))
            {
                eventFired = true;
                capturedLine = line;
            }
        };

        manager.StartProcess(startInfo);
        System.Threading.Thread.Sleep(500);

        Assert.True(eventFired);
        Assert.NotNull(capturedLine);
        Assert.Contains(capturedLine, manager.GetRecentLogs());
    }
}
