using System;

namespace PocketMC.Core.Services;

public interface ISleepPreventionService : IDisposable
{
    bool IsPreventingSleep { get; }
    void PreventSleep();
    void AllowSleep();
}
