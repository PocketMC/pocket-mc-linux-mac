using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace PocketMC.Infrastructure.Services
{
    public class ProcessMetricsTracker
    {
        private readonly Dictionary<int, TimeSpan> _lastCpuTimes = new();
        private DateTime _lastSnapshotTime = DateTime.MinValue;
        private readonly object _lock = new();

        public record ProcessMetrics(double CpuPercentage, long MemoryBytes);

        public ProcessMetrics GetGroupMetrics(int parentPid)
        {
            lock (_lock)
            {
                var pids = GetPidsInGroup(parentPid);
                
                long totalRss = 0;
                TimeSpan totalCpuTime = TimeSpan.Zero;
                var activePids = new List<int>();

                foreach (var pid in pids)
                {
                    try
                    {
                        using var proc = Process.GetProcessById(pid);
                        if (proc.HasExited) continue;

                        // RAM (Working Set / RSS)
                        totalRss += proc.WorkingSet64;

                        // CPU
                        totalCpuTime += proc.TotalProcessorTime;
                        activePids.Add(pid);
                    }
                    catch
                    {
                        // Process terminated during read
                    }
                }

                // CPU percentage calculation
                var now = DateTime.UtcNow;
                double cpuPercent = 0.0;

                if (_lastSnapshotTime != DateTime.MinValue && activePids.Count > 0)
                {
                    double elapsedMs = (now - _lastSnapshotTime).TotalMilliseconds;
                    if (elapsedMs > 0)
                    {
                        double totalCpuMsUsed = 0.0;
                        foreach (var pid in activePids)
                        {
                            // Match with previous tick
                            if (_lastCpuTimes.TryGetValue(pid, out var lastTime))
                            {
                                try
                                {
                                    using var proc = Process.GetProcessById(pid);
                                    var currentCpuTime = proc.TotalProcessorTime;
                                    totalCpuMsUsed += (currentCpuTime - lastTime).TotalMilliseconds;
                                    _lastCpuTimes[pid] = currentCpuTime;
                                }
                                catch
                                {
                                    _lastCpuTimes.Remove(pid);
                                }
                            }
                            else
                            {
                                try
                                {
                                    using var proc = Process.GetProcessById(pid);
                                    _lastCpuTimes[pid] = proc.TotalProcessorTime;
                                }
                                catch { }
                            }
                        }

                        // Total CPU utilization across all cores
                        cpuPercent = (totalCpuMsUsed / elapsedMs) * 100.0;
                        // Cap at total logical core percentage
                        cpuPercent = Math.Clamp(cpuPercent, 0, Environment.ProcessorCount * 100.0);
                    }
                }
                else
                {
                    // Bootstrap first measurements
                    foreach (var pid in activePids)
                    {
                        try
                        {
                            using var proc = Process.GetProcessById(pid);
                            _lastCpuTimes[pid] = proc.TotalProcessorTime;
                        }
                        catch { }
                    }
                }

                // Cleanup stale PIDs from tracker cache
                var currentPidsSet = new HashSet<int>(activePids);
                var deadPids = _lastCpuTimes.Keys.Where(p => !currentPidsSet.Contains(p)).ToList();
                foreach (var dead in deadPids) _lastCpuTimes.Remove(dead);

                _lastSnapshotTime = now;

                return new ProcessMetrics(cpuPercent, totalRss);
            }
        }

        private List<int> GetPidsInGroup(int pgid)
        {
            var pids = new List<int>();

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Fallback to single process on Windows
                pids.Add(pgid);
                return pids;
            }

            try
            {
                // Execute POSIX standard ps command to fetch PIDs and PGIDs
                var startInfo = new ProcessStartInfo
                {
                    FileName = "ps",
                    Arguments = "-A -o pid,pgid",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(startInfo);
                if (proc != null)
                {
                    using var reader = proc.StandardOutput;
                    reader.ReadLine(); // Skip header
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var parts = line.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2 &&
                            int.TryParse(parts[0], out int pid) &&
                            int.TryParse(parts[1], out int pGroupId) &&
                            pGroupId == pgid)
                        {
                            pids.Add(pid);
                        }
                    }
                    proc.WaitForExit();
                }
            }
            catch
            {
                // Safe fallback to root PID
                pids.Clear();
                pids.Add(pgid);
            }

            if (pids.Count == 0)
            {
                pids.Add(pgid);
            }

            return pids;
        }
    }
}
