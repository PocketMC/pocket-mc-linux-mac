using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace PocketMC.RemoteControl.Tunnels;

public static class UnixNative
{
    [DllImport("libc", SetLastError = true)]
    public static extern int setpgid(int pid, int pgid);

    [DllImport("libc", SetLastError = true)]
    public static extern int kill(int pid, int sig);

    public const int SIGKILL = 9;
    public const int SIGTERM = 15;
}

public class TunnelProcessGroupManager
{
    private const int MaxLogBufferLines = 1000;
    private readonly ConcurrentQueue<string> _logBuffer = new();
    private Process? _process;
    private int _pgid;

    public event Action<string>? OutputLineReceived;

    public int Pgid => _pgid;
    public bool IsRunning
    {
        get
        {
            if (_process == null) return false;
            try { _process.Refresh(); } catch { }
            return !_process.HasExited;
        }
    }

    public void StartProcess(ProcessStartInfo startInfo)
    {
        if (IsRunning) return;

        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        _process = new Process { StartInfo = startInfo };

        _process.OutputDataReceived += (_, e) => HandleLogLine(e.Data);
        _process.ErrorDataReceived += (_, e) => HandleLogLine(e.Data);

        _process.Start();

        _pgid = _process.Id;

        try
        {
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }
        catch { }

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                UnixNative.setpgid(_process.Id, _process.Id);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TunnelProcessGroupManager] Failed to setpgid: {ex.Message}");
        }
    }

    public void StopProcessGroup()
    {
        if (_process == null || _process.HasExited) return;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                UnixNative.kill(-_pgid, UnixNative.SIGTERM);
                Task.Delay(500).Wait();
                try { _process.Refresh(); } catch { }

                if (!_process.HasExited)
                {
                    UnixNative.kill(-_pgid, UnixNative.SIGKILL);
                    Task.Delay(500).Wait();
                    try { _process.Refresh(); } catch { }
                }

                if (!_process.HasExited)
                {
                    _process.Kill(true);
                }
            }
            else
            {
                _process.Kill(true);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TunnelProcessGroupManager] Failed to kill process group: {ex.Message}");
            try { _process.Kill(true); } catch { }
        }
    }

    public IReadOnlyList<string> GetRecentLogs() => _logBuffer.ToArray();

    public void ClearLogs()
    {
        while (_logBuffer.TryDequeue(out _)) { }
    }

    private void HandleLogLine(string? data)
    {
        if (data == null) return;

        _logBuffer.Enqueue(data);
        while (_logBuffer.Count > MaxLogBufferLines && _logBuffer.TryDequeue(out _)) { }

        try
        {
            OutputLineReceived?.Invoke(data);
        }
        catch { }
    }
}
