using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PocketMC.RemoteControl.Tunnels;

public class RemoteTunnelManager
{
    private readonly IEnumerable<IRemoteTunnelProvider> _providers;

    public RemoteTunnelManager(IEnumerable<IRemoteTunnelProvider> providers)
    {
        _providers = providers;
    }

    public async Task StartTunnelAsync(string providerId)
    {
        var provider = _providers.FirstOrDefault(p => p.ProviderId == providerId);
        if (provider != null)
        {
            await provider.StartAsync();
        }
    }

    public async Task StopTunnelAsync(string providerId)
    {
        var provider = _providers.FirstOrDefault(p => p.ProviderId == providerId);
        if (provider != null)
        {
            await provider.StopAsync();
        }
    }

    public async Task StopAllTunnelsAsync()
    {
        foreach (var provider in _providers)
        {
            await provider.StopAsync();
        }
    }

    public async Task<string> GetTunnelStatusAsync(string providerId)
    {
        var provider = _providers.FirstOrDefault(p => p.ProviderId == providerId);
        if (provider != null)
        {
            return await provider.GetStatusAsync();
        }
        return "Not Found";
    }
}
