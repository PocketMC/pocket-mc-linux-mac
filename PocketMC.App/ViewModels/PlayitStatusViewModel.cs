using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PocketMC.App.Navigation;
using PocketMC.Core.Models;
using PocketMC.Infrastructure.Services;

namespace PocketMC.App.ViewModels;

public sealed class PlayitStatusMonitorViewModel
{
    public string Name { get; }
    public string StatusClass { get; }
    public string StatusText { get; }
    public string IconColorHex { get; }

    public PlayitStatusMonitorViewModel(PlayitStatusMonitor monitor)
    {
        Name = monitor.Name ?? "Unknown Node";
        StatusClass = monitor.StatusClass ?? "unknown";

        switch (StatusClass.ToLowerInvariant())
        {
            case "success":
                StatusText = monitor.StatusText ?? "Operational";
                IconColorHex = "#10B981";
                break;
            case "warning":
                StatusText = monitor.StatusText ?? "Degraded";
                IconColorHex = "#F59E0B";
                break;
            case "danger":
                StatusText = monitor.StatusText ?? "Outage";
                IconColorHex = "#EF4444";
                break;
            default:
                StatusText = monitor.StatusText ?? "Unknown";
                IconColorHex = "#9CA3AF";
                break;
        }
    }
}

public partial class PlayitStatusViewModel : ObservableObject
{
    private readonly PlayitStatusService _statusService;
    private readonly IAppNavigationService? _navigationService;

    public ObservableCollection<PlayitStatusMonitorViewModel> Monitors { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _lastUpdated = string.Empty;

    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand GoBackCommand { get; }

    public PlayitStatusViewModel(PlayitStatusService statusService, IAppNavigationService? navigationService = null)
    {
        _statusService = statusService;
        _navigationService = navigationService ?? App.Services?.GetService<IAppNavigationService>();

        RefreshCommand = new AsyncRelayCommand(LoadStatusAsync);
        GoBackCommand = new RelayCommand(GoBack);
    }

    public async Task LoadStatusAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            var data = await _statusService.GetNetworkStatusAsync();
            DispatchOnUi(() =>
            {
                Monitors.Clear();
                foreach (var monitor in data)
                {
                    Monitors.Add(new PlayitStatusMonitorViewModel(monitor));
                }

                LastUpdated = $"Last updated: {DateTime.Now:t}";
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void GoBack()
    {
        if (_navigationService?.CanNavigateBack == true)
        {
            _navigationService.NavigateBack();
        }
        else
        {
            _navigationService?.NavigateToTunnel();
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
