using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.Core.Services;

namespace PocketMC.App.ViewModels;

public partial class AboutViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IInstanceService _instanceService;

    public string AppVersion => "Version 1.0.2 (Linux & macOS Native)";

    [ObservableProperty]
    private string _currentDataRoot = string.Empty;

    [ObservableProperty]
    private string _changeRootStatus = string.Empty;

    public IRelayCommand OpenGitHubCommand { get; }
    public IRelayCommand OpenDiscordCommand { get; }
    public IRelayCommand CopyDiscordInviteCommand { get; }
    public IRelayCommand OpenInstagramCommand { get; }
    public IRelayCommand OpenYouTubeCommand { get; }
    public IRelayCommand OpenRedditCommand { get; }
    public IRelayCommand OpenFeedbackCommand { get; }
    public IRelayCommand OpenDonationCommand { get; }
    public IRelayCommand NavigateBackCommand { get; }
    public IAsyncRelayCommand ChangeDataRootCommand { get; }
    public IRelayCommand OpenDataRootFolderCommand { get; }

    public AboutViewModel(ISettingsService settingsService, IInstanceService instanceService)
    {
        _settingsService = settingsService;
        _instanceService = instanceService;
        _currentDataRoot = _settingsService.GetDataRoot();

        OpenGitHubCommand = new RelayCommand(() => OpenUrl("https://github.com/PocketMC/pocket-mc-linux-mac"));
        OpenDiscordCommand = new RelayCommand(() => OpenUrl("https://discord.gg/pocketmc"));
        CopyDiscordInviteCommand = new RelayCommand(CopyDiscordInvite);
        OpenInstagramCommand = new RelayCommand(() => OpenUrl("https://instagram.com/pocketmc"));
        OpenYouTubeCommand = new RelayCommand(() => OpenUrl("https://youtube.com/@pocketmc"));
        OpenRedditCommand = new RelayCommand(() => OpenUrl("https://reddit.com/r/pocketmc"));
        OpenFeedbackCommand = new RelayCommand(() => OpenUrl("https://github.com/PocketMC/pocket-mc-linux-mac/issues"));
        OpenDonationCommand = new RelayCommand(() => OpenUrl("https://buymeacoffee.com/pocketmc"));
        NavigateBackCommand = new RelayCommand(NavigateBack);
        ChangeDataRootCommand = new AsyncRelayCommand(ChangeDataRootAsync);
        OpenDataRootFolderCommand = new RelayCommand(OpenDataRootFolder);
    }

    private async Task ChangeDataRootAsync()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
            if (topLevel != null)
            {
                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select New Storage Root Directory",
                    AllowMultiple = false
                });

                if (folders != null && folders.Count > 0)
                {
                    string newPath = folders[0].Path.LocalPath;
                    try
                    {
                        Directory.CreateDirectory(newPath);
                        _settingsService.SetDataRoot(newPath);
                        CurrentDataRoot = newPath;
                        ChangeRootStatus = "Storage root location updated successfully.";

                        // Refresh instances list in dashboard if loaded
                        var dashVM = App.Services.GetService(typeof(DashboardViewModel)) as DashboardViewModel;
                        if (dashVM != null)
                        {
                            await dashVM.LoadInstancesAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        ChangeRootStatus = $"Failed to change directory: {ex.Message}";
                    }
                }
            }
        }
    }

    private void OpenDataRootFolder()
    {
        try
        {
            var path = _settingsService.GetDataRoot();
            if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
        }
        catch { }
    }

    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { }
    }

    private async void CopyDiscordInvite()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync("https://discord.gg/pocketmc");
            }
        }
    }

    private void NavigateBack()
    {
        var mainVM = App.Services.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
        if (mainVM != null)
        {
            var dashVM = App.Services.GetService(typeof(DashboardViewModel)) as DashboardViewModel;
            if (dashVM != null)
            {
                mainVM.CurrentViewModel = dashVM;
            }
        }
    }
}
