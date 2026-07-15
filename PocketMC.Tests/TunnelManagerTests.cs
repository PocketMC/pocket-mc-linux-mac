using System.Diagnostics;
using PocketMC.RemoteControl.Tunnels;
using Xunit;

namespace PocketMC.Tests;

public class TunnelManagerTests
{
    [Fact]
    public void TunnelProcessGroupManager_ShouldSetPgidAndStopGroup()
    {
        // This is a minimal test demonstrating the PGID API invocation isn't throwing exceptions.
        // True isolation testing of POSIX signals requires native shell mocks, 
        // but we verify the manager's state machine works.
        
        var manager = new TunnelProcessGroupManager();
        Assert.False(manager.IsRunning);
        
        // Use a benign command for the test
        var startInfo = new ProcessStartInfo
        {
            FileName = "sleep",
            Arguments = "10",
            CreateNoWindow = true,
            UseShellExecute = false
        };
        
        manager.StartProcess(startInfo);
        
        Assert.True(manager.IsRunning);
        Assert.True(manager.Pgid > 0);
        
        manager.StopProcessGroup();
        
        // Give it a moment to die
        System.Threading.Thread.Sleep(500);
        
        Assert.False(manager.IsRunning);
    }
}
