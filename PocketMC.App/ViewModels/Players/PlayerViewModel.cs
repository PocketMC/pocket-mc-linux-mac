using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PocketMC.App.ViewModels.Players;

public partial class PlayerViewModel : ObservableObject
{
    private readonly Func<PlayerViewModel, Task>? _toggleOpAsync;
    private readonly Func<PlayerViewModel, string, Task>? _changeGamemodeAsync;
    private readonly Func<PlayerViewModel, string, string, Task<bool>>? _submitReasonAsync;
    private bool _suppressGamemodeCallback;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private bool _isOp;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BanButtonText))]
    [NotifyPropertyChangedFor(nameof(BanTooltip))]
    private bool _isBanned;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanToggleOp))]
    private bool _isOpLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChangeGamemode))]
    private bool _isGamemodeLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSendCommands))]
    [NotifyPropertyChangedFor(nameof(CanToggleOp))]
    [NotifyPropertyChangedFor(nameof(CanChangeGamemode))]
    [NotifyPropertyChangedFor(nameof(OfflineTooltip))]
    private bool _isServerOnline = true;

    [ObservableProperty]
    private string _selectedGameMode = "survival";

    [ObservableProperty]
    private string _confirmedGameMode = "survival";

    [ObservableProperty]
    private bool _isReasonPromptVisible;

    [ObservableProperty]
    private string _pendingReason = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReasonPromptText))]
    private string _pendingReasonAction = string.Empty;

    public ObservableCollection<string> GameModes { get; } = new(new[] { "survival", "creative", "adventure", "spectator" });

    public bool CanSendCommands => IsServerOnline;
    public bool CanToggleOp => CanSendCommands && !IsOpLoading;
    public bool CanChangeGamemode => CanSendCommands && !IsGamemodeLoading;
    public string ReasonPromptText => string.IsNullOrWhiteSpace(PendingReasonAction) ? "Reason" : $"{PendingReasonAction} reason";
    public string BanButtonText => IsBanned ? "Banned" : "Ban";
    public string BanTooltip => IsBanned ? "This player is already present in the persistent ban list." : "Ban this player";
    public string OfflineTooltip => CanSendCommands ? string.Empty : "Server must be online to send player commands.";
    public string GamemodeTooltip => "Switch player gamemode on live servers.";

    public PlayerViewModel(
        string name,
        Func<PlayerViewModel, Task>? toggleOpAsync = null,
        Func<PlayerViewModel, string, Task>? changeGamemodeAsync = null,
        Func<PlayerViewModel, string, string, Task<bool>>? submitReasonAsync = null)
    {
        _name = name;
        _toggleOpAsync = toggleOpAsync;
        _changeGamemodeAsync = changeGamemodeAsync;
        _submitReasonAsync = submitReasonAsync;
    }

    partial void OnSelectedGameModeChanged(string value)
    {
        if (_suppressGamemodeCallback || _changeGamemodeAsync == null || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        string normalized = NormalizeGameMode(value);
        if (!string.Equals(normalized, ConfirmedGameMode, StringComparison.OrdinalIgnoreCase) && CanChangeGamemode)
        {
            _ = ChangeGamemodeAsync(normalized);
        }
    }

    public void SetGameModeFromServer(string mode)
    {
        string normalized = NormalizeGameMode(mode);
        ConfirmedGameMode = normalized;
        _suppressGamemodeCallback = true;
        try
        {
            SelectedGameMode = normalized;
        }
        finally
        {
            _suppressGamemodeCallback = false;
        }

        IsGamemodeLoading = false;
    }

    [RelayCommand]
    public async Task ToggleOpAsync()
    {
        if (_toggleOpAsync != null && !IsOpLoading)
        {
            IsOpLoading = true;
            try
            {
                await _toggleOpAsync(this);
            }
            finally
            {
                IsOpLoading = false;
            }
        }
    }

    [RelayCommand]
    public async Task ChangeGamemodeAsync(string newMode)
    {
        if (_changeGamemodeAsync != null && !string.IsNullOrWhiteSpace(newMode))
        {
            IsGamemodeLoading = true;
            try
            {
                await _changeGamemodeAsync(this, NormalizeGameMode(newMode));
            }
            finally
            {
                IsGamemodeLoading = false;
            }
        }
    }

    [RelayCommand]
    public void PromptKick()
    {
        PendingReasonAction = "Kick";
        PendingReason = string.Empty;
        IsReasonPromptVisible = true;
    }

    [RelayCommand]
    public void PromptBan()
    {
        if (IsBanned)
        {
            return;
        }

        PendingReasonAction = "Ban";
        PendingReason = string.Empty;
        IsReasonPromptVisible = true;
    }

    [RelayCommand]
    public async Task ConfirmReasonAsync()
    {
        if (_submitReasonAsync != null)
        {
            bool success = await _submitReasonAsync(this, PendingReasonAction, PendingReason);
            if (success)
            {
                IsReasonPromptVisible = false;
                PendingReason = string.Empty;
                PendingReasonAction = string.Empty;
            }
        }
    }

    [RelayCommand]
    public void CancelReason()
    {
        IsReasonPromptVisible = false;
        PendingReason = string.Empty;
        PendingReasonAction = string.Empty;
    }

    private static string NormalizeGameMode(string? mode)
    {
        return (mode ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "creative" => "creative",
            "adventure" => "adventure",
            "spectator" => "spectator",
            _ => "survival"
        };
    }
}

public sealed partial class BannedPlayerViewModel : ObservableObject
{
    private readonly Func<BannedPlayerViewModel, Task> _pardonAsync;

    [ObservableProperty]
    private bool _isServerOnline;

    public BannedPlayerViewModel(
        string name,
        string reason,
        string bannedBy,
        string expires,
        bool isSidecar,
        bool isServerOnline,
        Func<BannedPlayerViewModel, Task> pardonAsync)
    {
        Name = name;
        Reason = reason;
        Banned = bannedBy;
        Expires = expires;
        IsSidecar = isSidecar;
        _isServerOnline = isServerOnline;
        _pardonAsync = pardonAsync;
        PardonCommand = new AsyncRelayCommand(() => _pardonAsync(this));
    }

    public string Name { get; }
    public string Reason { get; }
    public string Banned { get; }
    public string Expires { get; }
    public bool IsSidecar { get; }
    public string ReasonDisplay => string.IsNullOrWhiteSpace(Reason) ? "No reason recorded" : Reason;
    public string BannedDisplay => string.IsNullOrWhiteSpace(Banned) ? "console" : Banned;
    public string ExpiresDisplay => string.IsNullOrWhiteSpace(Expires) ? "forever" : Expires;
    public IAsyncRelayCommand PardonCommand { get; }
}

public sealed class WhitelistPlayerViewModel
{
    private readonly Func<string, Task> _removeAsync;

    public WhitelistPlayerViewModel(string name, Func<string, Task> removeAsync, string uuid = "")
    {
        Name = name;
        Uuid = uuid;
        _removeAsync = removeAsync;
        RemoveCommand = new AsyncRelayCommand(() => _removeAsync(Name));
    }

    public string Name { get; }
    public string Uuid { get; }
    public IAsyncRelayCommand RemoveCommand { get; }
}
