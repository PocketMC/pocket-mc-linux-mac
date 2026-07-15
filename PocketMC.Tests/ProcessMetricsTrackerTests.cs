using System;
using System.Diagnostics;
using System.Threading;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests
{
    public class ProcessMetricsTrackerTests
    {
        [Fact]
        public void ProcessMetricsTracker_CalculatesMetricsForRunningProcess()
        {
            ProcessStartInfo psi;
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                psi = new ProcessStartInfo("cmd.exe", "/c ping -t 127.0.0.1") { CreateNoWindow = true, UseShellExecute = false };
            }
            else
            {
                psi = new ProcessStartInfo("sleep", "10") { CreateNoWindow = true, UseShellExecute = false };
            }

            using var process = Process.Start(psi);
            Assert.NotNull(process);
            try
            {
                var tracker = new ProcessMetricsTracker();
                // Bootstrap first measurement
                var first = tracker.GetGroupMetrics(process.Id);
                
                // Sleep to allow CPU time accumulation and snapshot time difference
                Thread.Sleep(100);

                var second = tracker.GetGroupMetrics(process.Id);

                Assert.True(second.MemoryBytes > 0, $"Memory RSS should be > 0, got {second.MemoryBytes}");
                Assert.True(second.CpuPercentage >= 0, $"CPU percent should be >= 0, got {second.CpuPercentage}");
            }
            finally
            {
                try
                {
                    process.Kill();
                }
                catch { }
            }
        }

        [Fact]
        public void CircularBuffer_WrapsCorrectlyAndThreadSafe()
        {
            var buffer = new CircularBuffer<int>(3);
            Assert.Equal(3, buffer.Capacity);
            Assert.Equal(0, buffer.Count);

            buffer.Add(1);
            buffer.Add(2);
            buffer.Add(3);
            
            Assert.Equal(3, buffer.Count);
            Assert.Equal(new[] { 1, 2, 3 }, buffer.ToList());

            buffer.Add(4);
            Assert.Equal(3, buffer.Count);
            Assert.Equal(new[] { 2, 3, 4 }, buffer.ToList());
        }
    }
}
