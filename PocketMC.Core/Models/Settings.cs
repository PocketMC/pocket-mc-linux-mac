using System;
using System.Collections.Generic;

namespace PocketMC.Core.Models
{
    public class Settings
    {
        public int Version { get; set; } = 1;
        public bool HasCompletedInitialSetup { get; set; } = false;
        public string? CustomDataRoot { get; set; }
        public Dictionary<string, Dictionary<string, string>> DownloadedRuntimes { get; set; } = new Dictionary<string, Dictionary<string, string>>();
        public string? LastSelectedInstanceSlug { get; set; }
        public string Theme { get; set; } = "System";

        // Appearance & Backdrop
        public string WindowBackdrop { get; set; } = "BlurredWallpaper";
        public string AccentColorMode { get; set; } = "Custom";
        public string? CustomAccentColor { get; set; } = "#008B00";
        public string? CustomBackgroundImagePath { get; set; }
        public double WallpaperBlurRadius { get; set; } = 80.0;
        public double WallpaperTintOpacity { get; set; } = 0.72;

        // Window State
        public double? WindowWidth { get; set; }
        public double? WindowHeight { get; set; }
        public bool IsWindowMaximized { get; set; }
        public bool StartMinimizedToTray { get; set; } = false;
        public bool MinimizeToTrayOnClose { get; set; } = false;

        // Power Management & Behavior
        public bool KeepComputerAwakeWhileServersRunning { get; set; } = true;
        public bool EnableTelemetry { get; set; } = true;

        // CurseForge
        public string? CurseForgeApiKey { get; set; }

        // External/Cloud Backups
        public string? ExternalBackupDirectory { get; set; }
        public CloudBackupSettings CloudBackups { get; set; } = new();
        public Dictionary<string, CloudOAuthTokenSet> CloudTokens { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        // AI Diagnostician & Session Summarization
        public string AiProvider { get; set; } = "Gemini";
        public bool EnableAiSummarization { get; set; } = false;
        public bool AutoSummarizeOnStop { get; set; } = true;
        public bool AutoSummarizeOnCrash { get; set; } = true;
        public string OllamaMode { get; set; } = "Local";
        public Dictionary<string, string> AiApiKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> AiModels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> AiEndpoints { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public string? GetCurrentAiKey() => AiApiKeys.TryGetValue(AiProvider, out var key) ? key : null;
        public string? GetCurrentAiModel() => AiModels.TryGetValue(AiProvider, out var model) ? model : null;
        public string? GetCurrentAiEndpoint() => AiEndpoints.TryGetValue(AiProvider, out var ep) ? ep : null;

        public bool IsAiConfigured()
        {
            if (string.Equals(AiProvider, "Ollama", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(OllamaMode, "Cloud", StringComparison.OrdinalIgnoreCase))
                {
                    return !string.IsNullOrWhiteSpace(GetCurrentAiKey());
                }

                return true;
            }

            if (string.Equals(AiProvider, "OpenAI-Compatible", StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(GetCurrentAiEndpoint());
            }

            return !string.IsNullOrWhiteSpace(GetCurrentAiKey());
        }

        // Remote Control & Networking
        public RemoteControlSettings RemoteControl { get; set; } = new();
        public PlayitPartnerConnection? PlayitPartnerConnection { get; set; } = new();

        // Discord Rich Presence & Bot Integration
        public DiscordRpcSettings DiscordRpc { get; set; } = new();
        public bool EnableDiscordRpc
        {
            get => DiscordRpc.Enabled;
            set => DiscordRpc.Enabled = value;
        }
        public string? DiscordUserId { get; set; }
        public string? DiscordApiUrl { get; set; }
        public string? DiscordApiKey { get; set; }
        public bool EnableDiscordNotifications { get; set; } = true;
        public bool EnableServerOnlineNotifications { get; set; } = true;
        public bool EnableAgentConnectNotifications { get; set; } = true;
        public bool EnableRemoteControlNotifications { get; set; } = true;
        public bool EnableAiSummaryNotifications { get; set; } = true;

        // Console Settings
        public int ConsoleBufferSize { get; set; } = 5000;

        // Updates
        public string UpdateChannel { get; set; } = "Stable";
        public bool EnableAutoUpdates { get; set; } = true;
        public string? SkipUpdateVersion { get; set; }
        public string? LastSeenChangelogVersion { get; set; }
    }

    public class PlayitPartnerConnection
    {
        public string? ClaimCode { get; set; }
        public string? SecretKey { get; set; }
        public bool IsConfirmed { get; set; }
        public string? AgentId { get; set; }
    }

    public class DiscordRpcSettings
    {
        public bool Enabled { get; set; } = true;
        public bool ShowIdle { get; set; } = true;
        public bool ShowServerName { get; set; } = false;
        public bool ShowServerAddress { get; set; } = false;
        public bool ShowPlayerCount { get; set; } = true;
        public bool ShowVersionAndEngine { get; set; } = true;
        public bool ShowUptimeTimer { get; set; } = true;
        public bool ShowSoftwareIcon { get; set; } = false;
        public bool ShowDownloadButton { get; set; } = true;
    }
}
