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
using PocketMC.Infrastructure.Configuration;
using PocketMC.Infrastructure.WhatsNew;
using PocketMC.App.Views;

namespace PocketMC.App.ViewModels;

public partial class AboutViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IInstanceService _instanceService;
    private readonly WhatsNewService? _whatsNewService;

    public string AppVersion => $"Version {AppConfig.AppVersion}";

    [ObservableProperty]
    private string _currentDataRoot = string.Empty;

    [ObservableProperty]
    private string _changeRootStatus = string.Empty;

    public IRelayCommand OpenGitHubCommand { get; }
    public IRelayCommand OpenWebsiteCommand { get; }
    public IRelayCommand OpenStudioCommand { get; }
    public IRelayCommand OpenWhatsNewCommand { get; }
    public IRelayCommand OpenDivyCommand { get; }
    public IRelayCommand OpenSahajCommand { get; }
    public IRelayCommand OpenJohndinglesinCommand { get; }
    public IRelayCommand OpenNexViceCommand { get; }
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

    public AboutViewModel(ISettingsService settingsService, IInstanceService instanceService, WhatsNewService? whatsNewService = null)
    {
        _settingsService = settingsService;
        _instanceService = instanceService;
        _whatsNewService = whatsNewService;
        _currentDataRoot = _settingsService.GetDataRoot();

        OpenGitHubCommand = new RelayCommand(() => OpenUrl(AppUrls.GitHub));
        OpenWebsiteCommand = new RelayCommand(() => OpenUrl(AppUrls.Website));
        OpenStudioCommand = new RelayCommand(() => OpenUrl(AppUrls.Studio));
        OpenWhatsNewCommand = new RelayCommand(OpenWhatsNew);
        OpenDivyCommand = new RelayCommand(() => OpenUrl(AppUrls.DivyGitHub));
        OpenSahajCommand = new RelayCommand(() => OpenUrl(AppUrls.SahajGitHub));
        OpenJohndinglesinCommand = new RelayCommand(() => OpenUrl(AppUrls.JohndinglesinGitHub));
        OpenNexViceCommand = new RelayCommand(() => OpenUrl(AppUrls.NexViceDiscord));
        OpenDiscordCommand = new RelayCommand(() => OpenUrl(AppUrls.DiscordInvite));
        CopyDiscordInviteCommand = new RelayCommand(CopyDiscordInvite);
        OpenInstagramCommand = new RelayCommand(() => OpenUrl(AppUrls.Instagram));
        OpenYouTubeCommand = new RelayCommand(() => OpenUrl(AppUrls.YouTube));
        OpenRedditCommand = new RelayCommand(() => OpenUrl(AppUrls.Reddit));
        OpenFeedbackCommand = new RelayCommand(() => OpenUrl(AppUrls.Feedback));
        OpenDonationCommand = new RelayCommand(() => OpenUrl(AppUrls.Donation));
        NavigateBackCommand = new RelayCommand(NavigateBack);
        ChangeDataRootCommand = new AsyncRelayCommand(ChangeDataRootAsync);
        OpenDataRootFolderCommand = new RelayCommand(OpenDataRootFolder);
    }

    private void OpenWhatsNew()
    {
        var whatsNewSvc = _whatsNewService ?? App.Services.GetService(typeof(WhatsNewService)) as WhatsNewService;
        if (whatsNewSvc != null && Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var win = new WhatsNewWindow(whatsNewSvc);
            win.ShowDialog(desktop.MainWindow);
        }
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
                await topLevel.Clipboard.SetTextAsync(AppUrls.DiscordInvite);
            }
        }
    }

    private void NavigateBack()
    {
        var navService = App.Services?.GetService(typeof(PocketMC.App.Navigation.IAppNavigationService)) as PocketMC.App.Navigation.IAppNavigationService;
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
            var mainVM = App.Services?.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
            var dashVM = App.Services?.GetService(typeof(DashboardViewModel)) as DashboardViewModel;
            if (mainVM != null && dashVM != null)
            {
                mainVM.CurrentViewModel = dashVM;
            }
        }
    }
}
