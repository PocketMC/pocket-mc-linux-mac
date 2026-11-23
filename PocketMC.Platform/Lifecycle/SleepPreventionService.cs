using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using PocketMC.Core.Services;

namespace PocketMC.Platform.Lifecycle;

public sealed class SleepPreventionService : ISleepPreventionService
{
    private readonly ILogger<SleepPreventionService>? _logger;
    private readonly object _gate = new();
    private Process? _inhibitProcess;
    private bool _isPreventingSleep;
    private bool _disposed;

    [Flags]
    private enum ExecutionState : uint
    {
        ES_SYSTEM_REQUIRED = 0x00000001,
        ES_CONTINUOUS = 0x80000000
    }

    [DllImport("kernel32.dll", EntryPoint = "SetThreadExecutionState", SetLastError = true)]
    private static extern uint SetThreadExecutionState(uint esFlags);

    public SleepPreventionService(ILogger<SleepPreventionService>? logger = null)
    {
        _logger = logger;
    }

    public bool IsPreventingSleep
    {
        get
        {
            lock (_gate)
            {
                return _isPreventingSleep;
            }
        }
    }

    public void PreventSleep()
    {
        lock (_gate)
        {
            if (_disposed || _isPreventingSleep)
            {
                return;
            }

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    StartLinuxInhibitor();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    StartMacCaffeinate();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    SetThreadExecutionState((uint)(ExecutionState.ES_CONTINUOUS | ExecutionState.ES_SYSTEM_REQUIRED));
                }

                _isPreventingSleep = true;
                _logger?.LogInformation("System sleep prevention activated.");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to activate system sleep prevention.");
            }
        }
    }

    public void AllowSleep()
    {
        lock (_gate)
        {
            if (!_isPreventingSleep)
            {
                return;
            }

            try
            {
                StopInhibitProcess();

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    SetThreadExecutionState((uint)ExecutionState.ES_CONTINUOUS);
                }

                _isPreventingSleep = false;
                _logger?.LogInformation("System sleep prevention released.");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to release system sleep prevention.");
            }
        }
    }

    private void StartLinuxInhibitor()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "systemd-inhibit",
                Arguments = "--what=idle:sleep:shutdown --who=PocketMC --why=\"Minecraft server running\" --mode=block sleep infinity",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            _inhibitProcess = Process.Start(psi);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "systemd-inhibit not available or failed to start.");
        }
    }

    private void StartMacCaffeinate()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "caffeinate",
                Arguments = "-i -s",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            _inhibitProcess = Process.Start(psi);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "caffeinate not available or failed to start.");
        }
    }

    private void StopInhibitProcess()
    {
        if (_inhibitProcess != null)
        {
            try
            {
                if (!_inhibitProcess.HasExited)
                {
                    _inhibitProcess.Kill();
                    _inhibitProcess.WaitForExit(1000);
                }
            }
            catch
            {
                // Process may have already exited
            }
            finally
            {
                _inhibitProcess.Dispose();
                _inhibitProcess = null;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            AllowSleep();
        }
    }
}
