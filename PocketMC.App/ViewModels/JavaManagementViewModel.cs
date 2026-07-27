using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.App.Views;
using PocketMC.Core.Services;

namespace PocketMC.App.ViewModels;

public partial class JavaRuntimeItemViewModel : ObservableObject
{
    public string Version { get; }
    public string VersionLabel { get; }
    public string DisplayName { get; }
    public string SupportedMinecraftVersions { get; }

    [ObservableProperty]
    private bool _isInstalled;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _detailText = string.Empty;

    [ObservableProperty]
    private string _statusText = "Not Installed";

    public JavaRuntimeItemViewModel(string version, string displayName, string supportedMc)
    {
        Version = version;
        VersionLabel = version;
        DisplayName = displayName;
        SupportedMinecraftVersions = supportedMc;
    }
}

public partial class JavaManagementViewModel : ObservableObject
{
    private readonly IJavaService _javaService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private ObservableCollection<JavaRuntimeItemViewModel> _runtimes = new();

    [ObservableProperty]
    private string _globalStatus = string.Empty;

    public IAsyncRelayCommand DownloadMissingCommand { get; }
    public IAsyncRelayCommand AddCustomRuntimeCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand<JavaRuntimeItemViewModel> DownloadRuntimeCommand { get; }
    public IAsyncRelayCommand<JavaRuntimeItemViewModel> DeleteRuntimeCommand { get; }
    public IRelayCommand NavigateBackCommand { get; }

    public JavaManagementViewModel(IJavaService javaService, ISettingsService settingsService)
    {
        _javaService = javaService;
        _settingsService = settingsService;

        DownloadMissingCommand = new AsyncRelayCommand(DownloadMissingAsync);
        AddCustomRuntimeCommand = new AsyncRelayCommand(AddCustomRuntimeAsync);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        DownloadRuntimeCommand = new AsyncRelayCommand<JavaRuntimeItemViewModel>(DownloadRuntimeAsync);
        DeleteRuntimeCommand = new AsyncRelayCommand<JavaRuntimeItemViewModel>(DeleteRuntimeAsync);
        NavigateBackCommand = new RelayCommand(NavigateBack);

        InitializeItems();
        _ = RefreshAsync();
    }

    private void InitializeItems()
    {
        Runtimes.Clear();
        Runtimes.Add(new JavaRuntimeItemViewModel("8", "Java 8 (JRE 8)", "Minecraft 1.8 - 1.16.5"));
        Runtimes.Add(new JavaRuntimeItemViewModel("11", "Java 11 (JDK 11)", "Minecraft 1.12 - 1.16.5"));
        Runtimes.Add(new JavaRuntimeItemViewModel("17", "Java 17 (JDK 17)", "Minecraft 1.17 - 1.20.4"));
        Runtimes.Add(new JavaRuntimeItemViewModel("21", "Java 21 (JDK 21)", "Minecraft 1.20.5 - 1.21.4"));
        Runtimes.Add(new JavaRuntimeItemViewModel("25", "Java 25 (JDK 25)", "Minecraft 1.22+ & Future Versions"));
    }

    public async Task RefreshAsync()
    {
        try
        {
            GlobalStatus = "Scanning Java runtimes...";
            var settingsRuntimes = _settingsService.Settings.DownloadedRuntimes;
            javaDict = settingsRuntimes.TryGetValue("java", out var dict) ? dict : null;

            foreach (var item in Runtimes)
            {
                try
                {
                    bool isInst = await _javaService.IsJavaRuntimeInstalledAsync(item.Version);
                    Dispatcher.UIThread.Post(() =>
                    {
                        item.IsInstalled = isInst;
                        if (isInst)
                        {
                            string path = (javaDict != null && javaDict.TryGetValue(item.Version, out var p)) ? p : "System / Auto-provisioned";
                            item.DetailText = $"Installed at: {path} • Supports {item.SupportedMinecraftVersions}";
                            item.StatusText = "Installed";
                        }
                        else
                        {
                            item.DetailText = $"Supports {item.SupportedMinecraftVersions}. Not installed locally.";
                            item.StatusText = "Not Installed";
                        }
                    });
                }
                catch { }
            }
            Dispatcher.UIThread.Post(() => GlobalStatus = "Java runtimes up to date.");
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() => GlobalStatus = $"Error scanning runtimes: {ex.Message}");
        }
    }
    private System.Collections.Generic.Dictionary<string, string>? javaDict;

    private async Task DownloadMissingAsync()
    {
        foreach (var item in Runtimes.Where(r => !r.IsInstalled))
        {
            await DownloadRuntimeAsync(item);
        }
    }

    private async Task DownloadRuntimeAsync(JavaRuntimeItemViewModel? item)
    {
        if (item == null || item.IsDownloading) return;

        item.IsDownloading = true;
        item.StatusText = "Downloading...";
        GlobalStatus = $"Downloading {item.DisplayName}...";

        try
        {
            var progress = new Progress<double>(p =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    item.Progress = p * 100.0;
                    item.StatusText = $"Downloading: {p * 100.0:F0}%";
                });
            });

            await _javaService.ProvisionJavaRuntimeAsync(item.Version, progress);
            item.IsInstalled = true;
            item.StatusText = "Installed";
            item.DetailText = $"Installed & verified. Supports {item.SupportedMinecraftVersions}";
            GlobalStatus = $"{item.DisplayName} downloaded successfully!";
        }
        catch (Exception ex)
        {
            item.StatusText = "Failed";
            item.DetailText = $"Error downloading {item.DisplayName}: {ex.Message}";
            GlobalStatus = $"Failed to download {item.DisplayName}: {ex.Message}";
        }
        finally
        {
            item.IsDownloading = false;
        }
    }

    private async Task DeleteRuntimeAsync(JavaRuntimeItemViewModel? item)
    {
        if (item == null || !item.IsInstalled) return;

        bool confirm = await ConfirmationDialogWindow.ShowAsync(
            $"Delete {item.DisplayName}?",
            $"Are you sure you want to delete {item.DisplayName}? Servers using this Java version may fail to start until re-downloaded.",
            "Delete Runtime");

        if (!confirm) return;

        try
        {
            await _javaService.DeleteJavaRuntimeAsync(item.Version);
            item.IsInstalled = false;
            item.StatusText = "Not Installed";
            item.DetailText = $"Supports {item.SupportedMinecraftVersions}. Not installed locally.";
            GlobalStatus = $"{item.DisplayName} deleted.";
        }
        catch (Exception ex)
        {
            GlobalStatus = $"Failed to delete {item.DisplayName}: {ex.Message}";
        }
    }

    private async Task AddCustomRuntimeAsync()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(desktop.MainWindow);
            if (topLevel != null)
            {
                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
                {
                    Title = "Select Custom JDK / JRE Installation Directory",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    string path = folders[0].Path.LocalPath;
                    try
                    {
                        await _javaService.RegisterCustomJavaRuntimeAsync("21", path);
                        await RefreshAsync();
                        GlobalStatus = $"Registered custom Java runtime at {path}";
                    }
                    catch (Exception ex)
                    {
                        GlobalStatus = $"Failed to register custom runtime: {ex.Message}";
                    }
                }
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
