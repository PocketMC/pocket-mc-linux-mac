using System;

namespace PocketMC.Core.Services;

public interface IDiscordRpcService : IDisposable
{
    void Initialize();
    void UpdatePresence();
    void Shutdown();
}
