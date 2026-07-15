using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;
using PocketMC.App.Views;

namespace PocketMC.App.ViewModels
{
    public partial class SearchResultItemViewModel : ObservableObject
    {
        public ModrinthHit Hit { get; }

        [ObservableProperty]
        private bool _isInstalled;

        [ObservableProperty]
        private Avalonia.Media.Imaging.Bitmap? _iconBitmap;

        public string InitialLetter => Hit.Title.Length > 0 ? Hit.Title.Substring(0, 1).ToUpper() : "?";

        public SearchResultItemViewModel(ModrinthHit hit, bool isInstalled)
        {
            Hit = hit;
            _isInstalled = isInstalled;
        }

        public async Task LoadIconAsync(HttpClient httpClient)
        {
            if (string.IsNullOrWhiteSpace(Hit.IconUrl)) return;
            try
            {
                string cacheDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                    "PocketMC", "Cache", "AddonIcons");
                Directory.CreateDirectory(cacheDir);

                string hash;
                using (var sha256 = System.Security.Cryptography.SHA256.Create())
                {
                    var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Hit.IconUrl));
                    hash = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
                }

                string ext = Path.GetExtension(Hit.IconUrl);
                if (string.IsNullOrEmpty(ext)) ext = ".png";
                int qIndex = ext.IndexOf('?');
                if (qIndex > 0) ext = ext.Substring(0, qIndex);

                string localPath = Path.Combine(cacheDir, hash + ext);

                if (!File.Exists(localPath))
                {
                    var imageBytes = await httpClient.GetByteArrayAsync(Hit.IconUrl);
                    await File.WriteAllBytesAsync(localPath, imageBytes);
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        using var stream = File.OpenRead(localPath);
                        IconBitmap = new Avalonia.Media.Imaging.Bitmap(stream);
                    }
                    catch
                    {
                        // Ignore corrupt cache files
                    }
                });
            }
            catch
            {
                // Ignore load errors
            }
        }
    }

    public partial class MarketplaceViewModel : ObservableObject
    {
        private readonly CurseForgeService _curseForgeService;
        private readonly ModrinthService _modrinthService;
        private readonly AddonManifestService _manifestService;
        private readonly DependencyResolverService _resolverService;
        private readonly HttpClient _httpClient;

        [ObservableProperty]
        private ServerInstance _instance;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private string _selectedProvider = "Modrinth";

        [ObservableProperty]
        private ObservableCollection<string> _providers = new() { "Modrinth", "CurseForge" };

        [ObservableProperty]
        private string _selectedType = "Mods";

        [ObservableProperty]
        private ObservableCollection<string> _types = new() { "Mods", "Plugins" };

        [ObservableProperty]
        private ObservableCollection<SearchResultItemViewModel> _searchResults = new();

        [ObservableProperty]
        private ObservableCollection<AddonManifestEntry> _installedAddons = new();

        [ObservableProperty]
        private SearchResultItemViewModel? _selectedResult;

        [ObservableProperty]
        private AddonManifestEntry? _selectedInstalledAddon;

        // UI states
        [ObservableProperty]
        private bool _isSearching;

        [ObservableProperty]
        private bool _isDownloading;

        [ObservableProperty]
        private string _statusText = "Ready";

        [ObservableProperty]
        private double _progressPercent;

        public MarketplaceViewModel(
            CurseForgeService curseForgeService,
            ModrinthService modrinthService,
            AddonManifestService manifestService,
            DependencyResolverService resolverService,
            HttpClient httpClient)
        {
            _curseForgeService = curseForgeService;
            _modrinthService = modrinthService;
            _manifestService = manifestService;
            _resolverService = resolverService;
            _httpClient = httpClient;

            // Default fallback
            _instance = new ServerInstance();
        }

        public void Initialize(ServerInstance instance)
        {
            Instance = instance;
            
            // Adjust categories depending on engine type
            Types.Clear();
            if (instance.EngineType == EngineType.VanillaJava ||
                instance.EngineType == EngineType.Fabric ||
                instance.EngineType == EngineType.Forge ||
                instance.EngineType == EngineType.NeoForge)
            {
                Types.Add("Mods");
            }
            else if (instance.EngineType == EngineType.Paper ||
                     instance.EngineType == EngineType.PocketMine)
            {
                Types.Add("Plugins");
            }
            else
            {
                Types.Add("Mods");
                Types.Add("Plugins");
            }
            SelectedType = Types.FirstOrDefault() ?? "Mods";

            _ = InitializeMarketplaceAsync();
        }

        private async Task InitializeMarketplaceAsync()
        {
            await RefreshInstalledListAsync();
            // Fetch popular mods/plugins for blank state initially
            await SearchAsync();
        }

        private async Task RefreshInstalledListAsync()
        {
            try
            {
                var manifest = await _manifestService.LoadManifestAsync(Instance.Path);
                
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    InstalledAddons.Clear();
                    foreach (var entry in manifest.Entries)
                    {
                        InstalledAddons.Add(entry);
                    }
                });
            }
            catch (Exception ex)
            {
                StatusText = $"Failed to load installed list: {ex.Message}";
            }
        }

        [RelayCommand]
        private void NavigateBack()
        {
            var mainVM = App.Services.GetService<MainWindowViewModel>();
            if (mainVM != null)
            {
                mainVM.CurrentViewModel = App.Services.GetRequiredService<DashboardViewModel>();
            }
        }

        [RelayCommand]
        private async Task SearchAsync()
        {
            IsSearching = true;
            string displayQuery = string.IsNullOrWhiteSpace(SearchQuery) ? "popular items" : $"'{SearchQuery}'";
            StatusText = $"Searching {SelectedProvider} for {displayQuery}...";
            SearchResults.Clear();

            try
            {
                string apiType = SelectedType == "Mods" ? "project_type:mod" : "project_type:plugin";
                string mcVersion = Instance.EngineVersion;
                string loader = Instance.EngineType.ToString().ToLowerInvariant();

                List<ModrinthHit> hits;

                if (SelectedProvider == "CurseForge")
                {
                    hits = await _curseForgeService.SearchAsync(apiType, mcVersion, loader, SearchQuery);
                }
                else
                {
                    hits = await _modrinthService.SearchAsync(apiType, mcVersion, new[] { loader }, query: SearchQuery);
                }

                // If blank state, limit to top 10 items
                if (string.IsNullOrWhiteSpace(SearchQuery) && hits.Count > 10)
                {
                    hits = hits.Take(10).ToList();
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var hit in hits)
                    {
                        bool isInstalled = InstalledAddons.Any(a => a.ProjectId == hit.ProjectId);
                        var itemVM = new SearchResultItemViewModel(hit, isInstalled);
                        SearchResults.Add(itemVM);
                        // Download icon in background
                        _ = itemVM.LoadIconAsync(_httpClient);
                    }
                    StatusText = $"Found {SearchResults.Count} matching items.";
                });
            }
            catch (Exception ex)
            {
                StatusText = $"Search failed: {ex.Message}";
            }
            finally
            {
                IsSearching = false;
            }
        }

        [RelayCommand]
        private async Task InstallAddonAsync(SearchResultItemViewModel itemVM)
        {
            if (itemVM == null) return;

            IsDownloading = true;
            ProgressPercent = 0;
            StatusText = $"Resolving dependencies for {itemVM.Hit.Title}...";

            try
            {
                IAddonProvider provider = SelectedProvider == "CurseForge" 
                    ? (IAddonProvider)_curseForgeService 
                    : (IAddonProvider)_modrinthService;

                string mcVersion = Instance.EngineVersion;
                string loader = Instance.EngineType.ToString().ToLowerInvariant();
                var compat = new EngineCompatibility(Instance.EngineType.ToString());

                // 1. Resolve direct & transitive dependencies
                var resolved = await _resolverService.ResolveAsync(
                    provider,
                    Instance.Path,
                    itemVM.Hit.ProjectId,
                    mcVersion,
                    loader,
                    compat);

                if (resolved == null || !resolved.Any())
                {
                    StatusText = "No compatible files found for installation.";
                    return;
                }

                // 2. Ask user for confirmation on dependency mods to install
                var tcs = new TaskCompletionSource<bool>();
                Dispatcher.UIThread.Post(async () =>
                {
                    var lifetime = Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
                    var owner = lifetime?.MainWindow;

                    var dialog = new DependencyConfirmWindow(resolved);
                    
                    bool result = false;
                    if (owner != null)
                    {
                        result = await dialog.ShowDialog<bool>(owner);
                    }
                    else
                    {
                        result = await dialog.ShowDialog<bool>(dialog);
                    }
                    tcs.SetResult(result);
                });

                bool confirmed = await tcs.Task;
                if (!confirmed)
                {
                    StatusText = "Installation cancelled.";
                    return;
                }

                // Filter selected dependencies to download
                var selectedDeps = resolved.Where(r => r.IsSelected && !r.IsAlreadyInstalled).ToList();
                int toInstallCount = selectedDeps.Count;
                if (toInstallCount == 0)
                {
                    StatusText = $"{itemVM.Hit.Title} and all selected dependencies are already installed.";
                    return;
                }

                // 3. Download and install selected dependencies
                int currentIdx = 0;
                foreach (var dep in selectedDeps)
                {
                    currentIdx++;
                    Dispatcher.UIThread.Post(() => StatusText = $"Downloading {currentIdx}/{toInstallCount}: {dep.ProjectTitle}...");

                    var progress = new Progress<double>(p =>
                    {
                        double baseProgress = (double)(currentIdx - 1) / toInstallCount * 100.0;
                        double itemProgress = p / toInstallCount;
                        ProgressPercent = baseProgress + itemProgress;
                    });

                    await _manifestService.InstallAddonAsync(
                        Instance.Path,
                        compat.PrimaryAddonSubDir,
                        dep,
                        _httpClient,
                        CancellationToken.None,
                        progress);
                }

                StatusText = $"Successfully installed {itemVM.Hit.Title}!";
                ProgressPercent = 100;
                
                await RefreshInstalledListAsync();

                // Update installation state in SearchResults list
                foreach (var resolvedDep in resolved.Where(r => r.IsSelected))
                {
                    var matchingSearchHit = SearchResults.FirstOrDefault(s => s.Hit.ProjectId == resolvedDep.ProjectId);
                    if (matchingSearchHit != null)
                    {
                        matchingSearchHit.IsInstalled = true;
                    }
                }
                itemVM.IsInstalled = true;
            }
            catch (Exception ex)
            {
                StatusText = $"Installation failed: {ex.Message}";
            }
            finally
            {
                IsDownloading = false;
            }
        }

        [RelayCommand]
        private async Task UninstallAddonAsync(AddonManifestEntry entry)
        {
            if (entry == null) return;

            IsDownloading = true;
            StatusText = $"Uninstalling {entry.ProjectTitle}...";

            try
            {
                var compat = new EngineCompatibility(Instance.EngineType.ToString());
                string subDir = compat.PrimaryAddonSubDir;
                string path = Path.Combine(Instance.Path, subDir, entry.FileName);

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                await _manifestService.UnregisterAsync(Instance.Path, entry.Provider, entry.ProjectId);
                StatusText = $"Successfully uninstalled {entry.ProjectTitle}.";
                
                await RefreshInstalledListAsync();

                // Update installation state in SearchResults list
                var matchingSearchHit = SearchResults.FirstOrDefault(s => s.Hit.ProjectId == entry.ProjectId);
                if (matchingSearchHit != null)
                {
                    matchingSearchHit.IsInstalled = false;
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Failed to uninstall addon: {ex.Message}";
            }
            finally
            {
                IsDownloading = false;
            }
        }
    }
}
