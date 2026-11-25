using System;
using System.Threading;
using System.Threading.Tasks;
using PocketMC.Core.Models;

namespace PocketMC.Core.Services;

public interface IServerLifecycleService
{
    event Action<string, string>? InstanceStateChanged;
    event Action<string, int>? RestartCountdownTick;

    Task StartAsync(ServerInstance instance, bool isAutoRestart = false);
    Task StopAsync(ServerInstance instance);
    Task RestartAsync(ServerInstance instance, CancellationToken cancellationToken = default);
    void Kill(ServerInstance instance);
    void AbortRestartDelay(string slug);
    bool IsRunning(string slug);
    bool IsWaitingToRestart(string slug);
    DateTime? GetSessionStartTime(string slug);
}
