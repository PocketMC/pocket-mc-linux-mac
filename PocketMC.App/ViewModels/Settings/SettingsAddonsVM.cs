using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.App.Navigation;
using PocketMC.App.Services;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Mods;
using PocketMC.Infrastructure.Services;

namespace PocketMC.App.ViewModels.Settings
{
    public partial class AddonItemViewModel : ObservableObject
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string LoaderType { get; set; } = "Unknown";
        public string SourceLabel { get; set; } = "Local File";
        public string? Provider { get; set; }
        public string? ProjectId { get; set; }
        public string? VersionId { get; set; }
        public long SizeBytes { get; set; }
        public DateTime LastModifiedUtc { get; set; }
        public bool IsPlugin { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ToggleActionLabel))]
        [NotifyPropertyChangedFor(nameof(IsDisabled))]
        [NotifyPropertyChangedFor(nameof(StatusLabel))]
        private bool _isEnabled = true;

        [ObservableProperty]
        private Bitmap? _icon;

        [ObservableProperty]
        private bool _isUpdating;

        [ObservableProperty]
        private bool _hasUpdate;

        [ObservableProperty]
        private string? _updateAvailableVersion;

        public bool IsDisabled => !IsEnabled;
        public string ToggleActionLabel => IsEnabled ? "Disable" : "Enable";
        public string StatusLabel => IsEnabled ? LoaderType : $"{LoaderType} (Disabled)";
        public string FormattedSize => SizeBytes >= 1024 * 1024
            ? $"{SizeBytes / (1024.0 * 1024.0):F1} MB"
            : $"{Math.Max(1, SizeBytes / 1024.0):F0} KB";
        public string MetadataSummary => string.IsNullOrWhiteSpace(Version)
            ? $"{LoaderType} | {SourceLabel} | {FormattedSize}"
            : $"v{Version} | {LoaderType} | {SourceLabel} | {FormattedSize}";
    }

    public partial class BedrockPackItemViewModel : ObservableObject
    {
        public string Uuid { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public BedrockPackType PackType { get; set; }
        public string DirectoryPath { get; set; } = string.Empty;
        public string? IconPath { get; set; }
        public double SizeKb { get; set; }
        public Bitmap? Icon { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ToggleActionLabel))]
        private bool _isEnabled;

        public string ToggleActionLabel => IsEnabled ? "Disable" : "Enable";
        public string PackTypeLabel => PackType == BedrockPackType.Behavior ? "Behavior Pack" : "Resource Pack";
        public string FormattedSize => SizeKb >= 1024.0 ? $"{SizeKb / 1024.0:F1} MB" : $"{SizeKb:F0} KB";
    }

    public partial class SettingsAddonsVM : ObservableObject
    {
        private ServerInstance? _instance;
        private string _serverDir = string.Empty;
        private readonly IDialogService _dialogService;
        private readonly Action _markDirty;
        private readonly AddonManifestService _manifestService;
        private readonly AddonUpdateService? _updateService;
        private readonly BedrockAddonInstaller _bedrockInstaller;
        private readonly Func<bool>? _isRunningCheck;

        private readonly List<AddonItemViewModel> _allPlugins = new();
        private readonly List<AddonItemViewModel> _allMods = new();
        private readonly List<BedrockPackItemViewModel> _allBehaviorPacks = new();
        private readonly List<BedrockPackItemViewModel> _allResourcePacks = new();

        [ObservableProperty] private bool _autoUpdateAddons = true;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _selectedSortOption = "Name";
        [ObservableProperty] private AddonItemViewModel? _selectedAddon;
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private bool _isUpdatingAll;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsResourcePacksTabSelected))]
        private bool _isBehaviorPacksTabSelected = true;

        public bool IsResourcePacksTabSelected
        {
            get => !IsBehaviorPacksTabSelected;
            set => IsBehaviorPacksTabSelected = !value;
        }

        public List<string> SortOptions { get; } = new() { "Name", "Last Modified", "Size", "Loader Type", "Source" };

        public ObservableCollection<AddonItemViewModel> Plugins { get; } = new();
        public ObservableCollection<AddonItemViewModel> Mods { get; } = new();
        public ObservableCollection<BedrockPackItemViewModel> BehaviorPacks { get; } = new();
        public ObservableCollection<BedrockPackItemViewModel> ResourcePacks { get; } = new();

        public bool SupportsPlugins => _instance == null ||
            _instance.EngineType is EngineType.Paper or EngineType.PocketMine;

        public bool SupportsMods => _instance != null &&
            _instance.EngineType is EngineType.Fabric or EngineType.Forge or EngineType.NeoForge;

        public bool SupportsBedrockAddons => _instance != null && _instance.EngineType == EngineType.Bedrock;

        public bool SupportsModrinth => _instance != null &&
            _instance.EngineType is EngineType.Paper or EngineType.Fabric or EngineType.Forge or EngineType.NeoForge;

        public bool HasSupportedAddonTypes => SupportsPlugins || SupportsMods || SupportsBedrockAddons;

        public string SearchPlaceholder => SupportsBedrockAddons
            ? "Filter installed Behavior or Resource packs..."
            : SupportsMods
                ? "Filter installed mods..."
                : "Filter installed plugins...";

        public IRelayCommand RefreshCommand { get; }
        public IRelayCommand<object?> OpenFolderCommand { get; }
        public IAsyncRelayCommand<AddonItemViewModel> ToggleAddonCommand { get; }
        public IAsyncRelayCommand<AddonItemViewModel> ToggleModActiveCommand { get; }
        public IAsyncRelayCommand<AddonItemViewModel> DeleteAddonCommand { get; }
        public IAsyncRelayCommand<AddonItemViewModel> UpdatePluginCommand { get; }
        public IAsyncRelayCommand<AddonItemViewModel> UpdateModCommand { get; }
        public IAsyncRelayCommand UpdateAllPluginsCommand { get; }
        public IAsyncRelayCommand UpdateAllModsCommand { get; }
        public IAsyncRelayCommand AddPluginCommand { get; }
        public IAsyncRelayCommand AddModCommand { get; }
        public IRelayCommand BrowseModrinthPluginsCommand { get; }
        public IRelayCommand BrowseModrinthModsCommand { get; }
        public IAsyncRelayCommand ImportBedrockAddonCommand { get; }
        public IAsyncRelayCommand<BedrockPackItemViewModel> ToggleBedrockPackCommand { get; }
        public IAsyncRelayCommand<BedrockPackItemViewModel> DeleteBedrockPackCommand { get; }

        public SettingsAddonsVM(
            IDialogService dialogService,
            Action markDirty,
            AddonManifestService? manifestService = null,
            AddonUpdateService? updateService = null,
            BedrockAddonInstaller? bedrockInstaller = null,
            Func<bool>? isRunningCheck = null)
        {
            _dialogService = dialogService;
            _markDirty = markDirty;
            _manifestService = manifestService ?? new AddonManifestService();
            _updateService = updateService ?? (App.Services?.GetService(typeof(AddonUpdateService)) as AddonUpdateService);
            _bedrockInstaller = bedrockInstaller ?? new BedrockAddonInstaller();
            _isRunningCheck = isRunningCheck;

            RefreshCommand = new RelayCommand(LoadAddons);
            OpenFolderCommand = new RelayCommand<object?>(OpenFolder);
            ToggleAddonCommand = new AsyncRelayCommand<AddonItemViewModel>(ToggleAddonAsync);
            ToggleModActiveCommand = new AsyncRelayCommand<AddonItemViewModel>(ToggleAddonAsync);
            DeleteAddonCommand = new AsyncRelayCommand<AddonItemViewModel>(DeleteAddonAsync);
            UpdatePluginCommand = new AsyncRelayCommand<AddonItemViewModel>(CheckOrUpdateSingleAsync);
            UpdateModCommand = new AsyncRelayCommand<AddonItemViewModel>(CheckOrUpdateSingleAsync);
            UpdateAllPluginsCommand = new AsyncRelayCommand(() => UpdateAllAsync(isPlugins: true));
            UpdateAllModsCommand = new AsyncRelayCommand(() => UpdateAllAsync(isPlugins: false));
            AddPluginCommand = new AsyncRelayCommand(() => AddLocalJarAsync(isPlugin: true));
            AddModCommand = new AsyncRelayCommand(() => AddLocalJarAsync(isPlugin: false));
            BrowseModrinthPluginsCommand = new RelayCommand(OpenMarketplace);
            BrowseModrinthModsCommand = new RelayCommand(OpenMarketplace);
            ImportBedrockAddonCommand = new AsyncRelayCommand(ImportBedrockAddonAsync);
            ToggleBedrockPackCommand = new AsyncRelayCommand<BedrockPackItemViewModel>(ToggleBedrockPackAsync);
            DeleteBedrockPackCommand = new AsyncRelayCommand<BedrockPackItemViewModel>(DeleteBedrockPackAsync);
        }

        public void SetInstance(ServerInstance instance)
        {
            _instance = instance;
            _serverDir = instance.Path;
            RaiseCapabilityProperties();
            LoadAddons();
        }

        public void UpdateServerDir(string newDir)
        {
            _serverDir = newDir;
            RaiseCapabilityProperties();
            LoadAddons();
        }

        private void RaiseCapabilityProperties()
        {
            OnPropertyChanged(nameof(SupportsPlugins));
            OnPropertyChanged(nameof(SupportsMods));
            OnPropertyChanged(nameof(SupportsBedrockAddons));
            OnPropertyChanged(nameof(SupportsModrinth));
            OnPropertyChanged(nameof(HasSupportedAddonTypes));
            OnPropertyChanged(nameof(SearchPlaceholder));
        }

        public void LoadAddons()
        {
            _allPlugins.Clear();
            _allMods.Clear();
            _allBehaviorPacks.Clear();
            _allResourcePacks.Clear();

            if (string.IsNullOrEmpty(_serverDir) || !Directory.Exists(_serverDir))
            {
                ApplyFiltersAndSort();
                return;
            }

            IsLoading = true;
            try
            {
                AddonManifest manifest = _manifestService.LoadManifest(_serverDir);

                LoadJavaFolder(Path.Combine(_serverDir, "plugins"), isPlugin: true, manifest, _allPlugins);
                LoadJavaFolder(Path.Combine(_serverDir, "mods"), isPlugin: false, manifest, _allMods);

                if (SupportsBedrockAddons)
                {
                    foreach (BedrockPackInfo pack in _bedrockInstaller.GetPacks(_serverDir))
                    {
                        var vm = new BedrockPackItemViewModel
                        {
                            Uuid = pack.Uuid,
                            Name = pack.Name,
                            Description = pack.Description,
                            Version = pack.Version,
                            PackType = pack.PackType,
                            DirectoryPath = pack.DirectoryPath,
                            IconPath = pack.IconPath,
                            SizeKb = pack.SizeKb,
                            IsEnabled = pack.IsEnabled,
                            Icon = LoadBitmapSafe(pack.IconPath)
                        };

                        if (pack.PackType == BedrockPackType.Behavior)
                        {
                            _allBehaviorPacks.Add(vm);
                        }
                        else
                        {
                            _allResourcePacks.Add(vm);
                        }
                    }
                }

                ApplyFiltersAndSort();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void LoadJavaFolder(string folder, bool isPlugin, AddonManifest manifest, List<AddonItemViewModel> target)
        {
            if (!Directory.Exists(folder))
            {
                return;
            }

            string? expectedLoader = isPlugin
                ? "Paper"
                : _instance?.EngineType.ToString();

            var files = Directory.GetFiles(folder, "*.*")
                .Where(f => f.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase) ||
                            (isPlugin && _instance?.EngineType == EngineType.PocketMine &&
                             (f.EndsWith(".phar", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".phar.disabled", StringComparison.OrdinalIgnoreCase))));

            foreach (string file in files)
            {
                var fi = new FileInfo(file);
                bool enabled = !file.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
                string cleanFile = fi.Name.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
                    ? fi.Name.Substring(0, fi.Name.Length - ".disabled".Length)
                    : fi.Name;

                JavaModMetadata meta = file.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase)
                    ? JavaModMetadataService.ScanJar(file, expectedLoader)
                    : new JavaModMetadata
                    {
                        DisplayName = Path.GetFileNameWithoutExtension(cleanFile),
                        FileName = fi.Name,
                        LoaderType = "PocketMine"
                    };

                AddonManifestEntry? manifestEntry = manifest.Entries.FirstOrDefault(e =>
                    e.FileName.Equals(cleanFile, StringComparison.OrdinalIgnoreCase) ||
                    e.FileName.Equals(fi.Name, StringComparison.OrdinalIgnoreCase));

                string sourceLabel = manifestEntry != null && !string.IsNullOrWhiteSpace(manifestEntry.Provider)
                    ? manifestEntry.Provider
                    : "Local File";

                target.Add(new AddonItemViewModel
                {
                    FileName = fi.Name,
                    FilePath = fi.FullName,
                    DisplayName = !string.IsNullOrWhiteSpace(meta.DisplayName) ? meta.DisplayName : cleanFile,
                    Version = meta.Version ?? string.Empty,
                    Description = meta.Description ?? string.Empty,
                    LoaderType = meta.LoaderType == "Unknown" ? (isPlugin ? "Plugin" : "Mod") : meta.LoaderType,
                    SourceLabel = sourceLabel,
                    Provider = manifestEntry?.Provider,
                    ProjectId = manifestEntry?.ProjectId,
                    VersionId = manifestEntry?.VersionId,
                    SizeBytes = fi.Length,
                    LastModifiedUtc = fi.LastWriteTimeUtc,
                    IsEnabled = enabled,
                    IsPlugin = isPlugin,
                    Icon = AddonIconService.GetIconFromBytes(meta.IconBytes)
                });
            }
        }

        private void ApplyFiltersAndSort()
        {
            ApplyList(_allPlugins, Plugins);
            ApplyList(_allMods, Mods);

            BehaviorPacks.Clear();
            foreach (BedrockPackItemViewModel pack in _allBehaviorPacks.Where(p =>
                string.IsNullOrWhiteSpace(SearchText) ||
                p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)))
            {
                BehaviorPacks.Add(pack);
            }

            ResourcePacks.Clear();
            foreach (BedrockPackItemViewModel pack in _allResourcePacks.Where(p =>
                string.IsNullOrWhiteSpace(SearchText) ||
                p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)))
            {
                ResourcePacks.Add(pack);
            }
        }

        private void ApplyList(List<AddonItemViewModel> source, ObservableCollection<AddonItemViewModel> dest)
        {
            dest.Clear();
            IEnumerable<AddonItemViewModel> filtered = source;
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = filtered.Where(item =>
                    item.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    item.FileName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            filtered = SelectedSortOption switch
            {
                "Last Modified" => filtered.OrderByDescending(i => i.LastModifiedUtc),
                "Size" => filtered.OrderByDescending(i => i.SizeBytes),
                "Loader Type" => filtered.OrderBy(i => i.LoaderType, StringComparer.OrdinalIgnoreCase).ThenBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase),
                "Source" => filtered.OrderBy(i => i.SourceLabel, StringComparer.OrdinalIgnoreCase).ThenBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase),
                _ => filtered.OrderBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase)
            };

            foreach (AddonItemViewModel item in filtered)
            {
                dest.Add(item);
            }
        }

        private async Task ToggleAddonAsync(AddonItemViewModel? item)
        {
            if (item == null || !File.Exists(item.FilePath)) return;

            if (_isRunningCheck?.Invoke() == true)
            {
                _dialogService.ShowMessage("Server Running", "Stop the server before enabling or disabling mods or plugins.", DialogType.Warning);
                return;
            }

            try
            {
                string newPath = item.IsEnabled
                    ? item.FilePath + ".disabled"
                    : item.FilePath.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
                        ? item.FilePath.Substring(0, item.FilePath.Length - ".disabled".Length)
                        : item.FilePath;

                File.Move(item.FilePath, newPath, true);
                item.FilePath = newPath;
                item.FileName = Path.GetFileName(newPath);
                item.IsEnabled = !item.IsEnabled;
                _markDirty();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage("Failed to toggle addon", ex.Message, DialogType.Error);
            }
        }

        private async Task DeleteAddonAsync(AddonItemViewModel? item)
        {
            if (item == null) return;

            var confirm = await _dialogService.ShowDialogAsync(
                "Delete Addon",
                $"Are you sure you want to delete '{item.DisplayName}'?",
                DialogType.Question,
                showCancel: true);

            if (confirm != DialogResult.Ok && confirm != DialogResult.Yes) return;

            try
            {
                if (File.Exists(item.FilePath))
                {
                    File.Delete(item.FilePath);
                }
                if (!string.IsNullOrWhiteSpace(_serverDir))
                {
                    await _manifestService.UnregisterByFileNameAsync(_serverDir, item.FileName);
                }
                LoadAddons();
                _markDirty();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage("Delete Failed", ex.Message, DialogType.Error);
            }
        }

        private async Task AddLocalJarAsync(bool isPlugin)
        {
            if (string.IsNullOrWhiteSpace(_serverDir)) return;

            string filter = isPlugin ? "Plugin Archives (*.jar;*.phar)|*.jar;*.phar" : "Mod Archives (*.jar)|*.jar";
            string? selected = await _dialogService.OpenFileDialogAsync(isPlugin ? "Select Plugin File" : "Select Mod File", filter);
            if (string.IsNullOrWhiteSpace(selected) || !File.Exists(selected)) return;

            string destFolder = Path.Combine(_serverDir, isPlugin ? "plugins" : "mods");
            Directory.CreateDirectory(destFolder);
            string destPath = Path.Combine(destFolder, Path.GetFileName(selected));
            File.Copy(selected, destPath, overwrite: true);
            LoadAddons();
            _markDirty();
        }

        private async Task ImportBedrockAddonAsync()
        {
            if (string.IsNullOrWhiteSpace(_serverDir)) return;

            string? path = await _dialogService.OpenFileDialogAsync(
                "Import Bedrock Add-on",
                "Bedrock Add-ons (*.mcpack;*.mcaddon;*.zip)|*.mcpack;*.mcaddon;*.zip");
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

            try
            {
                var packs = await _bedrockInstaller.InstallAddonAsync(path, _serverDir);
                LoadAddons();
                _dialogService.ShowMessage("Add-on Imported", $"Installed {packs.Count} Bedrock pack(s) cleanly.", DialogType.Information);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage("Import Failed", ex.Message, DialogType.Error);
            }
        }

        private async Task ToggleBedrockPackAsync(BedrockPackItemViewModel? pack)
        {
            if (pack == null || string.IsNullOrWhiteSpace(_serverDir)) return;

            try
            {
                bool targetState = !pack.IsEnabled;
                await _bedrockInstaller.SetPackEnabledAsync(_serverDir, pack.Uuid, pack.PackType, targetState);
                pack.IsEnabled = targetState;
                _markDirty();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage("Toggle Failed", ex.Message, DialogType.Error);
            }
        }

        private async Task DeleteBedrockPackAsync(BedrockPackItemViewModel? pack)
        {
            if (pack == null || string.IsNullOrWhiteSpace(_serverDir)) return;

            var confirm = await _dialogService.ShowDialogAsync(
                "Delete Bedrock Pack",
                $"Remove '{pack.Name}' from this server?",
                DialogType.Question,
                showCancel: true);

            if (confirm != DialogResult.Ok && confirm != DialogResult.Yes) return;

            await _bedrockInstaller.DeletePackAsync(_serverDir, pack.Uuid, pack.PackType);
            LoadAddons();
        }

        private async Task CheckOrUpdateSingleAsync(AddonItemViewModel? item)
        {
            if (item == null || _updateService == null || _instance == null) return;

            item.IsUpdating = true;
            try
            {
                var compat = new EngineCompatibility(_instance.EngineType.ToString());
                string loader = compat.LoaderName;
                var result = await _updateService.CheckForUpdateAsync(
                    _serverDir,
                    item.FileName,
                    _instance.EngineVersion,
                    loader,
                    compat);

                if (!string.IsNullOrWhiteSpace(result.Error))
                {
                    _dialogService.ShowMessage("Update Check", result.Error, DialogType.Information);
                    return;
                }

                if (!result.IsUpdateAvailable)
                {
                    _dialogService.ShowMessage("Up to Date", $"{item.DisplayName} is already at the latest version.", DialogType.Information);
                    return;
                }

                var confirm = await _dialogService.ShowDialogAsync(
                    "Update Available",
                    $"Update '{item.DisplayName}' to {result.LatestVersionName}?",
                    DialogType.Question,
                    showCancel: true);

                if (confirm == DialogResult.Ok || confirm == DialogResult.Yes)
                {
                    await _updateService.ApplyUpdateAsync(
                        _serverDir,
                        item.FileName,
                        result,
                        item.Provider ?? "Modrinth",
                        item.ProjectId ?? string.Empty,
                        compat);
                    LoadAddons();
                }
            }
            finally
            {
                item.IsUpdating = false;
            }
        }

        private async Task UpdateAllAsync(bool isPlugins)
        {
            if (_updateService == null || _instance == null) return;

            IsUpdatingAll = true;
            int updated = 0;
            try
            {
                List<AddonItemViewModel> candidates = (isPlugins ? _allPlugins : _allMods)
                    .Where(a => a.IsEnabled && string.Equals(a.Provider, "Modrinth", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var compat = new EngineCompatibility(_instance.EngineType.ToString());
                string loader = compat.LoaderName;

                foreach (AddonItemViewModel item in candidates)
                {
                    var check = await _updateService.CheckForUpdateAsync(_serverDir, item.FileName, _instance.EngineVersion, loader, compat);
                    if (check.IsUpdateAvailable && string.IsNullOrWhiteSpace(check.Error))
                    {
                        await _updateService.ApplyUpdateAsync(
                            _serverDir,
                            item.FileName,
                            check,
                            item.Provider ?? "Modrinth",
                            item.ProjectId ?? string.Empty,
                            compat);
                        updated++;
                    }
                }

                LoadAddons();
                _dialogService.ShowMessage("Addon Update Check", updated > 0
                    ? $"Updated {updated} addon(s) successfully."
                    : "All marketplace-managed addons are up to date.", DialogType.Information);
            }
            finally
            {
                IsUpdatingAll = false;
            }
        }

        private void OpenMarketplace()
        {
            if (_instance == null) return;
            var nav = App.Services?.GetService(typeof(IAppNavigationService)) as IAppNavigationService;
            var marketVm = App.Services?.GetService(typeof(MarketplaceViewModel)) as MarketplaceViewModel;
            if (nav != null && marketVm != null)
            {
                marketVm.Initialize(_instance);
                nav.NavigateToDetailPage(
                    marketVm,
                    $"Marketplace: {_instance.Name}",
                    DetailRouteKind.PluginBrowser,
                    DetailBackNavigation.PreviousDetail);
            }
        }

        private void OpenFolder(object? param)
        {
            if (param is AddonItemViewModel item && File.Exists(item.FilePath))
            {
                string? dir = Path.GetDirectoryName(item.FilePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    OpenDirectory(dir);
                    return;
                }
            }

            if (string.IsNullOrEmpty(_serverDir)) return;

            string targetDir = Path.Combine(_serverDir, SupportsMods ? "mods" : "plugins");
            if (!Directory.Exists(targetDir))
            {
                targetDir = _serverDir;
            }
            OpenDirectory(targetDir);
        }

        private static void OpenDirectory(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch { }
        }

        private static Bitmap? LoadBitmapSafe(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            try
            {
                using var stream = File.OpenRead(path);
                return new Bitmap(stream);
            }
            catch
            {
                return null;
            }
        }

        partial void OnAutoUpdateAddonsChanged(bool value) => _markDirty();
        partial void OnSearchTextChanged(string value) => ApplyFiltersAndSort();
        partial void OnSelectedSortOptionChanged(string value) => ApplyFiltersAndSort();
    }
}
