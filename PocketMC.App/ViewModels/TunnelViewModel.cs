using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PocketMC.App.Navigation;
using PocketMC.App.Services;
using PocketMC.App.Views;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;
using PocketMC.RemoteControl.Tunnels;

namespace PocketMC.App.ViewModels;

public partial class TunnelRowViewModel : ObservableObject
{
    public TunnelData Data { get; }
    public string Id => Data.Id;
    public string PublicAddress => Data.PublicAddress;
    public bool HasPublicAddress => Data.HasPublicAddress;
    public string TunnelTypeDisplay => Data.TunnelTypeDisplay;
    public string LimitErrorText => Data.LimitErrorText;
    public bool HasAgentOrigin => Data.HasAgentOrigin;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNameChanged))]
    private string _editableName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPortChanged))]
    private string _editablePort;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private string _inlineError = string.Empty;

    [ObservableProperty]
    private bool _hasInlineError;

    [ObservableProperty]
    private bool _isCopied;

    public bool IsNameChanged => !string.Equals(EditableName?.Trim(), Data.Name ?? string.Empty, StringComparison.Ordinal);
    public bool IsPortChanged => !string.Equals(EditablePort?.Trim(), Data.Port.ToString(), StringComparison.Ordinal);

    public IAsyncRelayCommand SaveNameCommand { get; }
    public IAsyncRelayCommand SavePortCommand { get; }
    public IAsyncRelayCommand ToggleEnabledCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }
    public IAsyncRelayCommand CopyAddressCommand { get; }

    public TunnelRowViewModel(
        TunnelData data,
        Func<TunnelRowViewModel, Task> onSaveName,
        Func<TunnelRowViewModel, Task> onSavePort,
        Func<TunnelRowViewModel, Task> onToggleEnabled,
        Func<TunnelRowViewModel, Task> onDelete,
        Func<TunnelRowViewModel, Task> onCopyAddress)
    {
        Data = data;
        _editableName = data.Name ?? string.Empty;
        _editablePort = data.Port.ToString();
        _isEnabled = data.IsEnabled;

        SaveNameCommand = new AsyncRelayCommand(() => onSaveName(this));
        SavePortCommand = new AsyncRelayCommand(() => onSavePort(this));
        ToggleEnabledCommand = new AsyncRelayCommand(() => onToggleEnabled(this));
        DeleteCommand = new AsyncRelayCommand(() => onDelete(this));
        CopyAddressCommand = new AsyncRelayCommand(() => onCopyAddress(this));
    }

    public void NotifyDataUpdated()
    {
        OnPropertyChanged(nameof(IsNameChanged));
        OnPropertyChanged(nameof(IsPortChanged));
    }
}

public partial class TunnelViewModel : ObservableObject
{
    private readonly PlayitApiClient _playitApiClient;
    private readonly RemoteTunnelManager _tunnelManager;
    private readonly ISettingsService _settingsService;
    private readonly PlayitStatusService? _statusService;
    private readonly IAppNavigationService? _navigationService;

    [ObservableProperty]
    private string _agentStatus = "Checking...";

    [ObservableProperty]
    private string _agentDetailText = "Manage your Playit.gg tunnel agent and public endpoints.";

    [ObservableProperty]
    private bool _isAgentRunning;

    [ObservableProperty]
    private bool _isLinked;

    [ObservableProperty]
    private string _agentId = "Not Configured";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private ObservableCollection<TunnelData> _tunnels = new();

    [ObservableProperty]
    private ObservableCollection<TunnelRowViewModel> _tunnelRows = new();

    [ObservableProperty]
    private string _newTunnelName = string.Empty;

    [ObservableProperty]
    private string _newTunnelType = "minecraft-java";

    [ObservableProperty]
    private int _newTunnelPort = 25565;

    [ObservableProperty]
    private bool _isCreatingTunnel;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasTunnels;

    [ObservableProperty]
    private string _emptyTunnelsMessage = "Link Playit or create a server tunnel to expose your local ports.";

    [ObservableProperty]
    private string _networkStatusBadgeText = "Checking...";

    [ObservableProperty]
    private string _networkStatusSummary = "Checking Playit.gg global routing infrastructure...";

    [ObservableProperty]
    private string _networkStatusBg = "#2610B981";

    [ObservableProperty]
    private string _networkStatusFg = "#10B981";

    public List<string> TunnelTypes { get; } = new()
    {
        "minecraft-java",
        "minecraft-bedrock",
        "mc-simple-voice-chat"
    };

    public TunnelViewModel(
        PlayitApiClient playitApiClient,
        RemoteTunnelManager tunnelManager,
        ISettingsService settingsService,
        PlayitStatusService? statusService = null,
        IAppNavigationService? navigationService = null)
    {
        _playitApiClient = playitApiClient;
        _tunnelManager = tunnelManager;
        _settingsService = settingsService;
        _statusService = statusService ?? App.Services?.GetService<PlayitStatusService>();
        _navigationService = navigationService ?? App.Services?.GetService<IAppNavigationService>();
    }

    [RelayCommand]
    public async Task RefreshAllAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            bool isLinked = _playitApiClient.HasPartnerConnection();
            string agentId = _playitApiClient.GetAgentId() ?? "Not Configured";
            string status = await _tunnelManager.GetTunnelStatusAsync("playit-https");
            bool isAgentRunning = string.Equals(status, "Running", StringComparison.OrdinalIgnoreCase);

            var tunnelsList = new List<TunnelData>();
            string errMsg = string.Empty;

            if (isLinked)
            {
                var result = await _playitApiClient.GetTunnelsAsync();
                if (result.Success)
                {
                    tunnelsList = result.Tunnels;
                }
                else
                {
                    errMsg = result.ErrorMessage ?? "Failed to fetch active tunnels.";
                }
            }

            if (_statusService != null)
            {
                _ = RefreshNetworkBannerAsync();
            }

            DispatchOnUi(() =>
            {
                IsLinked = isLinked;
                AgentId = agentId;
                AgentStatus = isLinked ? (isAgentRunning ? "Connected" : "Ready") : "Awaiting Setup";
                AgentDetailText = isLinked
                    ? (isAgentRunning
                        ? $"Agent {agentId} is online and routing traffic."
                        : "Partner connection saved. Start the agent to bring tunnels online.")
                    : "Click Setup Agent to link your Playit.gg account and claim a self-managed agent.";
                IsAgentRunning = isAgentRunning;
                ErrorMessage = errMsg;

                Tunnels.Clear();
                TunnelRows.Clear();
                foreach (var t in tunnelsList)
                {
                    Tunnels.Add(t);
                    TunnelRows.Add(new TunnelRowViewModel(
                        t,
                        SaveTunnelNameAsync,
                        SaveTunnelPortAsync,
                        ToggleTunnelRowAsync,
                        row => DeleteTunnelAsync(row.Id),
                        CopyTunnelAddressAsync));
                }

                HasTunnels = TunnelRows.Count > 0;
                EmptyTunnelsMessage = isLinked
                    ? "No tunnels found yet. Create a server tunnel above to see entries here."
                    : "Link Playit with the Setup Wizard to load and manage tunnels.";
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RefreshNetworkBannerAsync()
    {
        if (_statusService == null) return;
        try
        {
            var monitors = await _statusService.GetNetworkStatusAsync();
            DispatchOnUi(() =>
            {
                if (monitors.Count == 0)
                {
                    NetworkStatusBadgeText = "Status Unavailable";
                    NetworkStatusSummary = "Open network monitor to inspect Playit.gg datacenter status.";
                    NetworkStatusBg = "#269CA3AF";
                    NetworkStatusFg = "#9CA3AF";
                    return;
                }

                int issues = monitors.Count(m => !string.Equals(m.StatusClass, "success", StringComparison.OrdinalIgnoreCase));
                if (issues == 0)
                {
                    NetworkStatusBadgeText = "All Systems Operational";
                    NetworkStatusSummary = $"{monitors.Count} regional Playit.gg routing monitors online.";
                    NetworkStatusBg = "#2610B981";
                    NetworkStatusFg = "#10B981";
                }
                else
                {
                    NetworkStatusBadgeText = $"{issues} Node Issue(s)";
                    NetworkStatusSummary = $"{monitors.Count - issues}/{monitors.Count} regions operational.";
                    NetworkStatusBg = "#26F59E0B";
                    NetworkStatusFg = "#F59E0B";
                }
            });
        }
        catch
        {
        }
    }

    private async Task SaveTunnelNameAsync(TunnelRowViewModel row)
    {
        string newName = row.EditableName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(newName))
        {
            row.EditableName = row.Data.Name ?? string.Empty;
            return;
        }

        row.HasInlineError = false;
        var res = await _playitApiClient.RenameTunnelAsync(row.Id, newName);
        if (res.Success)
        {
            row.Data.Name = newName;
            row.NotifyDataUpdated();
        }
        else
        {
            row.InlineError = $"Rename failed: {res.ErrorMessage}";
            row.HasInlineError = true;
        }
    }

    private async Task SaveTunnelPortAsync(TunnelRowViewModel row)
    {
        if (!int.TryParse(row.EditablePort?.Trim(), out int newPort) || newPort < 1 || newPort > 65535)
        {
            row.InlineError = "Enter a valid TCP/UDP port between 1 and 65535.";
            row.HasInlineError = true;
            return;
        }

        row.HasInlineError = false;
        var res = await _playitApiClient.UpdateTunnelAsync(
            row.Id,
            row.Data.LocalIp ?? "127.0.0.1",
            newPort,
            row.Data.AgentId,
            row.IsEnabled);

        if (res.Success)
        {
            row.Data.Port = newPort;
            row.NotifyDataUpdated();
        }
        else
        {
            row.InlineError = $"Port update failed: {res.ErrorMessage}";
            row.HasInlineError = true;
        }
    }

    private async Task ToggleTunnelRowAsync(TunnelRowViewModel row)
    {
        row.HasInlineError = false;
        var res = await _playitApiClient.EnableTunnelAsync(row.Id, row.IsEnabled);
        if (res.Success)
        {
            row.Data.IsEnabled = row.IsEnabled;
        }
        else
        {
            row.IsEnabled = row.Data.IsEnabled;
            row.InlineError = $"Toggle failed: {res.ErrorMessage}";
            row.HasInlineError = true;
        }
    }

    private async Task CopyTunnelAddressAsync(TunnelRowViewModel row)
    {
        if (string.IsNullOrWhiteSpace(row.PublicAddress)) return;
        await AppClipboard.SetTextAsync(row.PublicAddress);
        row.IsCopied = true;
        _ = Task.Delay(1800).ContinueWith(_ => DispatchOnUi(() => row.IsCopied = false));
    }

    [RelayCommand]
    public async Task LinkAccountAsync()
    {
        var wizard = new PlayitSetupWizardWindow
        {
            DataContext = new PlayitSetupWizardViewModel()
        };

        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            await wizard.ShowDialog(desktop.MainWindow);
            await RefreshAllAsync();
        }
    }

    [RelayCommand]
    private async Task StartAgentAsync()
    {
        if (!IsLinked)
        {
            await LinkAccountAsync();
            if (!IsLinked) return;
        }

        await _tunnelManager.StartTunnelAsync("playit-https");
        await RefreshAllAsync();
    }

    [RelayCommand]
    private async Task StopAgentAsync()
    {
        await _tunnelManager.StopTunnelAsync("playit-https");
        await RefreshAllAsync();
    }

    [RelayCommand]
    private async Task CreateTunnelAsync()
    {
        if (!IsLinked)
        {
            await LinkAccountAsync();
            if (!IsLinked) return;
        }

        if (!IsAgentRunning)
        {
            await StartAgentAsync();
        }

        if (string.IsNullOrWhiteSpace(NewTunnelName))
        {
            ErrorMessage = "Tunnel Name is required.";
            return;
        }

        if (NewTunnelPort <= 0 || NewTunnelPort > 65535)
        {
            ErrorMessage = "Invalid local port.";
            return;
        }

        IsCreatingTunnel = true;
        ErrorMessage = string.Empty;

        try
        {
            var result = await _playitApiClient.CreateTunnelAsync(NewTunnelName, NewTunnelType, NewTunnelPort);
            if (result.Success)
            {
                NewTunnelName = string.Empty;
                await RefreshAllAsync();
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Failed to create tunnel.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsCreatingTunnel = false;
        }
    }

    [RelayCommand]
    private async Task DeleteTunnelAsync(string tunnelId)
    {
        if (string.IsNullOrEmpty(tunnelId)) return;

        var tunnel = Tunnels.FirstOrDefault(t => t.Id == tunnelId);
        string name = tunnel?.Name ?? tunnelId;

        bool confirmed = await ConfirmationDialogWindow.ShowAsync(
            "Delete Tunnel",
            $"Are you sure you want to delete Playit tunnel '{name}'?",
            "Delete Tunnel");

        if (!confirmed) return;

        var result = await _playitApiClient.DeleteTunnelAsync(tunnelId);
        if (result.Success)
        {
            await RefreshAllAsync();
        }
        else
        {
            ErrorMessage = result.ErrorMessage ?? "Failed to delete tunnel.";
        }
    }

    [RelayCommand]
    private async Task ToggleTunnelAsync(TunnelData tunnel)
    {
        if (tunnel == null) return;
        var result = await _playitApiClient.EnableTunnelAsync(tunnel.Id, !tunnel.IsEnabled);
        if (result.Success)
        {
            await RefreshAllAsync();
        }
        else
        {
            ErrorMessage = result.ErrorMessage ?? "Failed to toggle tunnel.";
        }
    }

    [RelayCommand]
    private async Task OpenSetupWizardAsync()
    {
        var wizard = new PlayitSetupWizardWindow
        {
            DataContext = new PlayitSetupWizardViewModel()
        };

        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow != null)
        {
            await wizard.ShowDialog(desktop.MainWindow);
            await RefreshAllAsync();
        }
    }

    [RelayCommand]
    private async Task OpenConsoleAsync()
    {
        if (_tunnelManager.GetProvider<PlayitHttpsTunnelProvider>("playit-https") is { } provider)
        {
            var consoleWin = new Views.Tunnel.PlayitConsoleWindow(provider.ProcessManager);
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                await consoleWin.ShowDialog(desktop.MainWindow);
            }
            else
            {
                consoleWin.Show();
            }
        }
    }

    [RelayCommand]
    private async Task OpenCreateTunnelDialogAsync()
    {
        var dialog = new Views.Tunnel.CreateTunnelDialog(_playitApiClient);
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            await dialog.ShowDialog(desktop.MainWindow);
            if (dialog.TunnelCreated)
            {
                await RefreshAllAsync();
            }
        }
    }

    [RelayCommand]
    private async Task UnlinkAccountAsync()
    {
        _settingsService.Settings.PlayitPartnerConnection = null;
        _settingsService.Save();
        await RefreshAllAsync();
    }

    [RelayCommand]
    private void ViewNetworkStatus()
    {
        var navService = _navigationService ?? App.Services?.GetService<IAppNavigationService>();
        var statusVm = App.Services?.GetService<PlayitStatusViewModel>();
        if (navService != null && statusVm != null)
        {
            navService.NavigateToDetailPage(
                statusVm,
                "Playit.gg Network Status",
                DetailRouteKind.PlayitNetworkStatus,
                DetailBackNavigation.PreviousDetail);
        }
    }

    [RelayCommand]
    private void OpenPortsMap()
    {
        var navService = _navigationService ?? App.Services?.GetService<IAppNavigationService>();
        var portsMapVM = App.Services?.GetService<PortsMapViewModel>();
        if (portsMapVM != null)
        {
            if (navService != null)
            {
                navService.NavigateToDetailPage(portsMapVM, "Ports Map", DetailRouteKind.PortsMap, DetailBackNavigation.PreviousDetail);
            }
            else
            {
                var mainVM = App.Services?.GetService<MainWindowViewModel>();
                if (mainVM != null)
                {
                    mainVM.CurrentViewModel = portsMapVM;
                }
            }
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        var navService = _navigationService ?? App.Services?.GetService<IAppNavigationService>();
        if (navService?.CanNavigateBack == true)
        {
            navService.NavigateBack();
        }
        else if (navService != null)
        {
            navService.NavigateToDashboard();
        }
        else
        {
            var mainVM = App.Services?.GetService<MainWindowViewModel>();
            if (mainVM != null)
            {
                mainVM.CurrentViewModel = App.Services?.GetRequiredService<DashboardViewModel>()!;
            }
        }
    }

    private static void DispatchOnUi(Action action)
    {
        if (Avalonia.Application.Current == null || Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }
}
