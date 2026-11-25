using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;

namespace PocketMC.App.ViewModels;

public partial class ModpackDownloadItemViewModel : ObservableObject
{
    public ModpackFile File { get; }

    [ObservableProperty] private string _statusText = "Pending";
    [ObservableProperty] private string _statusColor = "#A1A1AA";
    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private bool _isDownloading;

    public string ProjectTitle => string.IsNullOrWhiteSpace(File.Name) ? Path.GetFileName(File.DestinationPath) : File.Name;
    public string FileName => File.DestinationPath;

    public ModpackDownloadItemViewModel(ModpackFile file)
    {
        File = file;
    }
}

public partial class ModpackInstallViewModel : ObservableObject
{
    private readonly string _modpackPath;
    private readonly ModpackParser _parser;
    private readonly IInstanceService _instanceService;
    private readonly IJavaService _javaService;
    private readonly DownloaderService _downloader;
    private readonly CurseForgeService? _curseForgeService;

    private CancellationTokenSource? _cts;
    private ServerInstance? _createdInstance;

    [ObservableProperty] private string _title = "Installing Modpack...";
    [ObservableProperty] private string _summaryText = "Parsing modpack manifest and downloading files concurrently.";
    [ObservableProperty] private string _overallStatusText = "Preparing...";
    [ObservableProperty] private double _overallProgress;
    [ObservableProperty] private bool _isIndeterminate = true;
    [ObservableProperty] private string? _alertMessage;
    [ObservableProperty] private bool _hasAlert;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isCompleted;
    [ObservableProperty] private string _actionButtonText = "Cancel";

    public ObservableCollection<ModpackDownloadItemViewModel> Items { get; } = new();

    public IAsyncRelayCommand CancelOrCloseCommand { get; }

    public ModpackInstallViewModel(
        string modpackPath,
        ModpackParser parser,
        IInstanceService instanceService,
        IJavaService javaService,
        DownloaderService downloader,
        CurseForgeService? curseForgeService = null)
    {
        _modpackPath = modpackPath;
        _parser = parser;
        _instanceService = instanceService;
        _javaService = javaService;
        _downloader = downloader;
        _curseForgeService = curseForgeService;

        CancelOrCloseCommand = new AsyncRelayCommand(CancelOrCloseAsync);
    }

    public event Action? RequestClose;

    public async Task StartInstallAsync()
    {
        if (IsRunning || IsCompleted) return;
        IsRunning = true;
        ActionButtonText = "Cancel";
        _cts = new CancellationTokenSource();

        try
        {
            OverallStatusText = "Reading modpack archive...";
            ModpackImportResult pack = await _parser.ParseZipAsync(_modpackPath);

            Title = $"Installing {pack.Name}";
            SummaryText = $"{pack.Loader} {pack.LoaderVersion} · Minecraft {pack.MinecraftVersion} · {pack.Mods.Count} files";

            EngineType engine = pack.Loader switch
            {
                "Fabric" => EngineType.Fabric,
                "Forge" => EngineType.Forge,
                "NeoForge" => EngineType.NeoForge,
                _ => throw new InvalidOperationException($"Unsupported loader: {pack.Loader}")
            };

            string safeName = SanitizeInstanceName(pack.Name);
            _createdInstance = await _instanceService.CreateInstanceAsync(safeName, engine, pack.MinecraftVersion);
            _createdInstance.IsImportedFromModpack = true;
            _createdInstance.ModpackName = pack.Name;
            _createdInstance.ModpackSource = Path.GetExtension(_modpackPath).TrimStart('.').ToUpperInvariant();
            _createdInstance.LoaderVersion = pack.LoaderVersion;
            await _instanceService.SaveInstanceAsync(_createdInstance);

            foreach (var file in pack.Mods)
            {
                Items.Add(new ModpackDownloadItemViewModel(file));
            }

            OverallStatusText = "Provisioning Java runtime...";
            string javaVer = MapJavaVersion(pack.MinecraftVersion);
            await _javaService.ProvisionJavaRuntimeAsync(javaVer);

            string targetDir = _createdInstance.Path;
            await File.WriteAllTextAsync(Path.Combine(targetDir, "eula.txt"), "eula=true\n", _cts.Token);

            IsIndeterminate = false;
            int total = Items.Count;
            int done = 0;

            using var semaphore = new SemaphoreSlim(4);
            var tasks = Items.Select(async item =>
            {
                await semaphore.WaitAsync(_cts.Token);
                try
                {
                    await DownloadModItemAsync(item, targetDir, _cts.Token);
                }
                finally
                {
                    int completedCount = Interlocked.Increment(ref done);
                    Dispatcher.UIThread.Post(() =>
                    {
                        OverallProgress = total > 0 ? (double)completedCount / total * 85.0 : 85.0;
                        OverallStatusText = $"Downloaded {completedCount} / {total} modpack items...";
                    });
                    semaphore.Release();
                }
            }).ToArray();

            await Task.WhenAll(tasks);

            OverallStatusText = "Extracting modpack overrides...";
            OverallProgress = 90;
            ExtractOverrides(_modpackPath, targetDir);

            OverallProgress = 100;
            OverallStatusText = $"Modpack '{pack.Name}' installed successfully!";
            IsCompleted = true;
            ActionButtonText = "Done";
        }
        catch (OperationCanceledException)
        {
            await CleanupFailedInstanceAsync();
            AlertMessage = "Modpack installation cancelled.";
            HasAlert = true;
            ActionButtonText = "Close";
        }
        catch (Exception ex)
        {
            await CleanupFailedInstanceAsync();
            AlertMessage = ex.Message;
            HasAlert = true;
            OverallStatusText = "Installation failed.";
            ActionButtonText = "Close";
        }
        finally
        {
            IsRunning = false;
            IsIndeterminate = false;
        }
    }

    private async Task DownloadModItemAsync(ModpackDownloadItemViewModel item, string targetDir, CancellationToken ct)
    {
        Dispatcher.UIThread.Post(() =>
        {
            item.IsDownloading = true;
            item.StatusText = "Downloading...";
            item.StatusColor = "#4EA0FF";
        });

        try
        {
            string? resolvedUrl = item.File.DownloadUrl;
            if (resolvedUrl.StartsWith("CURSEFORGE:", StringComparison.OrdinalIgnoreCase))
            {
                if (_curseForgeService == null || string.IsNullOrEmpty(item.File.VersionId))
                {
                    throw new InvalidOperationException("CurseForge file download requires a CurseForge API key configured in Settings.");
                }

                var versionInfo = await _curseForgeService.GetVersionByIdAsync(item.File.VersionId);
                resolvedUrl = versionInfo?.DownloadUrl;
                if (string.IsNullOrWhiteSpace(resolvedUrl))
                {
                    throw new InvalidOperationException("CurseForge author disallowed automated distribution.");
                }
            }

            string? dest = PathSafety.ValidateContainedPath(targetDir, item.File.DestinationPath);
            if (string.IsNullOrEmpty(dest))
            {
                throw new InvalidOperationException($"Unsafe destination path: {item.File.DestinationPath}");
            }

            string? parent = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            await _downloader.DownloadFileAsync(
                resolvedUrl,
                dest,
                item.File.HashSha512,
                string.IsNullOrWhiteSpace(item.File.HashSha512) ? null : "SHA512",
                new Progress<DownloadProgress>(p =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        item.ProgressValue = p.Percentage;
                    });
                }),
                ct);

            Dispatcher.UIThread.Post(() =>
            {
                item.IsDownloading = false;
                item.ProgressValue = 100;
                item.StatusText = "Done";
                item.StatusColor = "#2ECC71";
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                item.IsDownloading = false;
                item.StatusText = "Skipped";
                item.StatusColor = "#F59E0B";
            });
            System.Diagnostics.Debug.WriteLine($"Modpack item failed ({item.ProjectTitle}): {ex.Message}");
        }
    }

    private static void ExtractOverrides(string zipPath, string targetDir)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;

            string? relative = null;
            if (entry.FullName.StartsWith("overrides/", StringComparison.OrdinalIgnoreCase))
            {
                relative = entry.FullName["overrides/".Length..];
            }
            else if (entry.FullName.StartsWith("server-overrides/", StringComparison.OrdinalIgnoreCase))
            {
                relative = entry.FullName["server-overrides/".Length..];
            }

            if (string.IsNullOrWhiteSpace(relative)) continue;

            string? safeDest = PathSafety.ValidateContainedPath(targetDir, relative);
            if (safeDest == null) continue;

            string? dir = Path.GetDirectoryName(safeDest);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            entry.ExtractToFile(safeDest, overwrite: true);
        }
    }

    private async Task CancelOrCloseAsync()
    {
        if (IsRunning && _cts != null)
        {
            _cts.Cancel();
            return;
        }

        RequestClose?.Invoke();
    }

    private async Task CleanupFailedInstanceAsync()
    {
        if (_createdInstance != null)
        {
            try
            {
                await _instanceService.DeleteInstanceAsync(_createdInstance.Slug);
            }
            catch { }
            _createdInstance = null;
        }
    }

    private static string SanitizeInstanceName(string name)
    {
        string cleaned = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ' ').ToArray()).Trim();
        return cleaned.Length >= 3 ? cleaned.Replace(' ', '-') : "Modpack-Server";
    }

    private static string MapJavaVersion(string mcVersion)
    {
        if (Version.TryParse(mcVersion, out var v))
        {
            if (v >= new Version(1, 22, 0)) return "25";
            if (v >= new Version(1, 20, 5)) return "21";
            if (v >= new Version(1, 17, 0)) return "17";
            if (v >= new Version(1, 12, 0)) return "11";
        }
        return "8";
    }
}
