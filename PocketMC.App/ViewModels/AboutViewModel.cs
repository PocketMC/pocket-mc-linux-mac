using System;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PocketMC.App.ViewModels;

public partial class AboutViewModel : ObservableObject
{
    public string AppVersion => "Version 1.0.0 (Linux & macOS Native)";

    public IRelayCommand OpenGitHubCommand { get; }
    public IRelayCommand OpenDiscordCommand { get; }
    public IRelayCommand CopyDiscordInviteCommand { get; }
    public IRelayCommand OpenInstagramCommand { get; }
    public IRelayCommand OpenYouTubeCommand { get; }
    public IRelayCommand OpenRedditCommand { get; }
    public IRelayCommand OpenFeedbackCommand { get; }
    public IRelayCommand OpenDonationCommand { get; }
    public IRelayCommand NavigateBackCommand { get; }

    public AboutViewModel()
    {
        OpenGitHubCommand = new RelayCommand(() => OpenUrl("https://github.com/PocketMC/pocket-mc-linux-mac"));
        OpenDiscordCommand = new RelayCommand(() => OpenUrl("https://discord.gg/pocketmc"));
        CopyDiscordInviteCommand = new RelayCommand(CopyDiscordInvite);
        OpenInstagramCommand = new RelayCommand(() => OpenUrl("https://instagram.com/pocketmc"));
        OpenYouTubeCommand = new RelayCommand(() => OpenUrl("https://youtube.com/@pocketmc"));
        OpenRedditCommand = new RelayCommand(() => OpenUrl("https://reddit.com/r/pocketmc"));
        OpenFeedbackCommand = new RelayCommand(() => OpenUrl("https://github.com/PocketMC/pocket-mc-linux-mac/issues"));
        OpenDonationCommand = new RelayCommand(() => OpenUrl("https://buymeacoffee.com/pocketmc"));
        NavigateBackCommand = new RelayCommand(NavigateBack);
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
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(desktop.MainWindow);
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
