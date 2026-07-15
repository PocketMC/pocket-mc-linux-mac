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

        // World settings
        [ObservableProperty] private string _levelName = "world";
        [ObservableProperty] private string _difficulty = "easy";
        [ObservableProperty] private string _gamemode = "survival";

        // Performance settings
        [ObservableProperty] private int _maxRamGb = 2;
        [ObservableProperty] private int _viewDistance = 10;

        // Advanced settings
        [ObservableProperty] private bool _pvp = true;
        [ObservableProperty] private bool _allowFlight = false;
        [ObservableProperty] private bool _enableQuery = false;

        [ObservableProperty] private bool _hasUnsavedChanges;

        public List<string> Difficulties { get; } = new() { "peaceful", "easy", "normal", "hard" };
        public List<string> Gamemodes { get; } = new() { "survival", "creative", "adventure", "spectator" };

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
            }

            // Bind values
            Motd = GetPropValue("motd", "A Minecraft Server");
            ServerPort = int.TryParse(GetPropValue("server-port", "25565"), out var port) ? port : 25565;
            MaxPlayers = int.TryParse(GetPropValue("max-players", "20"), out var players) ? players : 20;

            LevelName = GetPropValue("level-name", "world");
            Difficulty = GetPropValue("difficulty", "easy");
            Gamemode = GetPropValue("gamemode", "survival");

            ViewDistance = int.TryParse(GetPropValue("view-distance", "10"), out var vd) ? vd : 10;

            Pvp = GetPropValue("pvp", "true").Equals("true", StringComparison.OrdinalIgnoreCase);
            AllowFlight = GetPropValue("allow-flight", "false").Equals("true", StringComparison.OrdinalIgnoreCase);
            EnableQuery = GetPropValue("enable-query", "false").Equals("true", StringComparison.OrdinalIgnoreCase);

            MaxRamGb = ParseMaxRam(_originalJvmArgs);

            HasUnsavedChanges = false;
        }

        private string GetPropValue(string key, string defaultValue)
        {
            return _originalProps.TryGetValue(key, out var val) ? val : defaultValue;
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

            updatedProps["motd"] = Motd;
            updatedProps["server-port"] = ServerPort.ToString();
            updatedProps["max-players"] = MaxPlayers.ToString();
            updatedProps["level-name"] = LevelName;
            updatedProps["difficulty"] = Difficulty;
            updatedProps["gamemode"] = Gamemode;
            updatedProps["view-distance"] = ViewDistance.ToString();
            updatedProps["pvp"] = Pvp.ToString().ToLowerInvariant();
            updatedProps["allow-flight"] = AllowFlight.ToString().ToLowerInvariant();
            updatedProps["enable-query"] = EnableQuery.ToString().ToLowerInvariant();

            // Write back to server.properties
            var lines = new List<string>();
            lines.Add("#PocketMC server properties");
            lines.Add($"#Saved at {DateTime.UtcNow:o}");
            foreach (var kvp in updatedProps)
            {
                lines.Add($"{kvp.Key}={kvp.Value}");
            }

            await File.WriteAllLinesAsync(propPath, lines);

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
