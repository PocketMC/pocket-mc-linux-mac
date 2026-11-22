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

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private bool _isOp;

    [ObservableProperty]
    private bool _isBanned;

    [ObservableProperty]
    private bool _isOpLoading;

    [ObservableProperty]
    private string _selectedGameMode = "survival";

    [ObservableProperty]
    private bool _isReasonPromptVisible;

    [ObservableProperty]
    private string _pendingReason = string.Empty;

    [ObservableProperty]
    private string _pendingReasonAction = string.Empty;

    public ObservableCollection<string> GameModes { get; } = new(new[] { "survival", "creative", "adventure", "spectator" });

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

    [RelayCommand]
    public async Task ToggleOpAsync()
    {
        if (_toggleOpAsync != null)
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
            SelectedGameMode = newMode;
            await _changeGamemodeAsync(this, newMode);
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
            }
        }
    }

    [RelayCommand]
    public void CancelReason()
    {
        IsReasonPromptVisible = false;
        PendingReason = string.Empty;
    }
}
