using System;
using System.Collections.Generic;

namespace PocketMC.Core.Models
{
    public class Settings
    {
        public int Version { get; set; } = 1;
        public string? CustomDataRoot { get; set; }
        public Dictionary<string, Dictionary<string, string>> DownloadedRuntimes { get; set; } = new Dictionary<string, Dictionary<string, string>>();
        public string? LastSelectedInstanceSlug { get; set; }
        public string Theme { get; set; } = "System";

        // CurseForge
        public string? CurseForgeApiKey { get; set; }

        // External/Cloud Backups
        public string? ExternalBackupDirectory { get; set; }
        public CloudBackupSettings CloudBackups { get; set; } = new();
        public Dictionary<string, CloudOAuthTokenSet> CloudTokens { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        // AI Diagnostician
        public string AiProvider { get; set; } = "Gemini";
        public bool EnableAiSummarization { get; set; } = false;
        public Dictionary<string, string> AiApiKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> AiModels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> AiEndpoints { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public string? GetCurrentAiKey() => AiApiKeys.TryGetValue(AiProvider, out var key) ? key : null;
        public string? GetCurrentAiModel() => AiModels.TryGetValue(AiProvider, out var model) ? model : null;
        public string? GetCurrentAiEndpoint() => AiEndpoints.TryGetValue(AiProvider, out var ep) ? ep : null;

        // Remote Control & Networking
        public RemoteControlSettings RemoteControl { get; set; } = new();
        public PlayitPartnerConnection? PlayitPartnerConnection { get; set; } = new();

        // Updates
        public string UpdateChannel { get; set; } = "Stable";
        public bool EnableAutoUpdates { get; set; } = true;
        public string? SkipUpdateVersion { get; set; }
    }

    public class PlayitPartnerConnection
    {
        public string? ClaimCode { get; set; }
        public string? SecretKey { get; set; }
        public bool IsConfirmed { get; set; }
        public string? AgentId { get; set; }
    }
}
