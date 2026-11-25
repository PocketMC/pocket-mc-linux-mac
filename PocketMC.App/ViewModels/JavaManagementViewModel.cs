using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.App.Navigation;
using PocketMC.App.Views;
using PocketMC.Core.Services;

namespace PocketMC.App.ViewModels;

public partial class JavaRuntimeItemViewModel : ObservableObject
{
    public string Version { get; }
    public string VersionLabel => IsCustom && string.IsNullOrWhiteSpace(Version) ? "?" : Version;
    public string DisplayName { get; }
    public string SupportedMinecraftVersions { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BadgeText))]
    [NotifyPropertyChangedFor(nameof(BadgeBackground))]
    [NotifyPropertyChangedFor(nameof(BadgeForeground))]
    [NotifyPropertyChangedFor(nameof(StatusBackground))]
    [NotifyPropertyChangedFor(nameof(ShowDownloadButton))]
    [NotifyPropertyChangedFor(nameof(ShowDeleteButton))]
    [NotifyPropertyChangedFor(nameof(ShowVerifyButton))]
    private bool _isInstalled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BadgeText))]
    [NotifyPropertyChangedFor(nameof(BadgeBackground))]
    [NotifyPropertyChangedFor(nameof(BadgeForeground))]
    [NotifyPropertyChangedFor(nameof(StatusBackground))]
    [NotifyPropertyChangedFor(nameof(ShowDownloadButton))]
    [NotifyPropertyChangedFor(nameof(ShowDeleteButton))]
    [NotifyPropertyChangedFor(nameof(ShowVerifyButton))]
    private bool _isDownloading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VersionLabel))]
    [NotifyPropertyChangedFor(nameof(BadgeText))]
    [NotifyPropertyChangedFor(nameof(BadgeBackground))]
    [NotifyPropertyChangedFor(nameof(BadgeForeground))]
    [NotifyPropertyChangedFor(nameof(ShowDownloadButton))]
    private bool _isCustom;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BadgeText))]
    [NotifyPropertyChangedFor(nameof(BadgeBackground))]
    [NotifyPropertyChangedFor(nameof(BadgeForeground))]
    [NotifyPropertyChangedFor(nameof(StatusBackground))]
    private bool _hasError;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string? _runtimePath;

    [ObservableProperty]
    private string _detailText = string.Empty;

    [ObservableProperty]
    private string _statusText = "Not Installed";

    public string BadgeText => IsCustom
        ? "CUSTOM"
        : HasError
            ? "ERROR"
            : IsDownloading
                ? "DOWNLOADING"
                : IsInstalled
                    ? "READY"
                    : "MISSING";

    public string BadgeBackground => IsCustom
        ? "#30A08CFF"
        : HasError
            ? "#30FF6666"
            : IsDownloading
                ? "#3066CCFF"
                : IsInstalled
                    ? "#3060CDFF"
                    : "#30FF9966";

    public string BadgeForeground => IsCustom
        ? "#C0B4FF"
        : HasError
            ? "#FF9C9C"
            : IsDownloading
                ? "#8BD0FF"
                : IsInstalled
                    ? "#78B8FF"
                    : "#FFBB88";

    public string StatusBackground => HasError
        ? "#25FF6666"
        : IsDownloading
            ? "#2566CCFF"
            : IsInstalled
                ? "#2560CDFF"
                : "#18FFFFFF";

    public bool ShowDownloadButton => !IsInstalled && !IsCustom && !IsDownloading;
    public bool ShowDeleteButton => IsInstalled && !IsDownloading;
    public bool ShowVerifyButton => IsInstalled && !IsDownloading;

    public JavaRuntimeItemViewModel(string version, string displayName, string supportedMc)
    {
        Version = version;
        DisplayName = displayName;
        SupportedMinecraftVersions = supportedMc;
    }
}

public partial class PhpRuntimeItemViewModel : ObservableObject
{
    public string Version { get; }
    public string VersionLabel => Version;
    public string DisplayName { get; }
    public string TargetPocketMineVersion { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BadgeText))]
    [NotifyPropertyChangedFor(nameof(BadgeBackground))]
    [NotifyPropertyChangedFor(nameof(BadgeForeground))]
    [NotifyPropertyChangedFor(nameof(StatusBackground))]
    [NotifyPropertyChangedFor(nameof(ShowDownloadButton))]
    [NotifyPropertyChangedFor(nameof(ShowDeleteButton))]
    private bool _isInstalled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BadgeText))]
    [NotifyPropertyChangedFor(nameof(BadgeBackground))]
    [NotifyPropertyChangedFor(nameof(BadgeForeground))]
    [NotifyPropertyChangedFor(nameof(StatusBackground))]
    [NotifyPropertyChangedFor(nameof(ShowDownloadButton))]
    [NotifyPropertyChangedFor(nameof(ShowDeleteButton))]
    private bool _isDownloading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BadgeText))]
    [NotifyPropertyChangedFor(nameof(BadgeBackground))]
    [NotifyPropertyChangedFor(nameof(BadgeForeground))]
    [NotifyPropertyChangedFor(nameof(StatusBackground))]
    private bool _hasError;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string? _runtimePath;

    [ObservableProperty]
    private string _detailText = string.Empty;

    [ObservableProperty]
    private string _statusText = "Not Installed";

    public string BadgeText => HasError
        ? "ERROR"
        : IsDownloading
            ? "DOWNLOADING"
            : IsInstalled
                ? "READY"
                : "MISSING";

    public string BadgeBackground => HasError
        ? "#30FF6666"
        : IsDownloading
            ? "#3066CCFF"
            : IsInstalled
                ? "#30A08CFF"
                : "#30FF9966";

    public string BadgeForeground => HasError
        ? "#FF9C9C"
        : IsDownloading
            ? "#8BD0FF"
            : IsInstalled
                ? "#C0B4FF"
                : "#FFBB88";

    public string StatusBackground => HasError
        ? "#25FF6666"
        : IsDownloading
            ? "#2566CCFF"
            : IsInstalled
                ? "#25A08CFF"
                : "#18FFFFFF";

    public bool ShowDownloadButton => !IsInstalled && !IsDownloading;
    public bool ShowDeleteButton => IsInstalled && !IsDownloading;

    public PhpRuntimeItemViewModel(string version, string displayName, string targetPocketMineVersion)
    {
        Version = version;
        DisplayName = displayName;
        TargetPocketMineVersion = targetPocketMineVersion;
    }
}

public partial class JavaManagementViewModel : ObservableObject
{
    private static readonly (string Version, string DisplayName, string McRange)[] StandardJavaRuntimes =
    {
        ("8", "Java 8 Runtime (MC 1.0 - 1.16.4)", "MC 1.0 - 1.16.4"),
        ("11", "Java 11 Runtime (MC 1.16.5 - 1.17.1)", "MC 1.16.5 - 1.17.1"),
        ("17", "Java 17 Runtime (MC 1.18 - 1.20.4)", "MC 1.18 - 1.20.4"),
        ("21", "Java 21 Runtime (MC 1.20.5 - 1.21.1)", "MC 1.20.5 - 1.21.1"),
        ("25", "Java 25 Runtime (MC 1.21.2+)", "MC 1.21.2+")
    };

    private static readonly (string Version, string DisplayName, string TargetPm)[] StandardPhpRuntimes =
    {
        ("8.2", "PHP 8.2 (PocketMine-MP 5.x - Recommended)", "PocketMine-MP 5.x"),
        ("8.3", "PHP 8.3 (PocketMine-MP 5.x / 6.x)", "PocketMine-MP 5.x / 6.x"),
        ("8.0", "PHP 8.0 (PocketMine-MP 4.x Legacy)", "PocketMine-MP 4.x"),
        ("8.1", "PHP 8.1 (PocketMine-MP 4.x Legacy)", "PocketMine-MP 4.x")
    };

    private readonly IJavaService _javaService;
    private readonly ISettingsService _settingsService;
    private readonly IPHPService? _phpService;
    private readonly IDialogService? _dialogService;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    [ObservableProperty]
    private ObservableCollection<JavaRuntimeItemViewModel> _runtimes = new();

    [ObservableProperty]
    private ObservableCollection<PhpRuntimeItemViewModel> _phpRuntimes = new();

    [ObservableProperty]
    private string _globalStatus = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownloadMissing))]
    private bool _isBusy;

    public bool CanDownloadMissing => !IsBusy;

    public IAsyncRelayCommand DownloadMissingCommand { get; }
    public IAsyncRelayCommand AddCustomRuntimeCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand<JavaRuntimeItemViewModel> DownloadRuntimeCommand { get; }
    public IAsyncRelayCommand<JavaRuntimeItemViewModel> DeleteRuntimeCommand { get; }
    public IAsyncRelayCommand<JavaRuntimeItemViewModel> VerifyRuntimeCommand { get; }
    public IAsyncRelayCommand<PhpRuntimeItemViewModel> DownloadPhpRuntimeCommand { get; }
    public IAsyncRelayCommand<PhpRuntimeItemViewModel> DeletePhpRuntimeCommand { get; }
    public IRelayCommand NavigateBackCommand { get; }

    public JavaManagementViewModel(
        IJavaService javaService,
        ISettingsService settingsService,
        IPHPService? phpService = null,
        IDialogService? dialogService = null)
    {
        _javaService = javaService;
        _settingsService = settingsService;
        _phpService = phpService;
        _dialogService = dialogService;

        DownloadMissingCommand = new AsyncRelayCommand(DownloadMissingAsync);
        AddCustomRuntimeCommand = new AsyncRelayCommand(AddCustomRuntimeAsync);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        DownloadRuntimeCommand = new AsyncRelayCommand<JavaRuntimeItemViewModel>(DownloadRuntimeAsync);
        DeleteRuntimeCommand = new AsyncRelayCommand<JavaRuntimeItemViewModel>(DeleteRuntimeAsync);
        VerifyRuntimeCommand = new AsyncRelayCommand<JavaRuntimeItemViewModel>(VerifyRuntimeAsync);
        DownloadPhpRuntimeCommand = new AsyncRelayCommand<PhpRuntimeItemViewModel>(DownloadPhpRuntimeAsync);
        DeletePhpRuntimeCommand = new AsyncRelayCommand<PhpRuntimeItemViewModel>(DeletePhpRuntimeAsync);
        NavigateBackCommand = new RelayCommand(NavigateBack);

        InitializeItems();
        UpdateSummaryStatus();
    }

    private void InitializeItems()
    {
        Runtimes.Clear();
        foreach (var (version, displayName, mcRange) in StandardJavaRuntimes)
        {
            Runtimes.Add(new JavaRuntimeItemViewModel(version, displayName, mcRange));
        }

        PhpRuntimes.Clear();
        foreach (var (version, displayName, targetPm) in StandardPhpRuntimes)
        {
            PhpRuntimes.Add(new PhpRuntimeItemViewModel(version, displayName, targetPm));
        }
    }

    public async Task RefreshAsync()
    {
        await _refreshLock.WaitAsync();
        try
        {
            GlobalStatus = "Scanning Java & PHP runtimes...";
            var settingsRuntimes = _settingsService.Settings.DownloadedRuntimes;
            var javaDict = settingsRuntimes.TryGetValue("java", out var dict) ? dict : null;

            var javaList = new List<JavaRuntimeItemViewModel>();
            foreach (var (version, displayName, mcRange) in StandardJavaRuntimes)
            {
                var item = new JavaRuntimeItemViewModel(version, displayName, mcRange);
                try
                {
                    bool isInst = await _javaService.IsJavaRuntimeInstalledAsync(version);
                    string? path = _javaService.GetJavaRuntimeDirectory(version)
                        ?? (javaDict != null && javaDict.TryGetValue(version, out var configured) ? configured : null);

                    item.IsInstalled = isInst;
                    item.RuntimePath = path;
                    if (isInst)
                    {
                        item.StatusText = "Installed";
                        item.DetailText = FormatRuntimeDetail(path, "System / Auto-provisioned");
                    }
                    else
                    {
                        item.StatusText = "Not Installed";
                        item.DetailText = "Missing runtime. PocketMC will download it automatically.";
                    }
                }
                catch (Exception ex)
                {
                    item.HasError = true;
                    item.StatusText = "Error";
                    item.DetailText = ex.Message;
                }

                javaList.Add(item);
            }

            // Discover custom Java runtimes inside Runtimes folder
            string runtimesRoot = Path.Combine(_settingsService.GetSettingsDirectory(), "Runtimes");
            if (Directory.Exists(runtimesRoot))
            {
                var standardFolders = new HashSet<string>(
                    StandardJavaRuntimes.SelectMany(r => new[] { $"java-{r.Version}", $"java{r.Version}" }),
                    StringComparer.OrdinalIgnoreCase);

                foreach (string dir in Directory.GetDirectories(runtimesRoot))
                {
                    string folderName = Path.GetFileName(dir);
                    if (standardFolders.Contains(folderName) ||
                        folderName.StartsWith("php", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string? execPath = FindJavaExecutableInFolder(dir);
                    bool exists = execPath != null && File.Exists(execPath);
                    var customItem = new JavaRuntimeItemViewModel("", folderName, "Custom JDK/JRE")
                    {
                        IsCustom = true,
                        IsInstalled = exists,
                        RuntimePath = dir,
                        StatusText = exists ? "Custom" : "Missing Binary",
                        DetailText = exists
                            ? FormatRuntimeDetail(dir, dir)
                            : $"{dir} - java executable not found"
                    };
                    javaList.Add(customItem);
                }
            }

            var phpList = new List<PhpRuntimeItemViewModel>();
            foreach (var (version, displayName, targetPm) in StandardPhpRuntimes)
            {
                var item = new PhpRuntimeItemViewModel(version, displayName, targetPm);
                try
                {
                    bool isInst = _phpService != null && await _phpService.IsPHPRuntimeInstalledAsync(version);
                    string? path = _phpService?.GetPHPRuntimeDirectory(version);
                    item.IsInstalled = isInst;
                    item.RuntimePath = path;
                    if (isInst)
                    {
                        item.StatusText = "Installed";
                        item.DetailText = FormatRuntimeDetail(path, targetPm);
                    }
                    else
                    {
                        item.StatusText = "Not Installed";
                        item.DetailText = $"Target: {targetPm}. Missing runtime.";
                    }
                }
                catch (Exception ex)
                {
                    item.HasError = true;
                    item.StatusText = "Error";
                    item.DetailText = ex.Message;
                }

                phpList.Add(item);
            }

            DispatchOnUi(() =>
            {
                Runtimes.Clear();
                foreach (var item in javaList)
                {
                    Runtimes.Add(item);
                }

                PhpRuntimes.Clear();
                foreach (var item in phpList)
                {
                    PhpRuntimes.Add(item);
                }

                UpdateSummaryStatus();
            });
        }
        catch (Exception ex)
        {
            DispatchOnUi(() => GlobalStatus = $"Error scanning runtimes: {ex.Message}");
        }
        finally
        {
            _refreshLock.Release();
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

    private void UpdateSummaryStatus()
    {
        var activeJava = Runtimes.Where(r => r.IsDownloading).ToList();
        var activePhp = PhpRuntimes.Where(p => p.IsDownloading).ToList();
        if (activeJava.Count > 0 || activePhp.Count > 0)
        {
            string active = activeJava.Count > 0 ? activeJava[0].DisplayName : activePhp[0].DisplayName;
            int remaining = activeJava.Count + activePhp.Count - 1;
            GlobalStatus = remaining > 0 ? $"Provisioning {active} (+{remaining} more)..." : $"Provisioning {active}...";
            return;
        }

        int installedJava = Runtimes.Count(r => !r.IsCustom && r.IsInstalled);
        int totalJava = Runtimes.Count(r => !r.IsCustom);
        int installedPhp = PhpRuntimes.Count(p => p.IsInstalled);
        int totalPhp = PhpRuntimes.Count;

        GlobalStatus = $"{installedJava}/{totalJava} Java runtimes, {installedPhp}/{totalPhp} PHP runtimes installed";
    }

    private async Task DownloadMissingAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            var missingJava = Runtimes.Where(r => !r.IsCustom && !r.IsInstalled).ToList();
            var missingPhp = PhpRuntimes.Where(p => !p.IsInstalled).ToList();

            foreach (var item in missingJava)
            {
                await DownloadRuntimeInternalAsync(item);
            }

            if (_phpService != null)
            {
                foreach (var item in missingPhp)
                {
                    await DownloadPhpRuntimeInternalAsync(item);
                }
            }
        }
        finally
        {
            IsBusy = false;
            UpdateSummaryStatus();
        }
    }

    private async Task DownloadRuntimeAsync(JavaRuntimeItemViewModel? item)
    {
        if (item == null || item.IsDownloading) return;
        await DownloadRuntimeInternalAsync(item);
        UpdateSummaryStatus();
    }

    private async Task DownloadRuntimeInternalAsync(JavaRuntimeItemViewModel item)
    {
        if (item.IsDownloading || string.IsNullOrWhiteSpace(item.Version)) return;

        item.HasError = false;
        item.IsDownloading = true;
        item.Progress = 0;
        item.StatusText = "Downloading...";
        UpdateSummaryStatus();

        try
        {
            var progress = new Progress<double>(p =>
            {
                DispatchOnUi(() =>
                {
                    item.Progress = p * 100.0;
                    item.StatusText = $"Downloading: {p * 100.0:F0}%";
                });
            });

            await _javaService.ProvisionJavaRuntimeAsync(item.Version, progress);
            string? path = _javaService.GetJavaRuntimeDirectory(item.Version);
            item.IsInstalled = true;
            item.RuntimePath = path;
            item.StatusText = "Installed";
            item.DetailText = FormatRuntimeDetail(path, $"Installed & verified. Supports {item.SupportedMinecraftVersions}");
        }
        catch (Exception ex)
        {
            item.HasError = true;
            item.StatusText = "Failed";
            item.DetailText = $"Error downloading {item.DisplayName}: {ex.Message}";
            GlobalStatus = $"Failed to download {item.DisplayName}: {ex.Message}";
        }
        finally
        {
            item.IsDownloading = false;
        }
    }

    private async Task DownloadPhpRuntimeAsync(PhpRuntimeItemViewModel? item)
    {
        if (item == null || item.IsDownloading) return;
        await DownloadPhpRuntimeInternalAsync(item);
        UpdateSummaryStatus();
    }

    private async Task DownloadPhpRuntimeInternalAsync(PhpRuntimeItemViewModel item)
    {
        if (_phpService == null || item.IsDownloading) return;

        item.HasError = false;
        item.IsDownloading = true;
        item.Progress = 0;
        item.StatusText = "Downloading...";
        UpdateSummaryStatus();

        try
        {
            var progress = new Progress<double>(p =>
            {
                DispatchOnUi(() =>
                {
                    item.Progress = p * 100.0;
                    item.StatusText = $"Downloading: {p * 100.0:F0}%";
                });
            });

            await _phpService.ProvisionPHPRuntimeAsync(item.Version, progress);
            string? path = _phpService.GetPHPRuntimeDirectory(item.Version);
            item.IsInstalled = true;
            item.RuntimePath = path;
            item.StatusText = "Installed";
            item.DetailText = FormatRuntimeDetail(path, $"Target: {item.TargetPocketMineVersion}");
        }
        catch (Exception ex)
        {
            item.HasError = true;
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

        bool confirm = _dialogService != null
            ? await _dialogService.ShowDialogAsync(
                $"Delete {item.DisplayName}?",
                $"Are you sure you want to delete {item.DisplayName}? Servers using this Java version may fail to start until re-downloaded.",
                DialogType.Question,
                showCancel: true,
                primaryButtonText: "Delete",
                cancelButtonText: "Cancel") is DialogResult.Ok or DialogResult.Yes
            : await ConfirmationDialogWindow.ShowAsync(
                $"Delete {item.DisplayName}?",
                $"Are you sure you want to delete {item.DisplayName}? Servers using this Java version may fail to start until re-downloaded.",
                "Delete Runtime");

        if (!confirm) return;

        try
        {
            if (item.IsCustom && !string.IsNullOrWhiteSpace(item.RuntimePath) && Directory.Exists(item.RuntimePath))
            {
                Directory.Delete(item.RuntimePath, true);
                await RefreshAsync();
                return;
            }

            await _javaService.DeleteJavaRuntimeAsync(item.Version);
            item.IsInstalled = false;
            item.RuntimePath = null;
            item.StatusText = "Not Installed";
            item.DetailText = "Missing runtime. PocketMC will download it automatically.";
            UpdateSummaryStatus();
        }
        catch (Exception ex)
        {
            GlobalStatus = $"Failed to delete {item.DisplayName}: {ex.Message}";
        }
    }

    private async Task VerifyRuntimeAsync(JavaRuntimeItemViewModel? item)
    {
        if (item == null || !item.IsInstalled) return;

        try
        {
            string? path = item.RuntimePath ?? await _javaService.GetJavaExecutablePathAsync(item.Version);
            if (string.IsNullOrWhiteSpace(path))
            {
                GlobalStatus = $"Could not locate binary for {item.DisplayName}.";
                return;
            }

            string execPath = Directory.Exists(path) ? (FindJavaExecutableInFolder(path) ?? path) : path;
            bool ok = string.IsNullOrWhiteSpace(item.Version)
                ? File.Exists(execPath)
                : await _javaService.ValidateJavaRuntimeAsync(execPath, item.Version);

            if (ok)
            {
                GlobalStatus = $"Verified {item.DisplayName} is healthy.";
            }
            else
            {
                item.HasError = true;
                item.StatusText = "Verification Failed";
                GlobalStatus = $"{item.DisplayName} failed binary verification.";
            }
        }
        catch (Exception ex)
        {
            GlobalStatus = $"Verification failed for {item.DisplayName}: {ex.Message}";
        }
    }

    private async Task DeletePhpRuntimeAsync(PhpRuntimeItemViewModel? item)
    {
        if (_phpService == null || item == null || !item.IsInstalled) return;

        bool confirm = _dialogService != null
            ? await _dialogService.ShowDialogAsync(
                $"Delete PHP {item.Version}?",
                $"Delete PHP {item.Version}? PocketMine-MP instances targeting this runtime will need it re-downloaded before starting.",
                DialogType.Question,
                showCancel: true,
                primaryButtonText: "Delete",
                cancelButtonText: "Cancel") is DialogResult.Ok or DialogResult.Yes
            : await ConfirmationDialogWindow.ShowAsync(
                $"Delete PHP {item.Version}?",
                $"Delete PHP {item.Version}? PocketMine-MP instances targeting this runtime will need it re-downloaded.",
                "Delete PHP");

        if (!confirm) return;

        try
        {
            await _phpService.DeletePHPRuntimeAsync(item.Version);
            item.IsInstalled = false;
            item.RuntimePath = null;
            item.StatusText = "Not Installed";
            item.DetailText = $"Target: {item.TargetPocketMineVersion}. Missing runtime.";
            UpdateSummaryStatus();
        }
        catch (Exception ex)
        {
            GlobalStatus = $"Failed to delete PHP {item.Version}: {ex.Message}";
        }
    }

    private async Task AddCustomRuntimeAsync()
    {
        string? folderPath = _dialogService != null
            ? await _dialogService.OpenFolderDialogAsync("Select Custom JDK / JRE Installation Directory")
            : null;

        if (string.IsNullOrWhiteSpace(folderPath) &&
            Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow != null)
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
                    folderPath = folders[0].Path.LocalPath;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(folderPath)) return;

        try
        {
            string? javaBinary = FindJavaExecutableInFolder(folderPath);
            string detectedVersion = javaBinary != null ? DetectJavaMajorVersion(javaBinary) : "21";
            await _javaService.RegisterCustomJavaRuntimeAsync(detectedVersion, folderPath);
            await RefreshAsync();
            GlobalStatus = $"Registered custom Java {detectedVersion} runtime at {folderPath}";
        }
        catch (Exception ex)
        {
            GlobalStatus = $"Failed to register custom runtime: {ex.Message}";
        }
    }

    private void NavigateBack()
    {
        var navService = App.Services?.GetService(typeof(IAppNavigationService)) as IAppNavigationService;
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

    private static string FormatRuntimeDetail(string? path, string fallback)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return fallback;
        }

        if (Directory.Exists(path))
        {
            double sizeMb = GetDirectorySizeMb(path);
            return $"{path}  |  {sizeMb:F1} MB";
        }

        if (File.Exists(path))
        {
            return path;
        }

        return fallback;
    }

    private static double GetDirectorySizeMb(string directory)
    {
        try
        {
            long totalBytes = 0;
            foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var info = new FileInfo(file);
                    if ((info.Attributes & FileAttributes.ReparsePoint) == 0)
                    {
                        totalBytes += info.Length;
                    }
                }
                catch
                {
                }
            }
            return totalBytes / (1024.0 * 1024.0);
        }
        catch
        {
            return 0;
        }
    }

    private static string? FindJavaExecutableInFolder(string rootDir)
    {
        if (File.Exists(rootDir)) return rootDir;
        if (!Directory.Exists(rootDir)) return null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string macHome = Path.Combine(rootDir, "Contents", "Home", "bin", "java");
            if (File.Exists(macHome)) return macHome;
        }

        string direct = Path.Combine(rootDir, "bin", "java");
        if (File.Exists(direct)) return direct;

        string winDirect = Path.Combine(rootDir, "bin", "java.exe");
        if (File.Exists(winDirect)) return winDirect;

        foreach (string sub in Directory.GetDirectories(rootDir))
        {
            string nested = Path.Combine(sub, "bin", "java");
            if (File.Exists(nested)) return nested;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string nestedMac = Path.Combine(sub, "Contents", "Home", "bin", "java");
                if (File.Exists(nestedMac)) return nestedMac;
            }
        }

        return null;
    }

    private static string DetectJavaMajorVersion(string executablePath)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = "-version",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardError.ReadToEnd() + process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);

            var match = Regex.Match(output, @"version\s*""(?:1\.)?(\d+)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }
        catch
        {
        }

        return "21";
    }
}
