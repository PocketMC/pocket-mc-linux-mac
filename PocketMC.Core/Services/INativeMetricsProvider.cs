using System;

namespace PocketMC.Core.Services
{
    public interface INativeMetricsProvider
    {
        (double CpuPercent, long MemoryBytes) GetProcessMetrics(int pid);
        (long TotalBytes, long AvailableBytes) GetSystemMemoryMetrics();
    }
}
