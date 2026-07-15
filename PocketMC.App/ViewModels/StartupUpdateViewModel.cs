using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.RemoteControl.Services;

namespace PocketMC.App.ViewModels;

public partial class StartupUpdateViewModel : ObservableObject
{
    private readonly UpdateService _updateService;
    private readonly UpdateInfo _updateInfo;

    [ObservableProperty]
    private string _version = "";

    [ObservableProperty]
    private string _releaseNotes = "";

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadProgress;

    public event Action? RequestClose;

    public StartupUpdateViewModel(UpdateInfo updateInfo)
    {
        _updateService = new UpdateService();
        _updateInfo = updateInfo;

        Version = updateInfo.Version;
        ReleaseNotes = updateInfo.ReleaseNotes;
    }

    [RelayCommand]
    private async Task UpdateAsync()
    {
        IsDownloading = true;
        await _updateService.DownloadAndApplyUpdateAsync(_updateInfo, progress => 
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => DownloadProgress = progress);
        });
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Skip()
    {
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void RemindLater()
    {
        RequestClose?.Invoke();
    }
}
