using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.RemoteControl.Hosting;
using PocketMC.RemoteControl.Models;
using PocketMC.RemoteControl.Tunnels;

namespace PocketMC.RemoteControl.Services;

public sealed class RemoteControlCoordinator
{
    private readonly ISettingsService _settingsService;
    private readonly RemoteDashboardHost? _dashboardHost;
    private readonly RemoteTunnelManager _tunnelManager;
    private readonly LocalNetworkAddressService _localNetworkAddressService;
    private readonly INotificationService? _notificationService;
    private readonly HttpClient _httpClient;
    private string? _lastNotifiedTunnelUrl;
    private string? _lastNotifiedUrl;

    public RemoteControlCoordinator(
        ISettingsService settingsService,
        RemoteDashboardHost? dashboardHost = null,
        RemoteTunnelManager? tunnelManager = null,
        LocalNetworkAddressService? localNetworkAddressService = null,
        INotificationService? notificationService = null,
        HttpClient? httpClient = null)
    {
        _settingsService = settingsService;
        _dashboardHost = dashboardHost;
        _tunnelManager = tunnelManager ?? new RemoteTunnelManager(Array.Empty<IRemoteTunnelProvider>(), settingsService);
        _localNetworkAddressService = localNetworkAddressService ?? new LocalNetworkAddressService();
        _notificationService = notificationService;
        _httpClient = httpClient ?? new HttpClient();
    }

    public RemoteDashboardStatus GetStatus()
    {
        RemoteControlSettings settings = _settingsService.Settings.RemoteControl;
        RemoteTunnelStatus tunnelStatus = _tunnelManager.GetStatus();
        return new RemoteDashboardStatus
        {
            Enabled = settings.Enabled,
            HostRunning = _dashboardHost?.IsRunning ?? false,
            Port = settings.Port,
            AccessMode = settings.AccessMode,
            LocalUrls = _localNetworkAddressService.GetLocalUrls(settings.Port),
            PublicUrl = tunnelStatus.PublicUrl,
            TunnelRunning = tunnelStatus.IsRunning,
            TunnelError = tunnelStatus.ErrorMessage,
            AllowRemoteConsoleCommands = settings.AllowRemoteConsoleCommands,
            AllowRemotePlayerActions = settings.AllowRemotePlayerActions,
            AllowRemoteServerSettings = settings.AllowRemoteServerSettings,
            AllowRemoteServerAddons = settings.AllowRemoteServerAddons,
            AllowRemoteFileManager = settings.AllowRemoteFileManager,
            AllowRemoteServerBackups = settings.AllowRemoteServerBackups
        };
    }

    public async Task StartHostAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.Settings;
        bool previousEnabled = settings.RemoteControl.Enabled;
        settings.RemoteControl.Enabled = true;

        try
        {
            if (_dashboardHost != null)
            {
                await _dashboardHost.StartAsync(cancellationToken);
            }
            _settingsService.Save();
        }
        catch
        {
            settings.RemoteControl.Enabled = previousEnabled;
            _settingsService.Save();
            throw;
        }
    }

    public async Task StopHostAsync(CancellationToken cancellationToken = default)
    {
        _lastNotifiedTunnelUrl = null;
        _settingsService.Settings.RemoteControl.Enabled = false;
        _settingsService.Save();
        await _tunnelManager.StopAsync(cancellationToken);
        if (_dashboardHost != null)
        {
            await _dashboardHost.StopAsync(cancellationToken);
        }
    }

    public async Task RestartAllAsync(CancellationToken cancellationToken = default)
    {
        if (!_settingsService.Settings.RemoteControl.Enabled)
        {
            return;
        }

        await StopAllAsync(cancellationToken);
        await StartTunnelAsync(cancellationToken);
    }

    public async Task<RemoteTunnelStartResult> StartTunnelAsync(CancellationToken cancellationToken = default)
    {
        if (!_settingsService.Settings.RemoteControl.Enabled)
        {
            return RemoteTunnelStartResult.Failed("Remote Control is disabled.");
        }

        if (_dashboardHost != null)
        {
            await _dashboardHost.StartAsync(cancellationToken);
        }
        var result = await _tunnelManager.StartAsync(cancellationToken);

        if (result.Success && !string.IsNullOrEmpty(result.PublicUrl))
        {
            _ = NotifyDiscordOfRemoteControlUrlAsync(result.PublicUrl);

            if (_settingsService.Settings.EnableRemoteControlNotifications &&
                _lastNotifiedTunnelUrl != result.PublicUrl &&
                _notificationService != null)
            {
                _lastNotifiedTunnelUrl = result.PublicUrl;
                _notificationService.ShowRemoteControlStarted();
            }
        }

        return result;
    }

    public async Task NotifyDiscordOfRemoteControlUrlAsync(string publicUrl)
    {
        var settings = _settingsService.Settings;
        if (!settings.EnableDiscordNotifications || string.IsNullOrEmpty(settings.DiscordUserId) || string.IsNullOrEmpty(settings.DiscordApiKey))
        {
            return;
        }

        if (_lastNotifiedUrl == publicUrl)
        {
            return;
        }

        var candidateUrls = new List<string>();
        if (!string.IsNullOrEmpty(settings.DiscordApiUrl))
        {
            candidateUrls.Add(settings.DiscordApiUrl);
        }

        if (candidateUrls.Count == 0)
        {
            return;
        }

        var payload = new
        {
            user_id = settings.DiscordUserId,
            message = $"Hey! Your PocketMC Remote Control Dashboard is now online at: {publicUrl}"
        };

        var jsonString = JsonSerializer.Serialize(payload);

        foreach (var url in candidateUrls)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{url.TrimEnd('/')}/send-dm")
                {
                    Content = new StringContent(jsonString, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.DiscordApiKey);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    _lastNotifiedUrl = publicUrl;
                    break;
                }
            }
            catch
            {
                // Ignore failure and try the next fallback URL
            }
        }
    }

    public Task StopTunnelAsync(CancellationToken cancellationToken = default)
    {
        _lastNotifiedTunnelUrl = null;
        return _tunnelManager.StopAsync(cancellationToken);
    }

    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        _lastNotifiedTunnelUrl = null;
        await _tunnelManager.StopAsync(cancellationToken);
        if (_dashboardHost != null)
        {
            await _dashboardHost.StopAsync(cancellationToken);
        }
    }
}
