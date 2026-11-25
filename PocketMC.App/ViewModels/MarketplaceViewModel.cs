using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PocketMC.App.Navigation;
using PocketMC.App.ViewModels.Marketplace;
using PocketMC.App.Views;
using PocketMC.App.Views.Dialogs;
using PocketMC.App.Views.Marketplace;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Configuration;
using PocketMC.Infrastructure.Services;
using PocketMC.Infrastructure.Utils;

namespace PocketMC.App.ViewModels;

public partial class SearchResultItemViewModel : ObservableObject
{
    public ModrinthHit Hit { get; }
    public string Provider { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActionButtonText))]
    private bool _isInstalled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActionButtonText))]
    private bool _isInstalling;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private Avalonia.Media.Imaging.Bitmap? _iconBitmap;

    public string InitialLetter => Hit.Title.Length > 0 ? Hit.Title.Substring(0, 1).ToUpperInvariant() : "?";
    public string ActionButtonText => IsInstalling ? "Installing..." : (IsInstalled ? "Reinstall" : "Install");

    public SearchResultItemViewModel(ModrinthHit hit, bool isInstalled, string provider = "Modrinth")
    {
        Hit = hit;
        _isInstalled = isInstalled;
        Provider = provider;
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
                byte[] bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Hit.IconUrl));
                hash = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            }

            string ext = Path.GetExtension(Hit.IconUrl);
            if (string.IsNullOrEmpty(ext)) ext = ".png";
            int qIndex = ext.IndexOf('?');
            if (qIndex > 0) ext = ext.Substring(0, qIndex);

            string localPath = Path.Combine(cacheDir, hash + ext);

            if (!File.Exists(localPath))
            {
                byte[] imageBytes = await httpClient.GetByteArrayAsync(Hit.IconUrl);
                await File.WriteAllBytesAsync(localPath, imageBytes);
            }

            if (Avalonia.Application.Current == null)
            {
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    using var stream = File.OpenRead(localPath);
                    IconBitmap = new Avalonia.Media.Imaging.Bitmap(stream);
                }
                catch
                {
                }
            });
        }
        catch
        {
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
    private readonly ISettingsService? _settingsService;
    private readonly WorldManager? _worldManager;
    private readonly IDialogService? _dialogService;

    private int _currentOffset;
    private bool _suppressAutoSearch;
    private Action<string>? _onMapDownloaded;
    private Action<string>? _onModpackDownloaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MinecraftVersionBadgeText))]
    private ServerInstance _instance;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSelectSort))]
    private string _selectedProvider = "Modrinth";

    [ObservableProperty]
    private ObservableCollection<string> _providers = new() { "Modrinth", "CurseForge" };

    [ObservableProperty]
    private string _selectedType = "Mods";

    [ObservableProperty]
    private ObservableCollection<string> _types = new() { "Mods", "Plugins" };

    [ObservableProperty]
    private string _selectedSort = "Trending";

    public IReadOnlyList<string> SortOptions { get; } = new[] { "Trending", "Top Downloads", "Newest" };

    [ObservableProperty]
    private string _selectedMapSort = "Popularity";

    public IReadOnlyList<string> MapSortOptions { get; } = new[]
    {
        "Relevancy",
        "Popularity",
        "Latest Update",
        "Name",
        "Total Downloads"
    };

    [ObservableProperty]
    private string _selectedMapCategory = "All Categories";

    public IReadOnlyList<string> MapCategories { get; } = new[]
    {
        "All Categories",
        "Adventure",
        "Creation",
        "Game Map",
        "Modded World",
        "Parkour",
        "Puzzle",
        "Survival"
    };

    [ObservableProperty]
    private string _selectedModpackLoader = "Any";

    public IReadOnlyList<string> ModpackLoaders { get; } = new[] { "Any", "Fabric", "Forge", "NeoForge" };

    [ObservableProperty]
    private string _selectedModpackMcVersion = "Any";

    [ObservableProperty]
    private ObservableCollection<string> _modpackMcVersions = new() { "Any" };

    [ObservableProperty]
    private ObservableCollection<SearchResultItemViewModel> _searchResults = new();

    [ObservableProperty]
    private ObservableCollection<AddonManifestEntry> _installedAddons = new();

    [ObservableProperty]
    private SearchResultItemViewModel? _selectedResult;

    [ObservableProperty]
    private AddonManifestEntry? _selectedInstalledAddon;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private bool _isLoadingMore;

    [ObservableProperty]
    private bool _hasMoreResults;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMcVersionBadge))]
    [NotifyPropertyChangedFor(nameof(ShowStandardFilters))]
    private bool _isMapMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMcVersionBadge))]
    [NotifyPropertyChangedFor(nameof(ShowStandardFilters))]
    private bool _isModpackMode;

    [ObservableProperty]
    private string _pageTitle = "Plugin & Mod Marketplace";

    [ObservableProperty]
    private string _searchPlaceholder = "Search mods/plugins...";

    [ObservableProperty]
    private bool _showCurseForgeKeyPanel;

    [ObservableProperty]
    private string _curseForgeKeyTitle = "CurseForge API Key Required";

    [ObservableProperty]
    private string _curseForgeKeyDetail = "CurseForge requires a free API key to browse and install addons. Enter your key to continue.";

    [ObservableProperty]
    private bool _isEmptyResults;

    [ObservableProperty]
    private string _emptyStateDetail = "We couldn't find anything matching your search. Try different keywords or filters.";

    public bool ShowMcVersionBadge => !IsModpackMode;
    public bool ShowStandardFilters => !IsMapMode && !IsModpackMode;
    public bool CanSelectSort => string.Equals(SelectedProvider, "Modrinth", StringComparison.OrdinalIgnoreCase);

    public string MinecraftVersionBadgeText =>
        string.IsNullOrWhiteSpace(Instance.EngineVersion) || Instance.EngineVersion == "*"
            ? "All Versions"
            : $"Minecraft {Instance.EngineVersion}";

    public MarketplaceViewModel(
        CurseForgeService curseForgeService,
        ModrinthService modrinthService,
        AddonManifestService manifestService,
        DependencyResolverService resolverService,
        HttpClient httpClient,
        ISettingsService? settingsService = null,
        WorldManager? worldManager = null,
        IDialogService? dialogService = null)
    {
        _curseForgeService = curseForgeService;
        _modrinthService = modrinthService;
        _manifestService = manifestService;
        _resolverService = resolverService;
        _httpClient = httpClient;
        _settingsService = settingsService ?? App.Services?.GetService<ISettingsService>();
        _worldManager = worldManager ?? App.Services?.GetService<WorldManager>() ?? new WorldManager();
        _dialogService = dialogService ?? App.Services?.GetService<IDialogService>();

        _instance = new ServerInstance();
    }

    public void Initialize(ServerInstance instance)
    {
        _suppressAutoSearch = true;
        try
        {
            Instance = instance;
            IsMapMode = false;
            IsModpackMode = false;
            _onMapDownloaded = null;
            _onModpackDownloaded = null;

            Providers.Clear();
            Providers.Add("Modrinth");
            if (instance.EngineType != EngineType.PocketMine)
            {
                Providers.Add("CurseForge");
            }
            SelectedProvider = "Modrinth";

            Types.Clear();
            if (instance.EngineType == EngineType.VanillaJava ||
                instance.EngineType == EngineType.Fabric ||
                instance.EngineType == EngineType.Forge ||
                instance.EngineType == EngineType.NeoForge)
            {
                Types.Add("Mods");
                PageTitle = "Mod Marketplace";
                SearchPlaceholder = $"Search {instance.EngineType} mods...";
            }
            else if (instance.EngineType == EngineType.Paper ||
                     instance.EngineType == EngineType.PocketMine)
            {
                Types.Add("Plugins");
                PageTitle = instance.EngineType == EngineType.PocketMine
                    ? "Pocketmine Plugins"
                    : "Plugin Marketplace";
                SearchPlaceholder = instance.EngineType == EngineType.PocketMine
                    ? "Search Pocketmine plugins (*.phar)..."
                    : "Search Spigot/Paper plugins...";
            }
            else if (instance.EngineType == EngineType.Bedrock)
            {
                Types.Add("Mods");
                PageTitle = "Bedrock Add-Ons Marketplace";
                SearchPlaceholder = "Search Bedrock Add-Ons...";
            }
            else
            {
                Types.Add("Mods");
                Types.Add("Plugins");
                PageTitle = "Plugin & Mod Marketplace";
                SearchPlaceholder = "Search mods/plugins...";
            }

            SelectedType = Types.FirstOrDefault() ?? "Mods";
            SelectedSort = "Trending";
            ShowCurseForgeKeyPanel = false;
            IsEmptyResults = false;
        }
        finally
        {
            _suppressAutoSearch = false;
        }

        _ = InitializeMarketplaceAsync();
    }

    public async Task InitializeForMapsAsync(ServerInstance instance, Action<string>? onMapDownloaded = null)
    {
        _suppressAutoSearch = true;
        try
        {
            Instance = instance;
            IsMapMode = true;
            IsModpackMode = false;
            _onMapDownloaded = onMapDownloaded;
            _onModpackDownloaded = null;

            Providers.Clear();
            Providers.Add("CurseForge");
            SelectedProvider = "CurseForge";

            Types.Clear();
            Types.Add("Maps");
            SelectedType = "Maps";

            SelectedMapSort = "Popularity";
            SelectedMapCategory = "All Categories";
            PageTitle = "World Maps Browser";
            SearchPlaceholder = "Search maps...";
            ShowCurseForgeKeyPanel = false;
            IsEmptyResults = false;
        }
        finally
        {
            _suppressAutoSearch = false;
        }

        await SearchAsync();
    }

    public async Task InitializeForModpacksAsync(Action<string>? onModpackDownloaded = null)
    {
        _suppressAutoSearch = true;
        try
        {
            Instance = new ServerInstance { EngineType = EngineType.VanillaJava, EngineVersion = "*" };
            IsMapMode = false;
            IsModpackMode = true;
            _onModpackDownloaded = onModpackDownloaded;
            _onMapDownloaded = null;

            Providers.Clear();
            Providers.Add("Modrinth");
            Providers.Add("CurseForge");
            SelectedProvider = "Modrinth";

            Types.Clear();
            Types.Add("Modpacks");
            SelectedType = "Modpacks";

            SelectedSort = "Trending";
            SelectedModpackLoader = "Any";
            SelectedModpackMcVersion = "Any";
            PageTitle = "Modpack Marketplace";
            SearchPlaceholder = "Search modpacks...";
            ShowCurseForgeKeyPanel = false;
            IsEmptyResults = false;
        }
        finally
        {
            _suppressAutoSearch = false;
        }

        await LoadModpackVersionsForLoaderAsync();
        await SearchAsync();
    }

    private async Task LoadModpackVersionsForLoaderAsync()
    {
        try
        {
            var list = new List<string> { "Any" };
            string json = await _httpClient.GetStringAsync(AppConfig.ProviderMojangManifest);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("versions", out var versionsEl))
            {
                foreach (var entry in versionsEl.EnumerateArray())
                {
                    if (entry.TryGetProperty("type", out var typeProp) &&
                        typeProp.GetString() == "release" &&
                        entry.TryGetProperty("id", out var idProp) &&
                        !string.IsNullOrWhiteSpace(idProp.GetString()))
                    {
                        list.Add(idProp.GetString()!);
                        if (list.Count >= 40) break;
                    }
                }
            }

            DispatchOnUi(() =>
            {
                ModpackMcVersions.Clear();
                foreach (string v in list)
                {
                    ModpackMcVersions.Add(v);
                }

                if (!ModpackMcVersions.Contains(SelectedModpackMcVersion))
                {
                    SelectedModpackMcVersion = "Any";
                }
            });
        }
        catch
        {
        }
    }

    private async Task InitializeMarketplaceAsync()
    {
        await RefreshInstalledListAsync();
        await SearchAsync();
    }

    partial void OnSelectedProviderChanged(string value)
    {
        if (!_suppressAutoSearch && !string.IsNullOrWhiteSpace(value))
        {
            _ = SearchAsync();
        }
    }

    partial void OnSelectedTypeChanged(string value)
    {
        if (!_suppressAutoSearch && !string.IsNullOrWhiteSpace(value))
        {
            _ = SearchAsync();
        }
    }

    partial void OnSelectedSortChanged(string value)
    {
        if (!_suppressAutoSearch && !string.IsNullOrWhiteSpace(value))
        {
            _ = SearchAsync();
        }
    }

    partial void OnSelectedMapSortChanged(string value)
    {
        if (!_suppressAutoSearch && IsMapMode && !string.IsNullOrWhiteSpace(value))
        {
            _ = SearchAsync();
        }
    }

    partial void OnSelectedMapCategoryChanged(string value)
    {
        if (!_suppressAutoSearch && IsMapMode && !string.IsNullOrWhiteSpace(value))
        {
            _ = SearchAsync();
        }
    }

    partial void OnSelectedModpackLoaderChanged(string value)
    {
        if (!_suppressAutoSearch && IsModpackMode && !string.IsNullOrWhiteSpace(value))
        {
            _ = ReloadModpackVersionsAndSearchAsync();
        }
    }

    partial void OnSelectedModpackMcVersionChanged(string value)
    {
        if (!_suppressAutoSearch && IsModpackMode && !string.IsNullOrWhiteSpace(value))
        {
            _ = SearchAsync();
        }
    }

    private async Task ReloadModpackVersionsAndSearchAsync()
    {
        await LoadModpackVersionsForLoaderAsync();
        await SearchAsync();
    }

    private async Task RefreshInstalledListAsync()
    {
        if (string.IsNullOrWhiteSpace(Instance.Path) || !Directory.Exists(Instance.Path))
        {
            return;
        }

        try
        {
            var manifest = await _manifestService.LoadManifestAsync(Instance.Path);
            DispatchOnUi(() =>
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
        var navService = App.Services?.GetService<IAppNavigationService>();
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
            var mainVM = App.Services?.GetService<MainWindowViewModel>();
            if (mainVM != null)
            {
                mainVM.CurrentViewModel = App.Services?.GetRequiredService<DashboardViewModel>()!;
            }
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await SearchInternalAsync(append: false);
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (IsLoadingMore || IsSearching || !HasMoreResults) return;
        await SearchInternalAsync(append: true);
    }

    private async Task SearchInternalAsync(bool append)
    {
        if (!append)
        {
            _currentOffset = 0;
            IsSearching = true;
            ShowCurseForgeKeyPanel = false;
            IsEmptyResults = false;
            SearchResults.Clear();
        }
        else
        {
            IsLoadingMore = true;
        }

        string displayQuery = string.IsNullOrWhiteSpace(SearchQuery) ? "popular items" : $"'{SearchQuery}'";
        StatusText = $"Searching {SelectedProvider} for {displayQuery}...";

        try
        {
            List<ModrinthHit> hits;
            if (IsMapMode)
            {
                int sortField = SelectedMapSort switch
                {
                    "Relevancy" => 1,
                    "Popularity" => 2,
                    "Latest Update" => 3,
                    "Name" => 4,
                    "Total Downloads" => 6,
                    _ => 2
                };

                int? categoryId = SelectedMapCategory switch
                {
                    "Adventure" => 248,
                    "Creation" => 249,
                    "Game Map" => 250,
                    "Modded World" => 4464,
                    "Parkour" => 251,
                    "Puzzle" => 252,
                    "Survival" => 253,
                    _ => null
                };

                string mcVer = string.IsNullOrWhiteSpace(Instance.EngineVersion) || Instance.EngineVersion == "*"
                    ? ""
                    : Instance.EngineVersion;

                hits = await _curseForgeService.SearchAsync(
                    "project_type:world",
                    mcVer,
                    "",
                    SearchQuery,
                    _currentOffset,
                    sortField,
                    "desc",
                    categoryId);
            }
            else if (IsModpackMode)
            {
                string sort = MapSortKey(SelectedSort);
                string mcVersionArg = SelectedModpackMcVersion == "Any" ? "" : SelectedModpackMcVersion;
                string loaderArg = SelectedModpackLoader == "Any" ? "" : SelectedModpackLoader.ToLowerInvariant();
                IReadOnlyList<string> loaders = string.IsNullOrEmpty(loaderArg)
                    ? Array.Empty<string>()
                    : new[] { loaderArg };

                hits = SelectedProvider == "CurseForge"
                    ? await _curseForgeService.SearchAsync("project_type:modpack", mcVersionArg, loaderArg, SearchQuery, _currentOffset)
                    : await _modrinthService.SearchAsync("project_type:modpack", mcVersionArg, loaders, sort, SearchQuery, _currentOffset);
            }
            else
            {
                string apiType = SelectedType == "Plugins" ? "project_type:plugin" : "project_type:mod";
                var compat = new EngineCompatibility(Instance.EngineType.ToString());
                string mcVersion = (compat.Family == EngineFamily.Bedrock || compat.Family == EngineFamily.Pocketmine)
                    ? ""
                    : Instance.EngineVersion;
                string loader = compat.LoaderName;
                IReadOnlyList<string> loaders = compat.CompatibleLoaderNames.Count > 0
                    ? compat.CompatibleLoaderNames
                    : (string.IsNullOrWhiteSpace(loader) ? Array.Empty<string>() : new[] { loader });

                if (SelectedProvider == "CurseForge")
                {
                    string cfType = compat.Family == EngineFamily.Bedrock ? "6945" : apiType;
                    hits = await _curseForgeService.SearchAsync(cfType, mcVersion, loader, SearchQuery, _currentOffset);
                }
                else
                {
                    hits = await _modrinthService.SearchAsync(apiType, mcVersion, loaders, MapSortKey(SelectedSort), SearchQuery, _currentOffset);
                }
            }

            if (TryDetectCurseForgeKeyError(hits, out string keyTitle, out string keyDetail))
            {
                DispatchOnUi(() =>
                {
                    CurseForgeKeyTitle = keyTitle;
                    CurseForgeKeyDetail = keyDetail;
                    ShowCurseForgeKeyPanel = true;
                    IsEmptyResults = false;
                    HasMoreResults = false;
                    StatusText = keyTitle;
                });
                return;
            }

            DispatchOnUi(() =>
            {
                foreach (var hit in hits)
                {
                    if (!SearchResults.Any(r => string.Equals(r.Hit.Slug, hit.Slug, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(hit.Slug)))
                    {
                        bool isInstalled = !IsMapMode && !IsModpackMode &&
                                           InstalledAddons.Any(a => string.Equals(a.ProjectId, hit.ProjectId, StringComparison.OrdinalIgnoreCase));
                        var itemVM = new SearchResultItemViewModel(hit, isInstalled, SelectedProvider);
                        SearchResults.Add(itemVM);
                        _ = itemVM.LoadIconAsync(_httpClient);
                    }
                }

                _currentOffset += hits.Count;
                HasMoreResults = hits.Count >= 20;
                IsEmptyResults = SearchResults.Count == 0;
                if (IsEmptyResults)
                {
                    UpdateEmptyStateDetail();
                }
                StatusText = $"Found {SearchResults.Count} matching items.";
            });
        }
        catch (Exception ex)
        {
            DispatchOnUi(() =>
            {
                StatusText = $"Search failed: {ex.Message}";
                IsEmptyResults = SearchResults.Count == 0;
            });
        }
        finally
        {
            DispatchOnUi(() =>
            {
                IsSearching = false;
                IsLoadingMore = false;
            });
        }
    }

    private bool TryDetectCurseForgeKeyError(List<ModrinthHit> hits, out string title, out string detail)
    {
        title = string.Empty;
        detail = string.Empty;
        if (!string.Equals(SelectedProvider, "CurseForge", StringComparison.OrdinalIgnoreCase) || hits.Count != 1)
        {
            return false;
        }

        var first = hits[0];
        if (!string.IsNullOrEmpty(first.Slug) || !string.IsNullOrEmpty(first.ProjectId))
        {
            return false;
        }

        if (string.Equals(first.Title, "CurseForge Key Required", StringComparison.OrdinalIgnoreCase))
        {
            title = "CurseForge API Key Required";
            detail = "CurseForge requires a free API key to browse and install addons. Enter your key to continue.";
            return true;
        }

        if (first.Title.StartsWith("API Error: 401", StringComparison.OrdinalIgnoreCase) ||
            first.Title.StartsWith("API Error: 403", StringComparison.OrdinalIgnoreCase))
        {
            title = "Invalid CurseForge API Key";
            detail = "Your CurseForge API key was rejected (403 Forbidden). Check that your key is active and entered correctly.";
            return true;
        }

        return false;
    }

    private void UpdateEmptyStateDetail()
    {
        string trimmed = SearchQuery.Trim();
        if (IsMapMode)
        {
            EmptyStateDetail = string.IsNullOrEmpty(trimmed)
                ? "No worlds or maps found for the selected criteria. Try changing the category filter or search terms."
                : $"No worlds or maps matched \"{trimmed}\". Try checking for spelling errors or clearing the category filter.";
            return;
        }

        string itemType = IsModpackMode ? "modpacks" : (SelectedType == "Plugins" ? "plugins" : "mods");
        EmptyStateDetail = string.IsNullOrEmpty(trimmed)
            ? $"No {itemType} matched the selected filter criteria. Try selecting 'Any' for loader or version."
            : $"No {itemType} matched \"{trimmed}\". Try checking for spelling errors or adjusting your filters.";
    }

    [RelayCommand]
    private async Task ResetFiltersAsync()
    {
        _suppressAutoSearch = true;
        try
        {
            SearchQuery = string.Empty;
            SelectedSort = "Trending";
            if (IsMapMode)
            {
                SelectedMapSort = "Popularity";
                SelectedMapCategory = "All Categories";
            }
            if (IsModpackMode)
            {
                SelectedModpackLoader = "Any";
                SelectedModpackMcVersion = "Any";
            }
        }
        finally
        {
            _suppressAutoSearch = false;
        }

        await SearchAsync();
    }

    [RelayCommand]
    private async Task ConfigureCurseForgeKeyAsync()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow != null)
        {
            var dialog = new CurseForgeApiKeyDialogWindow();
            bool saved = await dialog.ShowDialog<bool>(desktop.MainWindow);
            if (saved && !string.IsNullOrWhiteSpace(dialog.ApiKey) && _settingsService != null)
            {
                _settingsService.Settings.CurseForgeApiKey = dialog.ApiKey;
                _settingsService.Save();
                await SearchAsync();
            }
        }
    }

    [RelayCommand]
    private async Task SwitchToModrinthAsync()
    {
        if (Providers.Contains("Modrinth"))
        {
            SelectedProvider = "Modrinth";
        }
        else
        {
            await Task.CompletedTask;
        }
    }

    [RelayCommand]
    private void OpenWebMaps()
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://www.minecraftmaps.com/search") { UseShellExecute = true });
        }
        catch
        {
        }
    }

    [RelayCommand]
    private async Task ImportLocalModpackAsync()
    {
        if (_dialogService == null) return;
        string? file = await _dialogService.OpenFileDialogAsync("Select Modpack Archive", "Modpack Files (*.zip;*.mrpack)|*.zip;*.mrpack|ZIP Files (*.zip)|*.zip|Modrinth Packs (*.mrpack)|*.mrpack");
        if (string.IsNullOrWhiteSpace(file)) return;

        if (_onModpackDownloaded != null)
        {
            _onModpackDownloaded.Invoke(file);
            NavigateBack();
            return;
        }

        await LaunchModpackInstallerDialogAsync(file);
    }

    [RelayCommand]
    private async Task InstallAddonAsync(SearchResultItemViewModel itemVM)
    {
        if (itemVM == null || itemVM.IsInstalling) return;

        itemVM.IsInstalling = true;
        IsDownloading = true;
        ProgressPercent = 0;

        try
        {
            if (IsMapMode)
            {
                await InstallMapAsync(itemVM);
                return;
            }

            IAddonProvider provider = SelectedProvider == "CurseForge"
                ? _curseForgeService
                : _modrinthService;

            string mcVersion = Instance.EngineVersion;
            var compat = IsModpackMode
                ? new EngineCompatibility(SelectedModpackLoader == "Any" ? "Vanilla" : SelectedModpackLoader)
                : new EngineCompatibility(Instance.EngineType.ToString());
            string loader = IsModpackMode
                ? (SelectedModpackLoader == "Any" ? "" : SelectedModpackLoader.ToLowerInvariant())
                : compat.LoaderName;
            string projectId = string.IsNullOrWhiteSpace(itemVM.Hit.ProjectId) ? itemVM.Hit.Slug : itemVM.Hit.ProjectId;

            if (compat.Family is EngineFamily.Bedrock or EngineFamily.Pocketmine)
            {
                mcVersion = "";
            }
            else if (IsModpackMode)
            {
                mcVersion = SelectedModpackMcVersion == "Any" ? "" : SelectedModpackMcVersion;
            }

            StatusText = $"Resolving dependencies for {itemVM.Hit.Title}...";
            var resolved = await _resolverService.ResolveAsync(
                provider,
                IsModpackMode ? "" : Instance.Path,
                projectId,
                mcVersion,
                loader,
                compat);

            if (resolved == null || resolved.Count == 0 || !string.IsNullOrEmpty(resolved[0].Error))
            {
                string detail = resolved?.FirstOrDefault()?.Error ?? "No compatible version found.";
                StatusText = detail;
                _dialogService?.ShowMessage("No compatible version found", $"Could not find a compatible version of {itemVM.Hit.Title}.\n\n{detail}", DialogType.Warning);
                return;
            }

            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow != null)
            {
                var confVm = new DependencyConfirmationViewModel(resolved);
                var confirmDialog = new DependencyConfirmWindow(confVm);
                bool confirmed = await confirmDialog.ShowDialog<bool>(desktop.MainWindow);
                if (!confirmed)
                {
                    StatusText = "Installation cancelled.";
                    return;
                }
            }

            var selectedDeps = resolved.Where(r => r.IsSelected && !r.IsAlreadyInstalled).ToList();
            if (selectedDeps.Count == 0)
            {
                StatusText = $"{itemVM.Hit.Title} is already installed.";
                itemVM.IsInstalled = true;
                return;
            }

            if (IsModpackMode)
            {
                var rootItem = selectedDeps[0];
                string? installedPath = await DownloadTempModpackAsync(rootItem);
                if (!string.IsNullOrWhiteSpace(installedPath))
                {
                    itemVM.IsInstalled = true;
                    StatusText = $"Downloaded modpack {itemVM.Hit.Title}.";
                    if (_onModpackDownloaded != null)
                    {
                        _onModpackDownloaded.Invoke(installedPath);
                        NavigateBack();
                    }
                    else
                    {
                        await LaunchModpackInstallerDialogAsync(installedPath);
                    }
                }
                return;
            }

            await InstallResolvedDependenciesAsync(itemVM, selectedDeps, compat);
        }
        catch (Exception ex)
        {
            StatusText = $"Installation failed: {ex.Message}";
            _dialogService?.ShowMessage("Install Failed", ex.Message, DialogType.Error);
        }
        finally
        {
            itemVM.IsInstalling = false;
            IsDownloading = false;
        }
    }

    private async Task InstallResolvedDependenciesAsync(
        SearchResultItemViewModel itemVM,
        List<ResolvedDependency> selectedDeps,
        EngineCompatibility compat)
    {
        bool anyInstalled = false;

        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow != null)
        {
            var rows = selectedDeps.Select(dep => new AddonInstallRowViewModel { ResolvedItem = dep }).ToList();
            var dialog = new AddonInstallDialogWindow();
            dialog.SetItems(rows);
            dialog.InstallAction = async (row, progress, ct) =>
            {
                var adapterProgress = new Progress<double>(pct =>
                {
                    progress.Report(new AddonInstallProgress(pct, 0, 0));
                });
                await DownloadAndVerifyAddonAsync(row.ResolvedItem, compat, adapterProgress, ct);
            };
            await dialog.ShowDialog(desktop.MainWindow);
            anyInstalled = dialog.AnyInstalled;
            StatusText = dialog.AnyInstalled
                ? $"Successfully installed {itemVM.Hit.Title}!"
                : "Installation cancelled or failed.";
        }
        else
        {
            int idx = 0;
            foreach (var dep in selectedDeps)
            {
                idx++;
                StatusText = $"Downloading {idx}/{selectedDeps.Count}: {dep.ProjectTitle}...";
                await DownloadAndVerifyAddonAsync(dep, compat, new Progress<double>(p => ProgressPercent = p), CancellationToken.None);
            }
            anyInstalled = true;
            StatusText = $"Successfully installed {itemVM.Hit.Title}!";
        }

        if (anyInstalled)
        {
            ProgressPercent = 100;
            await RefreshInstalledListAsync();
            foreach (var dep in selectedDeps)
            {
                var hit = SearchResults.FirstOrDefault(s => string.Equals(s.Hit.ProjectId, dep.ProjectId, StringComparison.OrdinalIgnoreCase));
                if (hit != null)
                {
                    hit.IsInstalled = true;
                }
            }
            itemVM.IsInstalled = true;
        }
    }

    private async Task DownloadAndVerifyAddonAsync(
        ResolvedDependency dep,
        EngineCompatibility compat,
        IProgress<double> progress,
        CancellationToken ct)
    {
        await _manifestService.InstallAddonAsync(
            Instance.Path,
            compat.PrimaryAddonSubDir,
            dep,
            _httpClient,
            ct,
            progress);

        string? installedFile = AddonManifestService.ResolveAddonFilePath(Instance.Path, compat.PrimaryAddonSubDir, dep.FileName);
        if (installedFile != null && File.Exists(installedFile))
        {
            bool isPlugin = SelectedType == "Plugins";
            IReadOnlyList<string> warnings = MarketplaceArchiveInspector.InspectServerCompatibilityWarnings(installedFile, isPlugin);
            if (warnings.Count > 0 || MarketplaceArchiveInspector.IsClientOnlyAddon(installedFile))
            {
                string message = warnings.Count > 0 ? warnings[0] : "Client-side only mods cannot be installed on a server.";
                try { File.Delete(installedFile); } catch { }
                await _manifestService.UnregisterAsync(Instance.Path, dep.Provider, dep.ProjectId);
                throw new InvalidOperationException(message);
            }
        }
    }

    private async Task InstallMapAsync(SearchResultItemViewModel itemVM)
    {
        StatusText = $"Fetching map archive for {itemVM.Hit.Title}...";
        string slugOrId = string.IsNullOrWhiteSpace(itemVM.Hit.ProjectId) ? itemVM.Hit.Slug : itemVM.Hit.ProjectId;
        string mcVer = string.IsNullOrWhiteSpace(Instance.EngineVersion) || Instance.EngineVersion == "*" ? "" : Instance.EngineVersion;

        var version = await _curseForgeService.GetLatestVersionAsync(slugOrId, mcVer, "");
        if (version == null || string.IsNullOrWhiteSpace(version.DownloadUrl))
        {
            StatusText = "No compatible world version found on CurseForge.";
            _dialogService?.ShowMessage("Not Found", "No compatible world version found on CurseForge.", DialogType.Warning);
            return;
        }

        string safeName = SanitizeArchiveFileName(version.FileName);
        string tempZip = Path.Combine(Path.GetTempPath(), $"pmc_map_{Guid.NewGuid():N}_{safeName}");
        await DownloadUrlToFileAsync(version.DownloadUrl, tempZip, new Progress<double>(p => ProgressPercent = p), CancellationToken.None);

        if (_onMapDownloaded != null)
        {
            _onMapDownloaded.Invoke(tempZip);
            NavigateBack();
            return;
        }

        if (_worldManager != null && !string.IsNullOrWhiteSpace(Instance.Path))
        {
            string targetWorld = WorldManager.ResolveWorldDirectory(Instance.Path, Instance.EngineType);
            await _worldManager.ImportWorldZipAsync(tempZip, targetWorld);
            try { File.Delete(tempZip); } catch { }
            StatusText = $"Imported map '{itemVM.Hit.Title}' cleanly.";
            _dialogService?.ShowMessage("Map Imported", $"World '{itemVM.Hit.Title}' imported successfully into {Instance.Name}.", DialogType.Information);
        }
    }

    private async Task<string?> DownloadTempModpackAsync(ResolvedDependency rootItem)
    {
        string safeName = SanitizeArchiveFileName(rootItem.FileName);
        string dest = Path.Combine(Path.GetTempPath(), $"pmc_modpack_{Guid.NewGuid():N}_{safeName}");
        await DownloadUrlToFileAsync(rootItem.DownloadUrl, dest, new Progress<double>(p => ProgressPercent = p), CancellationToken.None);
        return dest;
    }

    private async Task LaunchModpackInstallerDialogAsync(string localArchivePath)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow == null)
        {
            return;
        }

        var instService = App.Services?.GetService<IInstanceService>();
        var javaService = App.Services?.GetService<IJavaService>();
        if (instService == null || javaService == null) return;

        var parser = App.Services?.GetService<ModpackParser>() ?? new ModpackParser();
        var downloader = App.Services?.GetService<DownloaderService>() ?? new DownloaderService(_httpClient);
        var dialogVm = new ModpackInstallViewModel(
            localArchivePath,
            parser,
            instService,
            javaService,
            downloader,
            _curseForgeService);

        var dialog = new ModpackInstallDialogWindow(dialogVm);
        await dialog.ShowDialog(desktop.MainWindow);

        if (dialogVm.IsCompleted)
        {
            var dashVm = App.Services?.GetService<DashboardViewModel>();
            if (dashVm != null)
            {
                await dashVm.LoadInstancesAsync();
            }
            App.Services?.GetService<IAppNavigationService>()?.NavigateToDashboard();
        }
    }

    private async Task DownloadUrlToFileAsync(string url, string destPath, IProgress<double> progress, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        long totalBytes = response.Content.Headers.ContentLength ?? -1L;
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        await using var output = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        byte[] buffer = new byte[81920];
        long totalRead = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
            totalRead += read;
            if (totalBytes > 0)
            {
                progress.Report((double)totalRead / totalBytes * 100.0);
            }
        }
    }

    [RelayCommand]
    private async Task UninstallAddonAsync(AddonManifestEntry entry)
    {
        if (entry == null) return;

        bool confirmed = await ConfirmationDialogWindow.ShowAsync(
            "Uninstall Addon",
            $"Are you sure you want to uninstall '{entry.ProjectTitle}' ({entry.FileName})?",
            "Uninstall");

        if (!confirmed) return;

        IsDownloading = true;
        StatusText = $"Uninstalling {entry.ProjectTitle}...";

        try
        {
            var compat = new EngineCompatibility(Instance.EngineType.ToString());
            string? path = AddonManifestService.ResolveAddonFilePath(Instance.Path, compat.PrimaryAddonSubDir, entry.FileName);
            if (path != null && File.Exists(path))
            {
                File.Delete(path);
            }

            await _manifestService.UnregisterAsync(Instance.Path, entry.Provider, entry.ProjectId);
            StatusText = $"Successfully uninstalled {entry.ProjectTitle}.";

            await RefreshInstalledListAsync();

            var matching = SearchResults.FirstOrDefault(s => string.Equals(s.Hit.ProjectId, entry.ProjectId, StringComparison.OrdinalIgnoreCase));
            if (matching != null)
            {
                matching.IsInstalled = false;
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

    private static string MapSortKey(string displaySort) => displaySort switch
    {
        "Top Downloads" => "downloads",
        "Newest" => "newest",
        _ => "relevance"
    };

    private static string SanitizeArchiveFileName(string fileName)
    {
        string leaf = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(leaf) || leaf.Contains(".."))
        {
            throw new InvalidOperationException($"Unsafe archive file name '{fileName}'.");
        }
        return leaf;
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
