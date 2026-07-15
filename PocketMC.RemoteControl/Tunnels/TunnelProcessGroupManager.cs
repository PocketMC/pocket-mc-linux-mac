using System;
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
    private Process? _process;
    private int _pgid;

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

        _process = new Process { StartInfo = startInfo };
        _process.Start();

        _pgid = _process.Id;
        
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
}
