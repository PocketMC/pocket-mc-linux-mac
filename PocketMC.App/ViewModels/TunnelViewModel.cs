using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.RemoteControl.Tunnels;
using PocketMC.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace PocketMC.App.ViewModels;

public partial class TunnelViewModel : ObservableObject
{
    private readonly PlayitApiClient _playitApiClient;
    private readonly RemoteTunnelManager _tunnelManager;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private string _agentStatus = "Checking...";

    [ObservableProperty]
    private bool _isAgentRunning;

    [ObservableProperty]
    private bool _isLinked;

    [ObservableProperty]
    private string _agentId = "Not Configured";

    [ObservableProperty]
    private ObservableCollection<TunnelData> _tunnels = new();

    [ObservableProperty]
    private string _newTunnelName = "";

    [ObservableProperty]
    private string _newTunnelType = "minecraft-java"; // default

    [ObservableProperty]
    private int _newTunnelPort = 25565;

    [ObservableProperty]
    private bool _isCreatingTunnel;

    [ObservableProperty]
    private string _errorMessage = "";

    public List<string> TunnelTypes { get; } = new()
    {
        "minecraft-java",
        "minecraft-bedrock",
        "mc-simple-voice-chat"
    };

    public TunnelViewModel(PlayitApiClient playitApiClient, RemoteTunnelManager tunnelManager, ISettingsService settingsService)
    {
        _playitApiClient = playitApiClient;
        _tunnelManager = tunnelManager;
        _settingsService = settingsService;

        _ = RefreshAllAsync();
    }

    [RelayCommand]
    public async Task RefreshAllAsync()
    {
        ErrorMessage = "";
        var isLinked = _playitApiClient.HasPartnerConnection();
        var agentId = _playitApiClient.GetAgentId() ?? "Not Configured";

        var status = await _tunnelManager.GetTunnelStatusAsync("playit-https");
        var isAgentRunning = (status == "Running");

        var tunnelsList = new List<TunnelData>();
        string errMsg = "";

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

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            IsLinked = isLinked;
            AgentId = agentId;
            AgentStatus = status;
            IsAgentRunning = isAgentRunning;
            ErrorMessage = errMsg;

            Tunnels.Clear();
            foreach (var t in tunnelsList)
            {
                Tunnels.Add(t);
            }
        });
    }

    [RelayCommand]
    private async Task StartAgentAsync()
    {
        if (!IsLinked)
        {
            ErrorMessage = "Please link your Playit account first.";
            return;
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
        ErrorMessage = "";

        try
        {
            var result = await _playitApiClient.CreateTunnelAsync(NewTunnelName, NewTunnelType, NewTunnelPort);
            if (result.Success)
            {
                NewTunnelName = "";
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
        var wizard = new PocketMC.App.Views.PlayitSetupWizardWindow
        {
            DataContext = new PlayitSetupWizardViewModel()
        };

        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow != null)
            {
                await wizard.ShowDialog(desktop.MainWindow);
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
    private void GoBack()
    {
        var mainVM = App.Services.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
        if (mainVM != null)
        {
            mainVM.CurrentViewModel = App.Services.GetRequiredService<DashboardViewModel>();
        }
    }
}
