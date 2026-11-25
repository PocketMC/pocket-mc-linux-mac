using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.App.Navigation;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;

namespace PocketMC.App.ViewModels.Settings
{
    public partial class SettingsWorldVM : ObservableObject
    {
        private ServerInstance? _instance;
        private string _serverDir = string.Empty;
        private readonly Action _markDirty;
        private readonly WorldManager _worldManager;
        private readonly IDialogService? _dialogService;
        private readonly Func<bool>? _isRunningCheck;

        [ObservableProperty] private string _seed = string.Empty;
        [ObservableProperty] private string _levelName = "world";
        [ObservableProperty] private string _levelType = "default";
        [ObservableProperty] private string _gamemode = "survival";
        [ObservableProperty] private string _difficulty = "easy";
        [ObservableProperty] private bool _pvp = true;
        [ObservableProperty] private bool _whiteList;
        [ObservableProperty] private bool _onlineMode = true;
        [ObservableProperty] private bool _allowCommandBlock = true;
        [ObservableProperty] private bool _allowFlight;
        [ObservableProperty] private bool _allowNether = true;
        [ObservableProperty] private int _spawnProtection = 16;
        [ObservableProperty] private int _maxPlayers = 20;
        [ObservableProperty] private int _viewDistance = 10;
        [ObservableProperty] private int _simulationDistance = 10;
        [ObservableProperty] private bool _generateStructures = true;
        [ObservableProperty] private bool _spawnMonsters = true;
        [ObservableProperty] private bool _spawnAnimals = true;
        [ObservableProperty] private bool _spawnNpcs = true;
        [ObservableProperty] private bool _forceGamemode;
        [ObservableProperty] private bool _hardcore;

        [ObservableProperty] private string _worldStatusText = "Checking world...";
        [ObservableProperty] private string _worldSizeText = string.Empty;

        public bool SupportsBedrockWorlds => _instance?.EngineType == EngineType.Bedrock;
        public string ImportWorldButtonText => SupportsBedrockWorlds ? "Import World (ZIP / .mcworld)" : "Import World (ZIP)";

        public List<string> Difficulties { get; } = new() { "peaceful", "easy", "normal", "hard" };
        public List<string> Gamemodes { get; } = new() { "survival", "creative", "adventure", "spectator" };
        public List<string> LevelTypes { get; } = new() { "default", "flat", "large_biomes", "amplified" };

        public IAsyncRelayCommand UploadWorldCommand { get; }
        public IRelayCommand BrowseMapsCommand { get; }
        public IAsyncRelayCommand DeleteWorldCommand { get; }

        public SettingsWorldVM(
            Action markDirty,
            WorldManager? worldManager = null,
            IDialogService? dialogService = null,
            Func<bool>? isRunningCheck = null)
        {
            _markDirty = markDirty;
            _worldManager = worldManager ?? new WorldManager();
            _dialogService = dialogService ?? (App.Services?.GetService(typeof(IDialogService)) as IDialogService);
            _isRunningCheck = isRunningCheck;

            UploadWorldCommand = new AsyncRelayCommand(UploadWorldAsync);
            BrowseMapsCommand = new RelayCommand(BrowseMaps);
            DeleteWorldCommand = new AsyncRelayCommand(DeleteWorldAsync);
        }

        public void SetInstance(ServerInstance instance)
        {
            _instance = instance;
            _serverDir = instance.Path;
            OnPropertyChanged(nameof(SupportsBedrockWorlds));
            OnPropertyChanged(nameof(ImportWorldButtonText));
            RefreshWorldStatus();
        }

        public void UpdateServerDir(string newDir)
        {
            _serverDir = newDir;
            RefreshWorldStatus();
        }

        public void RefreshWorldStatus()
        {
            if (string.IsNullOrWhiteSpace(_serverDir))
            {
                WorldStatusText = "No world directory";
                WorldSizeText = string.Empty;
                return;
            }

            string targetWorld = WorldManager.ResolveWorldDirectory(
                _serverDir,
                _instance?.EngineType ?? EngineType.VanillaJava,
                LevelName);
            var (_, status, size) = WorldManager.InspectWorld(targetWorld);
            WorldStatusText = status;
            WorldSizeText = size;
        }

        private async Task UploadWorldAsync()
        {
            if (_dialogService == null || string.IsNullOrWhiteSpace(_serverDir)) return;

            if (_isRunningCheck?.Invoke() == true)
            {
                _dialogService.ShowMessage("Server Running", "Stop the server before importing a world archive.", DialogType.Warning);
                return;
            }

            string? archive = await _dialogService.OpenFileDialogAsync(
                "Select World Archive",
                "Minecraft World Archives (*.zip;*.mcworld)|*.zip;*.mcworld");
            if (string.IsNullOrWhiteSpace(archive) || !File.Exists(archive)) return;

            string targetWorld = WorldManager.ResolveWorldDirectory(
                _serverDir,
                _instance?.EngineType ?? EngineType.VanillaJava,
                LevelName);

            try
            {
                await _dialogService.ShowProgressDialogAsync("Importing World", "Extracting archive and installing world...", async progress =>
                {
                    await _worldManager.ImportWorldZipAsync(archive, targetWorld, (pct, msg) =>
                    {
                        progress.Report(new ProgressDialogUpdate(pct < 0 ? 0 : pct, msg));
                    });
                });

                RefreshWorldStatus();
                _dialogService.ShowMessage("World Imported", "World installed cleanly.", DialogType.Information);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage("Import Failed", ex.Message, DialogType.Error);
            }
        }

        private async Task DeleteWorldAsync()
        {
            if (_dialogService == null || string.IsNullOrWhiteSpace(_serverDir)) return;

            if (_isRunningCheck?.Invoke() == true)
            {
                _dialogService.ShowMessage("Server Running", "Stop the server before wiping world data.", DialogType.Warning);
                return;
            }

            var confirm = await _dialogService.ShowDialogAsync(
                "Wipe World Data",
                "Are you sure you want to permanently delete this world folder? A fresh world will generate on next start.",
                DialogType.Warning,
                showCancel: true);

            if (confirm != DialogResult.Ok && confirm != DialogResult.Yes) return;

            string targetWorld = WorldManager.ResolveWorldDirectory(
                _serverDir,
                _instance?.EngineType ?? EngineType.VanillaJava,
                LevelName);
            await _worldManager.WipeWorldAsync(_serverDir, targetWorld);
            RefreshWorldStatus();
        }

        private void BrowseMaps()
        {
            if (_instance == null) return;
            var nav = App.Services?.GetService(typeof(IAppNavigationService)) as IAppNavigationService;
            var marketVm = App.Services?.GetService(typeof(MarketplaceViewModel)) as MarketplaceViewModel;
            if (nav != null && marketVm != null)
            {
                _ = marketVm.InitializeForMapsAsync(_instance);
                nav.NavigateToDetailPage(
                    new Views.Marketplace.MapBrowserPage { DataContext = marketVm },
                    $"Browse Maps: {_instance.Name}",
                    DetailRouteKind.MapBrowser,
                    DetailBackNavigation.PreviousDetail);
            }
        }

        partial void OnSeedChanged(string value) => _markDirty();
        partial void OnLevelNameChanged(string value)
        {
            _markDirty();
            RefreshWorldStatus();
        }
        partial void OnLevelTypeChanged(string value) => _markDirty();
        partial void OnGamemodeChanged(string value) => _markDirty();
        partial void OnDifficultyChanged(string value) => _markDirty();
        partial void OnPvpChanged(bool value) => _markDirty();
        partial void OnWhiteListChanged(bool value) => _markDirty();
        partial void OnOnlineModeChanged(bool value) => _markDirty();
        partial void OnAllowCommandBlockChanged(bool value) => _markDirty();
        partial void OnAllowFlightChanged(bool value) => _markDirty();
        partial void OnAllowNetherChanged(bool value) => _markDirty();
        partial void OnSpawnProtectionChanged(int value) => _markDirty();
        partial void OnMaxPlayersChanged(int value) => _markDirty();
        partial void OnViewDistanceChanged(int value) => _markDirty();
        partial void OnSimulationDistanceChanged(int value) => _markDirty();
        partial void OnGenerateStructuresChanged(bool value) => _markDirty();
        partial void OnSpawnMonstersChanged(bool value) => _markDirty();
        partial void OnSpawnAnimalsChanged(bool value) => _markDirty();
        partial void OnSpawnNpcsChanged(bool value) => _markDirty();
        partial void OnForceGamemodeChanged(bool value) => _markDirty();
        partial void OnHardcoreChanged(bool value) => _markDirty();
    }
}
