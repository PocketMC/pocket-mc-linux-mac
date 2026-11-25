using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using PocketMC.Core.Models;

namespace PocketMC.App.ViewModels
{
    public partial class InstanceCardViewModel : ObservableObject
    {
        private ServerInstance _instance;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsRunning))]
        [NotifyPropertyChangedFor(nameof(IsSettledOnline))]
        [NotifyPropertyChangedFor(nameof(ShowRunningBorder))]
        [NotifyPropertyChangedFor(nameof(IsBusy))]
        [NotifyPropertyChangedFor(nameof(ShowRunningControls))]
        [NotifyPropertyChangedFor(nameof(ShowStoppedControls))]
        [NotifyPropertyChangedFor(nameof(StatusBrush))]
        [NotifyPropertyChangedFor(nameof(ShowPrimaryTunnelSkeleton))]
        [NotifyPropertyChangedFor(nameof(ShowBedrockIp))]
        [NotifyPropertyChangedFor(nameof(ShowVoiceChatRow))]
        private string _statusText = "Stopped";

        [ObservableProperty]
        private string _cpuText = "· · ·";

        [ObservableProperty]
        private string _ramText = "· · ·";

        [ObservableProperty]
        private string _playerStatus = "· · ·";

        [ObservableProperty]
        private string _uptime = "00:00:00";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowRunningControls))]
        [NotifyPropertyChangedFor(nameof(ShowStoppedControls))]
        [NotifyPropertyChangedFor(nameof(StopButtonText))]
        [NotifyPropertyChangedFor(nameof(IsRestartCountdownActive))]
        [NotifyPropertyChangedFor(nameof(RestartCountdownText))]
        private bool _isWaitingToRestart;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsRestartCountdownActive))]
        [NotifyPropertyChangedFor(nameof(RestartCountdownText))]
        private int _restartCountdownSeconds;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasTunnelAddress))]
        [NotifyPropertyChangedFor(nameof(ShowPrimaryTunnelSkeleton))]
        private string? _tunnelAddress;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasNumericTunnelAddress))]
        private string? _numericTunnelAddress;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasBedrockTunnelAddress))]
        [NotifyPropertyChangedFor(nameof(ShowBedrockIp))]
        [NotifyPropertyChangedFor(nameof(BedrockIpDisplayText))]
        [NotifyPropertyChangedFor(nameof(ShowGeyserHostnameSkeleton))]
        private string? _bedrockTunnelAddress;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasBedrockNumericTunnelAddress))]
        [NotifyPropertyChangedFor(nameof(ShowBedrockNumericSkeleton))]
        [NotifyPropertyChangedFor(nameof(ShowGeyserNumericSkeleton))]
        private string? _bedrockNumericTunnelAddress;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasVoiceChatTunnelAddress))]
        [NotifyPropertyChangedFor(nameof(ShowVoiceChatRow))]
        private string? _voiceChatTunnelAddress;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasVoiceChatNumericTunnelAddress))]
        [NotifyPropertyChangedFor(nameof(ShowVoiceChatNumericRow))]
        private string? _voiceChatNumericTunnelAddress;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasLanAddress))]
        [NotifyPropertyChangedFor(nameof(BedrockIpDisplayText))]
        private string? _lanAddressDisplayText;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowPrimaryTunnelSkeleton))]
        [NotifyPropertyChangedFor(nameof(ShowBedrockNumericSkeleton))]
        [NotifyPropertyChangedFor(nameof(ShowGeyserHostnameSkeleton))]
        [NotifyPropertyChangedFor(nameof(ShowGeyserNumericSkeleton))]
        private bool _isTunnelResolving;

        private DateTime? _sessionStartTime;
        private bool _showCrossPlayBadge;
        private bool _showVoiceChatBadge;
        private bool _hasPortIssue;
        private string _portIssueBadgeText = string.Empty;
        private string _portIssueTooltip = string.Empty;

        public InstanceCardViewModel(ServerInstance instance)
        {
            _instance = instance;
        }

        public ServerInstance Instance => _instance;
        public Guid Id => _instance.Id;
        public string Name => _instance.Name;
        public string Slug => _instance.Slug;
        public string Description => _instance.Description;
        public bool IsPinned => _instance.PinnedAt.HasValue;
        public string PinMenuHeader => IsPinned ? "Unpin from Top" : "Pin to Top";
        public string PinButtonTooltip => IsPinned ? "Pinned to Top" : "Pin to Top";
        public string MinecraftVersion => _instance.EngineVersion;
        public string ServerType => _instance.EngineType.ToString();
        public int MaxPlayers => _instance.MaxPlayers;
        public int MaxRamMb => _instance.MaxRamMb;
        public bool IsImportedFromModpack => _instance.IsImportedFromModpack;
        public string ModpackBadgeText => "Modpack";
        public string ModpackBadgeTooltip => string.IsNullOrWhiteSpace(_instance.ModpackName)
            ? "Imported from a modpack archive."
            : $"Imported from modpack: {_instance.ModpackName}";
        public bool IsPumpkinServer => _instance.EngineType == EngineType.Pumpkin;

        public bool IsRunning =>
            string.Equals(StatusText, "Online", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(StatusText, "Running", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(StatusText, "Starting", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(StatusText, "SettingUp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(StatusText, "Installing", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(StatusText, "Stopping", StringComparison.OrdinalIgnoreCase);

        public bool IsSettledOnline =>
            string.Equals(StatusText, "Online", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(StatusText, "Running", StringComparison.OrdinalIgnoreCase);

        public bool ShowRunningBorder => IsSettledOnline;

        public bool IsBusy =>
            string.Equals(StatusText, "Starting", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(StatusText, "SettingUp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(StatusText, "Installing", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(StatusText, "Stopping", StringComparison.OrdinalIgnoreCase);

        public bool ShowRunningControls => IsRunning || IsWaitingToRestart;
        public bool ShowStoppedControls => !ShowRunningControls;
        public string StopButtonText => IsWaitingToRestart ? "Cancel Restart" : "Stop";
        public bool IsRestartCountdownActive => IsWaitingToRestart && RestartCountdownSeconds > 0;
        public string RestartCountdownText => IsRestartCountdownActive ? $"Restarting in {RestartCountdownSeconds}s..." : string.Empty;

        public string StatusBrush => StatusText switch
        {
            "Online" or "Running" => "#10b981",
            "Starting" or "SettingUp" or "Installing" => "#f59e0b",
            "Stopping" or "Crashed" => "#ef4444",
            _ => "#9ca3af"
        };

        public string LastPlayedLabelText => IsRunning ? "Uptime" : "Last played";

        public string LastPlayedValueText
        {
            get
            {
                if (IsRunning)
                {
                    return Uptime;
                }
                if (_instance.LastPlayedAt == null)
                {
                    return "Never";
                }
                return FormatRelativeTime(_instance.LastPlayedAt.Value);
            }
        }

        public string LastPlayedTooltip
        {
            get
            {
                if (IsRunning && _sessionStartTime.HasValue)
                {
                    return $"Started {_sessionStartTime.Value.ToLocalTime():MMM d, yyyy · HH:mm:ss}";
                }
                if (_instance.LastPlayedAt == null)
                {
                    return "Never started";
                }
                return $"Last active {_instance.LastPlayedAt.Value.ToLocalTime():MMM d, yyyy · HH:mm:ss}";
            }
        }

        public string CreatedValueText => _instance.CreatedAt.ToLocalTime().ToString("MMM d, yyyy", CultureInfo.CurrentCulture);

        public string PlatformSummaryText => _instance.EngineType switch
        {
            EngineType.Bedrock or EngineType.PocketMine => "Bedrock",
            EngineType.Pumpkin => "Pumpkin (Java + Bedrock)",
            _ => "Java"
        };

        public bool IsBedrockServer => _instance.EngineType == EngineType.Bedrock || _instance.EngineType == EngineType.PocketMine;
        public bool ShowBedrockIp => IsRunning && (IsBedrockServer || IsPumpkinServer || ShowCrossPlayBadge || HasBedrockTunnelAddress);
        public string BedrockIpLabel => IsPumpkinServer ? "Bedrock (NetherNet):" : "Bedrock (Geyser):";
        public string BedrockIpDisplayText =>
            !string.IsNullOrWhiteSpace(BedrockTunnelAddress)
                ? BedrockTunnelAddress
                : (!string.IsNullOrWhiteSpace(LanAddressDisplayText)
                    ? $"{LanAddressDisplayText.Split(':')[0]}:{_instance.GeyserBedrockPort ?? _instance.PumpkinBedrockPort ?? 19132} (local)"
                    : $"127.0.0.1:{_instance.GeyserBedrockPort ?? 19132} (local)");

        public bool ShowCrossPlayBadge => _showCrossPlayBadge;
        public string CrossPlayBadgeText => "Cross-play";
        public string CrossPlayBadgeTooltip => "Java and Bedrock players can join through Geyser/Floodgate.";

        public bool ShowVoiceChatBadge => _showVoiceChatBadge;
        public string VoiceChatBadgeText => "Voice Chat";
        public string VoiceChatBadgeTooltip => "Simple Voice Chat is configured on this server.";

        public bool HasPortIssue => _hasPortIssue;
        public string PortIssueBadgeText => _portIssueBadgeText;
        public string PortIssueTooltip => _portIssueTooltip;

        public void SetBadges(bool crossPlay, bool voiceChat)
        {
            _showCrossPlayBadge = crossPlay;
            _showVoiceChatBadge = voiceChat;
            OnPropertyChanged(nameof(ShowCrossPlayBadge));
            OnPropertyChanged(nameof(ShowVoiceChatBadge));
            OnPropertyChanged(nameof(ShowBedrockIp));
            OnPropertyChanged(nameof(ShowVoiceChatRow));
        }

        public void SetPortIssue(string badgeText, string tooltip)
        {
            _hasPortIssue = !string.IsNullOrWhiteSpace(badgeText);
            _portIssueBadgeText = badgeText;
            _portIssueTooltip = tooltip;
            OnPropertyChanged(nameof(HasPortIssue));
            OnPropertyChanged(nameof(PortIssueBadgeText));
            OnPropertyChanged(nameof(PortIssueTooltip));
        }

        public void ClearPortIssue()
        {
            if (!_hasPortIssue) return;
            _hasPortIssue = false;
            _portIssueBadgeText = string.Empty;
            _portIssueTooltip = string.Empty;
            OnPropertyChanged(nameof(HasPortIssue));
            OnPropertyChanged(nameof(PortIssueBadgeText));
            OnPropertyChanged(nameof(PortIssueTooltip));
        }

        public bool HasTunnelAddress => !string.IsNullOrWhiteSpace(TunnelAddress);
        public bool HasNumericTunnelAddress => !string.IsNullOrWhiteSpace(NumericTunnelAddress);
        public bool HasBedrockTunnelAddress => !string.IsNullOrWhiteSpace(BedrockTunnelAddress);
        public bool HasBedrockNumericTunnelAddress => !string.IsNullOrWhiteSpace(BedrockNumericTunnelAddress);
        public bool HasVoiceChatTunnelAddress => !string.IsNullOrWhiteSpace(VoiceChatTunnelAddress);
        public bool HasVoiceChatNumericTunnelAddress => !string.IsNullOrWhiteSpace(VoiceChatNumericTunnelAddress);
        public bool HasLanAddress => !string.IsNullOrWhiteSpace(LanAddressDisplayText);

        public bool ShowPrimaryTunnelSkeleton => IsRunning && IsTunnelResolving && !HasTunnelAddress;
        public bool ShowBedrockNumericSkeleton => IsRunning && IsBedrockServer && IsTunnelResolving && !HasBedrockNumericTunnelAddress;
        public bool ShowGeyserHostnameSkeleton => IsRunning && ShowCrossPlayBadge && IsTunnelResolving && !HasBedrockTunnelAddress;
        public bool ShowGeyserNumericSkeleton => IsRunning && ShowCrossPlayBadge && IsTunnelResolving && !HasBedrockNumericTunnelAddress;
        public bool ShowVoiceChatRow => IsRunning && (HasVoiceChatTunnelAddress || ShowVoiceChatBadge);
        public bool ShowVoiceChatNumericRow => IsRunning && HasVoiceChatNumericTunnelAddress;

        public void UpdateState(string newState, DateTime? sessionStart = null)
        {
            StatusText = newState;

            if (sessionStart.HasValue)
            {
                _sessionStartTime = sessionStart;
            }
            else if ((string.Equals(newState, "Online", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(newState, "Running", StringComparison.OrdinalIgnoreCase)) && _sessionStartTime == null)
            {
                _sessionStartTime = DateTime.UtcNow;
            }
            else if (string.Equals(newState, "Stopped", StringComparison.OrdinalIgnoreCase) || string.Equals(newState, "Crashed", StringComparison.OrdinalIgnoreCase))
            {
                _sessionStartTime = null;
                Uptime = "00:00:00";
                CpuText = "· · ·";
                RamText = "· · ·";
                PlayerStatus = "· · ·";
                RestartCountdownSeconds = 0;
                TunnelAddress = null;
                NumericTunnelAddress = null;
                BedrockTunnelAddress = null;
                BedrockNumericTunnelAddress = null;
                VoiceChatTunnelAddress = null;
                VoiceChatNumericTunnelAddress = null;
                LanAddressDisplayText = null;
                IsTunnelResolving = false;
            }

            if (!IsRunning)
            {
                OnPropertyChanged(nameof(LastPlayedLabelText));
                OnPropertyChanged(nameof(LastPlayedValueText));
                OnPropertyChanged(nameof(LastPlayedTooltip));
                return;
            }

            if (_sessionStartTime.HasValue)
            {
                Uptime = FormatUptime(DateTime.UtcNow - _sessionStartTime.Value);
            }
            OnPropertyChanged(nameof(LastPlayedLabelText));
            OnPropertyChanged(nameof(LastPlayedValueText));
            OnPropertyChanged(nameof(LastPlayedTooltip));
        }

        public void UpdateRestartCountdown(int secondsRemaining)
        {
            RestartCountdownSeconds = Math.Max(0, secondsRemaining);
            IsWaitingToRestart = RestartCountdownSeconds > 0;
        }

        public void UpdateMetrics(double cpuPercent, double ramUsedMb, int playerCount)
        {
            CpuText = $"{Math.Round(cpuPercent)}%";
            double ramGb = ramUsedMb / 1024.0;
            double maxGb = MaxRamMb / 1024.0;
            RamText = $"{ramGb:F1} / {maxGb:F0} GB";
            PlayerStatus = $"{playerCount} / {MaxPlayers}";

            if (_sessionStartTime.HasValue)
            {
                Uptime = FormatUptime(DateTime.UtcNow - _sessionStartTime.Value);
            }

            OnPropertyChanged(nameof(LastPlayedValueText));
        }

        public void UpdateTunnels(string? javaTunnel, string? numericTunnel, string? bedrockTunnel, string? bedrockNumericTunnel, string? lanAddress)
        {
            TunnelAddress = javaTunnel;
            NumericTunnelAddress = numericTunnel;
            BedrockTunnelAddress = bedrockTunnel;
            BedrockNumericTunnelAddress = bedrockNumericTunnel;
            LanAddressDisplayText = lanAddress;
        }

        public void UpdateVoiceChatTunnel(string? voiceChatTunnel, string? voiceChatNumericTunnel = null)
        {
            VoiceChatTunnelAddress = voiceChatTunnel;
            VoiceChatNumericTunnelAddress = voiceChatNumericTunnel;
        }

        public void NotifyUptimeChanged()
        {
            if (IsRunning && _sessionStartTime.HasValue)
            {
                Uptime = FormatUptime(DateTime.UtcNow - _sessionStartTime.Value);
            }
            OnPropertyChanged(nameof(LastPlayedLabelText));
            OnPropertyChanged(nameof(LastPlayedValueText));
            OnPropertyChanged(nameof(LastPlayedTooltip));
        }

        public void SetPinned(bool pinned)
        {
            _instance.PinnedAt = pinned ? DateTime.UtcNow : null;
            OnPropertyChanged(nameof(IsPinned));
            OnPropertyChanged(nameof(PinMenuHeader));
            OnPropertyChanged(nameof(PinButtonTooltip));
        }

        private static string FormatUptime(TimeSpan span)
        {
            if (span.TotalDays >= 1)
            {
                return $"{(int)span.TotalDays}d {span.Hours}h {span.Minutes}m";
            }
            if (span.TotalHours >= 1)
            {
                return $"{(int)span.TotalHours}h {span.Minutes}m {span.Seconds}s";
            }
            return $"{span.Minutes:D2}:{span.Seconds:D2}";
        }

        private static string FormatRelativeTime(DateTime pastUtc)
        {
            var diff = DateTime.UtcNow - pastUtc;
            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 30) return $"{(int)diff.TotalDays}d ago";
            return pastUtc.ToLocalTime().ToString("MMM d, yyyy", CultureInfo.CurrentCulture);
        }
    }
}
