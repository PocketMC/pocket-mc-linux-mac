using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.App.ViewModels
{
    public partial class ServerSettingsViewModel : ObservableObject
    {
        private readonly IInstanceService _instanceService;
        private readonly ISettingsService _settingsService;

        private Dictionary<string, string> _originalProps = new();
        private string _originalJvmArgs = string.Empty;

        [ObservableProperty]
        private ServerInstance? _selectedInstance;

        // General settings
        [ObservableProperty] private string _motd = string.Empty;
        [ObservableProperty] private int _serverPort = 25565;
        [ObservableProperty] private int _maxPlayers = 20;
        [ObservableProperty] private bool _onlineMode = true;
        [ObservableProperty] private bool _whiteList = false;
        [ObservableProperty] private string _serverIp = string.Empty;

        // World settings
        [ObservableProperty] private string _levelName = "world";
        [ObservableProperty] private string _difficulty = "easy";
        [ObservableProperty] private string _gamemode = "survival";
        [ObservableProperty] private bool _forceGamemode = false;
        [ObservableProperty] private bool _hardcore = false;
        [ObservableProperty] private string _levelSeed = string.Empty;
        [ObservableProperty] private string _levelType = "default";
        [ObservableProperty] private int _spawnProtection = 16;
        [ObservableProperty] private bool _generateStructures = true;
        [ObservableProperty] private bool _spawnMonsters = true;
        [ObservableProperty] private bool _spawnAnimals = true;
        [ObservableProperty] private bool _spawnNpcs = true;

        // Performance settings
        [ObservableProperty] private int _maxRamGb = 2;
        [ObservableProperty] private int _viewDistance = 10;
        [ObservableProperty] private int _simulationDistance = 10;
        [ObservableProperty] private int _entityBroadcastRange = 100;
        [ObservableProperty] private int _networkCompressionThreshold = 256;
        [ObservableProperty] private int _maxTickTime = 60000;

        // Advanced settings
        [ObservableProperty] private bool _pvp = true;
        [ObservableProperty] private bool _allowFlight = false;
        [ObservableProperty] private bool _enableCommandBlock = true;
        [ObservableProperty] private bool _enableQuery = false;
        [ObservableProperty] private int _queryPort = 25565;
        [ObservableProperty] private bool _enableRcon = false;
        [ObservableProperty] private string _rconPassword = string.Empty;
        [ObservableProperty] private int _rconPort = 25575;
        [ObservableProperty] private string _resourcePack = string.Empty;

        // Raw Properties
        [ObservableProperty] private string _rawPropertiesText = string.Empty;

        [ObservableProperty] private bool _hasUnsavedChanges;

        public List<string> Difficulties { get; } = new() { "peaceful", "easy", "normal", "hard" };
        public List<string> Gamemodes { get; } = new() { "survival", "creative", "adventure", "spectator" };
        public List<string> LevelTypes { get; } = new() { "default", "flat", "large_biomes", "amplified" };

        public IAsyncRelayCommand SaveCommand { get; }
        public IRelayCommand CancelCommand { get; }
        public IRelayCommand GoBackCommand { get; }

        public ServerSettingsViewModel(
            IInstanceService instanceService,
            ISettingsService settingsService)
        {
            _instanceService = instanceService;
            _settingsService = settingsService;

            SaveCommand = new AsyncRelayCommand(SaveSettingsAsync);
            CancelCommand = new RelayCommand(CancelEdits);
            GoBackCommand = new RelayCommand(GoBack);
        }

        public void Initialize(ServerInstance instance)
        {
            SelectedInstance = instance;
            LoadSettings();
        }

        private void LoadSettings()
        {
            if (SelectedInstance == null) return;

            _originalProps.Clear();
            _originalJvmArgs = SelectedInstance.JvmArgs ?? string.Empty;

            // Load from server.properties
            string propPath = Path.Combine(SelectedInstance.Path, "server.properties");
            if (File.Exists(propPath))
            {
                var lines = File.ReadAllLines(propPath);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("#") || !trimmed.Contains("=")) continue;
                    var idx = trimmed.IndexOf('=');
                    var key = trimmed.Substring(0, idx).Trim();
                    var val = trimmed.Substring(idx + 1).Trim();
                    _originalProps[key] = val;
                }
                RawPropertiesText = File.ReadAllText(propPath);
            }
            else
            {
                RawPropertiesText = "# PocketMC server properties" + Environment.NewLine;
            }

            // General
            Motd = GetPropValue("motd", "A Minecraft Server");
            ServerPort = GetPropInt("server-port", (SelectedInstance.EngineType == EngineType.PocketMine || SelectedInstance.EngineType == EngineType.Bedrock) ? 19132 : 25565);
            MaxPlayers = GetPropInt("max-players", 20);
            OnlineMode = GetPropBool("online-mode", true);
            WhiteList = GetPropBool("white-list", false) || GetPropBool("enforce-whitelist", false);
            ServerIp = GetPropValue("server-ip", "");

            // World
            LevelName = GetPropValue("level-name", "world");
            Difficulty = GetPropValue("difficulty", "easy");
            Gamemode = GetPropValue("gamemode", "survival");
            ForceGamemode = GetPropBool("force-gamemode", false);
            Hardcore = GetPropBool("hardcore", false);
            LevelSeed = GetPropValue("level-seed", "");
            LevelType = GetPropValue("level-type", "default");
            SpawnProtection = GetPropInt("spawn-protection", 16);
            GenerateStructures = GetPropBool("generate-structures", true);
            SpawnMonsters = GetPropBool("spawn-monsters", true);
            SpawnAnimals = GetPropBool("spawn-animals", true);
            SpawnNpcs = GetPropBool("spawn-npcs", true);

            // Performance
            MaxRamGb = ParseMaxRam(_originalJvmArgs);
            ViewDistance = GetPropInt("view-distance", 10);
            SimulationDistance = GetPropInt("simulation-distance", 10);
            EntityBroadcastRange = GetPropInt("entity-broadcast-range-percentage", 100);
            NetworkCompressionThreshold = GetPropInt("network-compression-threshold", 256);
            MaxTickTime = GetPropInt("max-tick-time", 60000);

            // Advanced
            Pvp = GetPropBool("pvp", true);
            AllowFlight = GetPropBool("allow-flight", false);
            EnableCommandBlock = GetPropBool("enable-command-block", true);
            EnableQuery = GetPropBool("enable-query", false);
            QueryPort = GetPropInt("query.port", 25565);
            EnableRcon = GetPropBool("enable-rcon", false);
            RconPassword = GetPropValue("rcon.password", "");
            RconPort = GetPropInt("rcon.port", 25575);
            ResourcePack = GetPropValue("resource-pack", "");

            HasUnsavedChanges = false;
        }

        private string GetPropValue(string key, string defaultValue)
        {
            return _originalProps.TryGetValue(key, out var val) ? val : defaultValue;
        }

        private int GetPropInt(string key, int defaultValue)
        {
            if (_originalProps.TryGetValue(key, out var val) && int.TryParse(val, out var res))
                return res;
            return defaultValue;
        }

        private bool GetPropBool(string key, bool defaultValue)
        {
            if (_originalProps.TryGetValue(key, out var val))
            {
                if (bool.TryParse(val, out var res)) return res;
                if (val.Equals("true", StringComparison.OrdinalIgnoreCase) || val == "1") return true;
                if (val.Equals("false", StringComparison.OrdinalIgnoreCase) || val == "0") return false;
            }
            return defaultValue;
        }

        private int ParseMaxRam(string jvmArgs)
        {
            if (string.IsNullOrEmpty(jvmArgs)) return 2;
            var match = Regex.Match(jvmArgs, @"-Xmx(\d+)([GM])", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                int val = int.Parse(match.Groups[1].Value);
                string unit = match.Groups[2].Value.ToUpper();
                if (unit == "M") return val / 1024;
                return val;
            }
            return 2;
        }

        private string SetMaxRam(string jvmArgs, int ramGb)
        {
            var newArg = $"-Xmx{ramGb}G";
            if (string.IsNullOrEmpty(jvmArgs)) return newArg;
            if (jvmArgs.Contains("-Xmx"))
            {
                return Regex.Replace(jvmArgs, @"-Xmx\d+[GM]", newArg, RegexOptions.IgnoreCase);
            }
            return jvmArgs + " " + newArg;
        }

        private async Task SaveSettingsAsync()
        {
            if (SelectedInstance == null) return;

            string propPath = Path.Combine(SelectedInstance.Path, "server.properties");
            var updatedProps = new Dictionary<string, string>(_originalProps);

            // General
            updatedProps["motd"] = Motd;
            updatedProps["server-port"] = ServerPort.ToString();
            updatedProps["max-players"] = MaxPlayers.ToString();
            updatedProps["online-mode"] = OnlineMode.ToString().ToLowerInvariant();
            updatedProps["white-list"] = WhiteList.ToString().ToLowerInvariant();
            updatedProps["enforce-whitelist"] = WhiteList.ToString().ToLowerInvariant();
            updatedProps["server-ip"] = ServerIp;

            // World
            updatedProps["level-name"] = LevelName;
            updatedProps["difficulty"] = Difficulty;
            updatedProps["gamemode"] = Gamemode;
            updatedProps["force-gamemode"] = ForceGamemode.ToString().ToLowerInvariant();
            updatedProps["hardcore"] = Hardcore.ToString().ToLowerInvariant();
            updatedProps["level-seed"] = LevelSeed;
            updatedProps["level-type"] = LevelType;
            updatedProps["spawn-protection"] = SpawnProtection.ToString();
            updatedProps["generate-structures"] = GenerateStructures.ToString().ToLowerInvariant();
            updatedProps["spawn-monsters"] = SpawnMonsters.ToString().ToLowerInvariant();
            updatedProps["spawn-animals"] = SpawnAnimals.ToString().ToLowerInvariant();
            updatedProps["spawn-npcs"] = SpawnNpcs.ToString().ToLowerInvariant();

            // Performance
            updatedProps["view-distance"] = ViewDistance.ToString();
            updatedProps["simulation-distance"] = SimulationDistance.ToString();
            updatedProps["entity-broadcast-range-percentage"] = EntityBroadcastRange.ToString();
            updatedProps["network-compression-threshold"] = NetworkCompressionThreshold.ToString();
            updatedProps["max-tick-time"] = MaxTickTime.ToString();

            // Advanced
            updatedProps["pvp"] = Pvp.ToString().ToLowerInvariant();
            updatedProps["allow-flight"] = AllowFlight.ToString().ToLowerInvariant();
            updatedProps["enable-command-block"] = EnableCommandBlock.ToString().ToLowerInvariant();
            updatedProps["enable-query"] = EnableQuery.ToString().ToLowerInvariant();
            updatedProps["query.port"] = QueryPort.ToString();
            updatedProps["enable-rcon"] = EnableRcon.ToString().ToLowerInvariant();
            if (!string.IsNullOrEmpty(RconPassword)) updatedProps["rcon.password"] = RconPassword;
            updatedProps["rcon.port"] = RconPort.ToString();
            if (!string.IsNullOrWhiteSpace(ResourcePack)) updatedProps["resource-pack"] = ResourcePack;

            // Write back to server.properties
            var lines = new List<string>();
            lines.Add("#PocketMC server properties");
            lines.Add($"#Saved at {DateTime.UtcNow:o}");
            foreach (var kvp in updatedProps)
            {
                lines.Add($"{kvp.Key}={kvp.Value}");
            }

            await File.WriteAllLinesAsync(propPath, lines);

            // Update raw properties text
            RawPropertiesText = string.Join(Environment.NewLine, lines);

            // Save RAM changes to JVM args in metadata
            SelectedInstance.JvmArgs = SetMaxRam(_originalJvmArgs, MaxRamGb);

            // Write local instance.json metadata file
            var metaPath = Path.Combine(SelectedInstance.Path, "instance.json");
            var json = JsonSerializer.Serialize(SelectedInstance, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(metaPath, json);

            // Refresh original copies
            _originalProps = updatedProps;
            _originalJvmArgs = SelectedInstance.JvmArgs;

            HasUnsavedChanges = false;
        }

        private void CancelEdits()
        {
            LoadSettings();
        }

        private void GoBack()
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

        protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (e.PropertyName != nameof(HasUnsavedChanges) && 
                e.PropertyName != nameof(SelectedInstance))
            {
                HasUnsavedChanges = true;
            }
        }
    }
}
