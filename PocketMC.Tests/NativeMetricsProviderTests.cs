using System;
using System.Diagnostics;
using PocketMC.Platform.Metrics;
using Xunit;

namespace PocketMC.Tests;

public class NativeMetricsProviderTests
{
    [Fact]
    public void GetProcessMetrics_ForCurrentProcess_ReturnsNonZeroMemory()
    {
        var provider = new NativeMetricsProvider();
        int currentPid = Environment.ProcessId;

        var metrics = provider.GetProcessMetrics(currentPid);

        Assert.True(metrics.MemoryBytes > 0, "Expected resident memory to be greater than 0");
        Assert.True(metrics.CpuPercent >= 0.0, "Expected non-negative CPU percent");
    }

    [Fact]
    public void GetSystemMemoryMetrics_ReturnsPositiveTotals()
    {
        var provider = new NativeMetricsProvider();
        var (total, available) = provider.GetSystemMemoryMetrics();

        Assert.True(total > 0, "Total system memory must be greater than 0");
        Assert.True(available > 0, "Available system memory must be greater than 0");
        Assert.True(available <= total, "Available memory cannot exceed total memory");
    }
}
