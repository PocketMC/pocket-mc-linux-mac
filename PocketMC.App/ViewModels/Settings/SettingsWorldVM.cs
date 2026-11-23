using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PocketMC.App.ViewModels.Settings
{
    public partial class SettingsWorldVM : ObservableObject
    {
        private string _serverDir = string.Empty;
        private readonly Action _markDirty;

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

        public List<string> Difficulties { get; } = new() { "peaceful", "easy", "normal", "hard" };
        public List<string> Gamemodes { get; } = new() { "survival", "creative", "adventure", "spectator" };
        public List<string> LevelTypes { get; } = new() { "default", "flat", "large_biomes", "amplified" };

        public SettingsWorldVM(Action markDirty)
        {
            _markDirty = markDirty;
        }

        public void UpdateServerDir(string newDir)
        {
            _serverDir = newDir;
        }

        partial void OnSeedChanged(string value) => _markDirty();
        partial void OnLevelNameChanged(string value) => _markDirty();
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
