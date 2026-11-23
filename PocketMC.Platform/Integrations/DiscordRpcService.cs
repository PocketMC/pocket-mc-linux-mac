using System;
using DiscordRPC;
using Microsoft.Extensions.Logging;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.Platform.Integrations;

public sealed class DiscordRpcService : IDiscordRpcService
{
    public const string DiscordApplicationId = "1507742265240715354";
    public const string AppWebsite = "https://pocketmc.net";

    private readonly ISettingsService? _settingsService;
    private readonly ILogger<DiscordRpcService>? _logger;
    private DiscordRpcClient? _client;
    private readonly object _lock = new();
    private bool _disposed;

    public DiscordRpcService(ISettingsService? settingsService = null, ILogger<DiscordRpcService>? logger = null)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public void Initialize()
    {
        lock (_lock)
        {
            if (_disposed) return;

            var rpcSettings = _settingsService?.Settings?.DiscordRpc;
            if (rpcSettings == null || !rpcSettings.Enabled)
            {
                _logger?.LogInformation("Discord RPC is disabled in settings. Skipping initialization.");
                return;
            }

            if (_client != null && !_client.IsDisposed)
            {
                return;
            }

            try
            {
                _client = new DiscordRpcClient(DiscordApplicationId);

                _client.OnReady += (_, e) =>
                {
                    _logger?.LogInformation("Discord RPC connected for user {Username}.", e.User.Username);
                    UpdatePresence();
                };

                _client.OnConnectionFailed += (_, e) =>
                {
                    _logger?.LogDebug("Discord RPC connection failed (pipe {Pipe}).", e.FailedPipe);
                };

                _client.Initialize();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to initialize Discord RPC client.");
            }
        }
    }

    public void UpdatePresence()
    {
        lock (_lock)
        {
            if (_disposed || _client == null || _client.IsDisposed) return;

            var rpcSettings = _settingsService?.Settings?.DiscordRpc;
            if (rpcSettings == null || !rpcSettings.Enabled)
            {
                _client.ClearPresence();
                return;
            }

            var presence = BuildIdlePresence(rpcSettings);
            _client.SetPresence(presence);
        }
    }

    public void SetServerPresence(string serverName, string version, string loader, int playerCount, bool isOnline)
    {
        lock (_lock)
        {
            if (_disposed || _client == null || _client.IsDisposed) return;

            var rpcSettings = _settingsService?.Settings?.DiscordRpc;
            if (rpcSettings == null || !rpcSettings.Enabled)
            {
                _client.ClearPresence();
                return;
            }

            var presence = BuildServerPresence(serverName, version, loader, playerCount, isOnline, rpcSettings);
            _client.SetPresence(presence);
        }
    }

    public static RichPresence BuildIdlePresence(DiscordRpcSettings rpcSettings)
    {
        var presence = new RichPresence
        {
            Details = "Managing Servers",
            State = "Idle",
            Assets = new Assets
            {
                LargeImageKey = "pocketmc",
                LargeImageText = "PocketMC Desktop"
            }
        };

        if (rpcSettings.ShowDownloadButton)
        {
            presence.Buttons = new[]
            {
                new Button { Label = "Download PocketMC", Url = AppWebsite }
            };
        }

        return presence;
    }

    public static RichPresence BuildServerPresence(
        string serverName,
        string version,
        string loader,
        int playerCount,
        bool isOnline,
        DiscordRpcSettings rpcSettings)
    {
        string details = rpcSettings.ShowServerName ? serverName : "Minecraft Server";
        string state;

        if (!isOnline)
        {
            state = "Offline";
        }
        else
        {
            string versionStr = rpcSettings.ShowVersionAndEngine ? $"{loader} {version}" : "Online";
            string playerStr = rpcSettings.ShowPlayerCount ? $" ({playerCount} online)" : string.Empty;
            state = $"{versionStr}{playerStr}";
        }

        var presence = new RichPresence
        {
            Details = details,
            State = state,
            Assets = new Assets
            {
                LargeImageKey = "pocketmc",
                LargeImageText = serverName
            }
        };

        if (rpcSettings.ShowDownloadButton)
        {
            presence.Buttons = new[]
            {
                new Button { Label = "Download PocketMC", Url = AppWebsite }
            };
        }

        return presence;
    }

    public void Shutdown()
    {
        lock (_lock)
        {
            if (_client != null && !_client.IsDisposed)
            {
                try
                {
                    _client.ClearPresence();
                    _client.Dispose();
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Error during Discord RPC shutdown.");
                }
            }

            _client = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Shutdown();
    }
}
