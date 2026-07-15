using System;
using System.Collections.Concurrent;
using System.Threading;

namespace PocketMC.RemoteControl.Services;

public class RemoteRequestLimiter
{
    private readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> _limits = new();
    private readonly int _maxRequestsPerMinute = 5;

    public bool AllowRequest(string ipAddress)
    {
        var now = DateTime.UtcNow;
        var entry = _limits.AddOrUpdate(ipAddress, 
            (1, now), 
            (key, oldValue) =>
            {
                if ((now - oldValue.WindowStart).TotalMinutes >= 1)
                {
                    return (1, now);
                }
                return (oldValue.Count + 1, oldValue.WindowStart);
            });

        return entry.Count <= _maxRequestsPerMinute;
    }
}
