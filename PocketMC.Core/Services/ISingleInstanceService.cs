using System;
using System.Threading.Tasks;

namespace PocketMC.Core.Services;

public interface ISingleInstanceService : IDisposable
{
    bool IsFirstInstance { get; }
    bool TryAcquireLock();
    void StartListening(Action<string[]> onArgumentsReceived);
    Task<bool> SendArgumentsToExistingInstanceAsync(string[] args);
    void RegisterUrlProtocol();
}
