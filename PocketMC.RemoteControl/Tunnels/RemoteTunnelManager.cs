using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.RemoteControl.Tunnels;

public class RemoteTunnelManager
{
    private readonly IEnumerable<IRemoteTunnelProvider> _providers;
    private readonly ISettingsService? _settingsService;

    public RemoteTunnelManager(IEnumerable<IRemoteTunnelProvider> providers, ISettingsService? settingsService = null)
    {
        _providers = providers;
        _settingsService = settingsService;
    }

    public RemoteTunnelStatus GetStatus()
    {
        var cloudflare = _providers.OfType<CloudflaredQuickTunnelProvider>().FirstOrDefault();
        if (cloudflare != null && cloudflare.IsRunning)
        {
            return new RemoteTunnelStatus
            {
                IsRunning = true,
                PublicUrl = cloudflare.PublicUrl
            };
        }

        var playit = _providers.OfType<PlayitHttpsTunnelProvider>().FirstOrDefault();
        if (playit != null && playit.IsRunning)
        {
            return new RemoteTunnelStatus
            {
                IsRunning = true,
                PublicUrl = null
            };
        }

        return new RemoteTunnelStatus
        {
            IsRunning = false
        };
    }

    public async Task<RemoteTunnelStartResult> StartAsync(CancellationToken cancellationToken = default)
    {
        string? providerId = _settingsService?.Settings.RemoteControl.TunnelProviderId;
        if (string.IsNullOrEmpty(providerId))
        {
            var mode = _settingsService?.Settings.RemoteControl.AccessMode ?? RemoteAccessMode.CloudflaredQuickTunnel;
            providerId = mode switch
            {
                RemoteAccessMode.PlayitHttpsTunnel => "playit-https",
                _ => "cloudflared-quick"
            };
        }

        var provider = _providers.FirstOrDefault(p => string.Equals(p.ProviderId, providerId, StringComparison.OrdinalIgnoreCase));
        if (provider == null)
        {
            return RemoteTunnelStartResult.Failed($"Remote tunnel provider '{providerId}' is not available.");
        }

        bool started = await provider.StartAsync();
        if (!started)
        {
            return RemoteTunnelStartResult.Failed($"Tunnel provider '{provider.DisplayName}' failed to start.");
        }

        string? publicUrl = (provider as CloudflaredQuickTunnelProvider)?.PublicUrl;
        return RemoteTunnelStartResult.Succeeded(publicUrl);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await StopAllTunnelsAsync();
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

    public TProvider? GetProvider<TProvider>(string providerId) where TProvider : class, IRemoteTunnelProvider
    {
        return _providers.FirstOrDefault(p => p.ProviderId == providerId) as TProvider;
    }
}
