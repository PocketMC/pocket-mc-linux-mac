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
        [NotifyPropertyChangedFor(nameof(IsBusy))]
        [NotifyPropertyChangedFor(nameof(ShowRunningControls))]
        [NotifyPropertyChangedFor(nameof(ShowStoppedControls))]
        [NotifyPropertyChangedFor(nameof(StatusBrush))]
        [NotifyPropertyChangedFor(nameof(LastPlayedLabelText))]
        [NotifyPropertyChangedFor(nameof(LastPlayedValueText))]
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
        private bool _isWaitingToRestart;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasTunnelAddress))]
        private string? _tunnelAddress;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasNumericTunnelAddress))]
        private string? _numericTunnelAddress;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasBedrockTunnelAddress))]
        [NotifyPropertyChangedFor(nameof(ShowBedrockIp))]
        private string? _bedrockTunnelAddress;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasBedrockNumericTunnelAddress))]
        private string? _bedrockNumericTunnelAddress;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasLanAddress))]
        private string? _lanAddressDisplayText;

        [ObservableProperty]
        private bool _isTunnelResolving;

        private DateTime? _sessionStartTime;

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
        public string MinecraftVersion => _instance.EngineVersion;
        public string ServerType => _instance.EngineType.ToString();
        public int MaxPlayers => _instance.MaxPlayers;
        public int MaxRamMb => _instance.MaxRamMb;

        public bool IsRunning =>
            string.Equals(StatusText, "Online", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(StatusText, "Starting", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(StatusText, "SettingUp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(StatusText, "Installing", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(StatusText, "Stopping", StringComparison.OrdinalIgnoreCase);

        public bool IsBusy =>
            string.Equals(StatusText, "Starting", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(StatusText, "SettingUp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(StatusText, "Installing", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(StatusText, "Stopping", StringComparison.OrdinalIgnoreCase);

        public bool ShowRunningControls => IsRunning || IsWaitingToRestart;
        public bool ShowStoppedControls => !ShowRunningControls;
        public string StopButtonText => IsWaitingToRestart ? "Abort" : "Stop";

        public string StatusBrush => StatusText switch
        {
            "Online" => "#10b981", // Emerald green
            "Starting" or "SettingUp" or "Installing" => "#f59e0b", // Amber
            "Stopping" => "#ef4444", // Red
            "Crashed" => "#ef4444",
            _ => "#9ca3af" // Muted gray
        };

        public string LastPlayedLabelText => IsRunning ? "Uptime" : "Last played";

        public string LastPlayedValueText
        {
            get
            {
                if (IsRunning)
                {
                    if (_sessionStartTime.HasValue)
                    {
                        var elapsed = DateTime.UtcNow - _sessionStartTime.Value;
                        return FormatUptime(elapsed);
                    }
                    return "Just started";
                }

                return _instance.LastPlayedAt.HasValue
                    ? FormatRelativeTime(_instance.LastPlayedAt.Value)
                    : "Never";
            }
        }

        public string LastPlayedTooltip
        {
            get
            {
                if (IsRunning && _sessionStartTime.HasValue)
                {
                    return $"Started at: {_sessionStartTime.Value.ToLocalTime().ToString("MMM d, yyyy h:mm tt", CultureInfo.CurrentCulture)}";
                }

                return _instance.LastPlayedAt.HasValue
                    ? _instance.LastPlayedAt.Value.ToLocalTime().ToString("MMM d, yyyy h:mm tt", CultureInfo.CurrentCulture)
                    : "Never played";
            }
        }

        public string CreatedValueText => _instance.CreatedAt.ToLocalTime().ToString("MMM d, yyyy", CultureInfo.CurrentCulture);

        public string PlatformSummaryText => _instance.EngineType switch
        {
            EngineType.Bedrock or EngineType.PocketMine => "Bedrock",
            _ => "Java"
        };

        public bool IsBedrockServer => _instance.EngineType == EngineType.Bedrock || _instance.EngineType == EngineType.PocketMine;
        public bool ShowBedrockIp => IsBedrockServer || HasBedrockTunnelAddress;

        public bool ShowCrossPlayBadge => _showCrossPlayBadge;
        private bool _showCrossPlayBadge;
        public string CrossPlayBadgeText => "Cross-play";
        public string CrossPlayBadgeTooltip => "Java and Bedrock players can join through Geyser/Floodgate.";

        public bool ShowVoiceChatBadge => _showVoiceChatBadge;
        private bool _showVoiceChatBadge;
        public string VoiceChatBadgeTooltip => "Simple Voice Chat is configured on this server.";

        public void SetBadges(bool crossPlay, bool voiceChat)
        {
            _showCrossPlayBadge = crossPlay;
            _showVoiceChatBadge = voiceChat;
            OnPropertyChanged(nameof(ShowCrossPlayBadge));
            OnPropertyChanged(nameof(ShowVoiceChatBadge));
        }

        public bool HasTunnelAddress => !string.IsNullOrWhiteSpace(TunnelAddress);
        public bool HasNumericTunnelAddress => !string.IsNullOrWhiteSpace(NumericTunnelAddress);
        public bool HasBedrockTunnelAddress => !string.IsNullOrWhiteSpace(BedrockTunnelAddress);
        public bool HasBedrockNumericTunnelAddress => !string.IsNullOrWhiteSpace(BedrockNumericTunnelAddress);
        public bool HasLanAddress => !string.IsNullOrWhiteSpace(LanAddressDisplayText);

        public void UpdateState(string newState, DateTime? sessionStart = null)
        {
            StatusText = newState;
            if (sessionStart.HasValue)
            {
                _sessionStartTime = sessionStart;
            }
            else if (string.Equals(newState, "Online", StringComparison.OrdinalIgnoreCase) && _sessionStartTime == null)
            {
                _sessionStartTime = DateTime.UtcNow;
            }
            else if (string.Equals(newState, "Stopped", StringComparison.OrdinalIgnoreCase) || string.Equals(newState, "Crashed", StringComparison.OrdinalIgnoreCase))
            {
                _sessionStartTime = null;
                CpuText = "· · ·";
                RamText = "· · ·";
                PlayerStatus = "· · ·";
                _instance.LastPlayedAt = DateTime.UtcNow;
            }

            NotifyUptimeChanged();
        }

        public void UpdateMetrics(double cpuPercent, double ramUsedMb, int playerCount)
        {
            if (!IsRunning)
            {
                CpuText = "· · ·";
                RamText = "· · ·";
                PlayerStatus = "· · ·";
                return;
            }

            CpuText = $"{Math.Round(cpuPercent):0}%";
            double maxRamGb = _instance.MaxRamMb / 1024.0;
            double usedRamGb = ramUsedMb / 1024.0;
            RamText = $"{usedRamGb:F1} / {maxRamGb:F0} GB";
            PlayerStatus = $"{playerCount} / {_instance.MaxPlayers}";

            NotifyUptimeChanged();
        }

        public void UpdateTunnels(string? javaTunnel, string? numericTunnel, string? bedrockTunnel, string? bedrockNumericTunnel, string? lanAddress)
        {
            TunnelAddress = javaTunnel;
            NumericTunnelAddress = numericTunnel;
            BedrockTunnelAddress = bedrockTunnel;
            BedrockNumericTunnelAddress = bedrockNumericTunnel;
            LanAddressDisplayText = lanAddress;
        }

        public void NotifyUptimeChanged()
        {
            OnPropertyChanged(nameof(LastPlayedValueText));
            OnPropertyChanged(nameof(LastPlayedTooltip));
            if (_sessionStartTime.HasValue)
            {
                var elapsed = DateTime.UtcNow - _sessionStartTime.Value;
                Uptime = FormatUptime(elapsed);
            }
        }

        public void SetPinned(bool pinned)
        {
            _instance.PinnedAt = pinned ? DateTime.UtcNow : null;
            OnPropertyChanged(nameof(IsPinned));
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
