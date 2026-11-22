using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using PocketMC.Core.Services;

namespace PocketMC.Platform.Metrics;

public sealed class NativeMetricsProvider : INativeMetricsProvider
{
    private readonly ConcurrentDictionary<int, (DateTime Timestamp, TimeSpan TotalProcessorTime)> _cpuSamples = new();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    public (double CpuPercent, long MemoryBytes) GetProcessMetrics(int pid)
    {
        if (pid <= 0) return (0.0, 0);

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var linuxMetrics = ReadLinuxProcMetrics(pid);
                if (linuxMetrics.HasValue) return linuxMetrics.Value;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var macMetrics = ReadMacPsMetrics(pid);
                if (macMetrics.HasValue) return macMetrics.Value;
            }

            return ReadStandardProcessMetrics(pid);
        }
        catch
        {
            return (0.0, 0);
        }
    }

    public (long TotalBytes, long AvailableBytes) GetSystemMemoryMetrics()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                {
                    return ((long)memStatus.ullTotalPhys, (long)memStatus.ullAvailPhys);
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var linuxMem = ReadLinuxMeminfo();
                if (linuxMem.HasValue) return linuxMem.Value;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var macMem = ReadMacSystemMemory();
                if (macMem.HasValue) return macMem.Value;
            }

            var gcInfo = GC.GetGCMemoryInfo();
            long total = gcInfo.TotalAvailableMemoryBytes;
            long used = gcInfo.MemoryLoadBytes;
            return (Math.Max(1024 * 1024 * 1024L, total), Math.Max(512 * 1024 * 1024L, total - used));
        }
        catch
        {
            long fallbackTotal = 8L * 1024 * 1024 * 1024;
            return (fallbackTotal, fallbackTotal / 2);
        }
    }

    private (double CpuPercent, long MemoryBytes) ReadStandardProcessMetrics(int pid)
    {
        using var process = Process.GetProcessById(pid);
        long mem = process.WorkingSet64;
        DateTime now = DateTime.UtcNow;
        TimeSpan currentProcessorTime = process.TotalProcessorTime;

        if (_cpuSamples.TryGetValue(pid, out var prevSample))
        {
            double elapsedMs = (now - prevSample.Timestamp).TotalMilliseconds;
            double cpuMs = (currentProcessorTime - prevSample.TotalProcessorTime).TotalMilliseconds;
            _cpuSamples[pid] = (now, currentProcessorTime);

            if (elapsedMs > 0)
            {
                double cpu = (cpuMs / (elapsedMs * Environment.ProcessorCount)) * 100.0;
                return (Math.Clamp(cpu, 0.0, 100.0), mem);
            }
        }
        else
        {
            _cpuSamples[pid] = (now, currentProcessorTime);
        }

        return (0.0, mem);
    }

    private (double CpuPercent, long MemoryBytes)? ReadLinuxProcMetrics(int pid)
    {
        try
        {
            string statusPath = $"/proc/{pid}/status";
            if (!File.Exists(statusPath)) return null;

            long memoryBytes = 0;
            foreach (var line in File.ReadLines(statusPath))
            {
                if (line.StartsWith("VmRSS:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && long.TryParse(parts[1], out long kb))
                    {
                        memoryBytes = kb * 1024;
                        break;
                    }
                }
            }

            var std = ReadStandardProcessMetrics(pid);
            return (std.CpuPercent, memoryBytes > 0 ? memoryBytes : std.MemoryBytes);
        }
        catch
        {
            return null;
        }
    }

    private (double CpuPercent, long MemoryBytes)? ReadMacPsMetrics(int pid)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ps",
                Arguments = $"-o %cpu,rss -p {pid}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return null;

            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(1000);

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length >= 2)
            {
                var parts = lines[1].Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 &&
                    double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double cpu) &&
                    long.TryParse(parts[1], out long rssKb))
                {
                    return (cpu, rssKb * 1024);
                }
            }
        }
        catch
        {
            // Fallback
        }
        return null;
    }

    private static (long TotalBytes, long AvailableBytes)? ReadLinuxMeminfo()
    {
        try
        {
            if (!File.Exists("/proc/meminfo")) return null;

            long totalKb = 0;
            long availableKb = 0;

            foreach (var line in File.ReadLines("/proc/meminfo"))
            {
                if (line.StartsWith("MemTotal:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && long.TryParse(parts[1], out long kb)) totalKb = kb;
                }
                else if (line.StartsWith("MemAvailable:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && long.TryParse(parts[1], out long kb)) availableKb = kb;
                }

                if (totalKb > 0 && availableKb > 0) break;
            }

            if (totalKb > 0)
            {
                return (totalKb * 1024, (availableKb > 0 ? availableKb : totalKb / 2) * 1024);
            }
        }
        catch
        {
            // Fallback
        }
        return null;
    }

    private static (long TotalBytes, long AvailableBytes)? ReadMacSystemMemory()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sysctl",
                Arguments = "-n hw.memsize",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return null;

            string outStr = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(1000);

            if (long.TryParse(outStr, out long totalBytes) && totalBytes > 0)
            {
                return (totalBytes, totalBytes / 2); // conservative safe available estimate
            }
        }
        catch
        {
            // Fallback
        }
        return null;
    }
}
