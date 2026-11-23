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
using PocketMC.Infrastructure.Services;
using PocketMC.App.Services;
using PocketMC.App.ViewModels.Settings;

namespace PocketMC.App.ViewModels
{
    public partial class ServerSettingsViewModel : ObservableObject
    {
        private readonly IInstanceService _instanceService;
        private readonly ISettingsService _settingsService;
        private readonly IInstanceUpdateService? _updateService;
        private readonly IGeyserDetector _geyserDetector;
        private readonly GeyserProvisioningService? _geyserProvisioningService;
        private readonly IRebootScheduleService? _rebootScheduleService;
        private readonly IDialogService _dialogService;
        private readonly IImageProcessingService _imageProcessingService;
        private readonly BackupService? _backupService;
        private readonly SummaryStorageService _summaryStorageService;

        private Dictionary<string, string> _originalProps = new();
        private string _originalJvmArgs = string.Empty;

        [ObservableProperty]
        private ServerInstance? _selectedInstance;

        [ObservableProperty]
        private bool _hasGeyser;

        [ObservableProperty]
        private int _geyserBedrockPort = 19132;

        [ObservableProperty]
        private bool _hasUnsavedChanges;

        [ObservableProperty]
        private bool _isRestartRequired;

        // 9 Modular Sub-ViewModels
        public SettingsGeneralVM General { get; }
        public SettingsWorldVM World { get; }
        public SettingsPerformanceVM Performance { get; }
        public SettingsBedrockVM Bedrock { get; }
        public SettingsBackupsVM Backups { get; }
        public SettingsAddonsVM Addons { get; }
        public SettingsVersionUpdatesVM VersionUpdates { get; }
        public SettingsSummariesVM Summaries { get; }
        public SettingsAdvancedVM Advanced { get; }

        public int SubViewModelCount => 9;

        // Backward compatibility convenience accessors
        public string Motd { get => General.Motd; set => General.Motd = value; }
        public int ServerPort { get => General.ServerPort; set => General.ServerPort = value; }
        public string Difficulty { get => World.Difficulty; set => World.Difficulty = value; }
        public int MaxRamGb { get => (int)(Performance.MaxRam / 1024.0); set => Performance.MaxRam = value * 1024.0; }

        public IAsyncRelayCommand SaveCommand { get; }
        public IRelayCommand CancelCommand { get; }
        public IRelayCommand GoBackCommand { get; }

        public ServerSettingsViewModel(
            IInstanceService instanceService,
            ISettingsService settingsService,
            IInstanceUpdateService? updateService = null,
            IGeyserDetector? geyserDetector = null,
            GeyserProvisioningService? geyserProvisioningService = null,
            IRebootScheduleService? rebootScheduleService = null,
            IDialogService? dialogService = null,
            IImageProcessingService? imageProcessingService = null,
            BackupService? backupService = null,
            SummaryStorageService? summaryStorageService = null)
        {
            _instanceService = instanceService;
            _settingsService = settingsService;
            _updateService = updateService;
            _geyserDetector = geyserDetector ?? new GeyserDetector(new AddonManifestService());
            _geyserProvisioningService = geyserProvisioningService;
            _rebootScheduleService = rebootScheduleService;
            _dialogService = dialogService ?? new AvaloniaDialogService();
            _imageProcessingService = imageProcessingService ?? new ImageProcessingService();
            _backupService = backupService;
            _summaryStorageService = summaryStorageService ?? new SummaryStorageService();

            General = new SettingsGeneralVM(string.Empty, _dialogService, _imageProcessingService, MarkDirty);
            World = new SettingsWorldVM(MarkDirty);
            Performance = new SettingsPerformanceVM(MarkDirty, _dialogService);
            Bedrock = new SettingsBedrockVM(MarkDirty);
            Backups = new SettingsBackupsVM(_dialogService, MarkDirty, _backupService);
            Addons = new SettingsAddonsVM(_dialogService, MarkDirty);
            VersionUpdates = new SettingsVersionUpdatesVM(_updateService);
            Summaries = new SettingsSummariesVM(string.Empty, _summaryStorageService, _dialogService);
            Advanced = new SettingsAdvancedVM(MarkDirty);

            SaveCommand = new AsyncRelayCommand(SaveSettingsAsync);
            CancelCommand = new RelayCommand(CancelEdits);
            GoBackCommand = new RelayCommand(GoBack);
        }

        private void MarkDirty()
        {
            HasUnsavedChanges = true;
        }

        public void Initialize(ServerInstance instance)
        {
            SelectedInstance = instance;
            LoadSettings();
        }

        partial void OnSelectedInstanceChanged(ServerInstance? value)
        {
            if (value != null)
            {
                General.UpdateServerDir(value.Path);
                General.InstanceName = value.Name;
                General.InstanceDescription = value.Description ?? string.Empty;
                General.ServerPort = value.ServerPort.HasValue && value.ServerPort.Value > 0 ? value.ServerPort.Value : 25565;

                World.UpdateServerDir(value.Path);
                Performance.MaxRam = value.MaxRamMb > 0 ? value.MaxRamMb : 2048;
                Performance.MinRam = value.MinRamMb > 0 ? value.MinRamMb : 1024;
                Performance.AdvancedJvmArgs = value.JvmArgs;

                Backups.SetInstance(value);
                Addons.UpdateServerDir(value.Path);
                VersionUpdates.SetInstance(value);
                Summaries.UpdateServerDir(value.Path);
                Advanced.UpdateServerDir(value.Path);

                LoadSettings();
            }
        }

        public void LoadSettings()
        {
            if (SelectedInstance == null) return;

            _originalProps.Clear();
            _originalJvmArgs = SelectedInstance.JvmArgs ?? string.Empty;

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
                Advanced.RawPropertiesText = File.ReadAllText(propPath);
            }
            else
            {
                Advanced.RawPropertiesText = "# PocketMC server properties" + Environment.NewLine;
            }

            // General
            HasGeyser = _geyserDetector.IsGeyserInstalled(SelectedInstance.Path);
            General.Motd = GetPropValue("motd", "A Minecraft Server");
            General.ServerPort = GetPropInt("server-port", (SelectedInstance.EngineType == EngineType.PocketMine || SelectedInstance.EngineType == EngineType.Bedrock) ? 19132 : 25565);
            General.ServerIp = GetPropValue("server-ip", "");
            General.LoadIcon();

            // World
            World.LevelName = GetPropValue("level-name", "world");
            World.Difficulty = GetPropValue("difficulty", "easy");
            World.Gamemode = GetPropValue("gamemode", "survival");
            World.ForceGamemode = GetPropBool("force-gamemode", false);
            World.Hardcore = GetPropBool("hardcore", false);
            World.Seed = GetPropValue("level-seed", "");
            World.LevelType = GetPropValue("level-type", "default");
            World.SpawnProtection = GetPropInt("spawn-protection", 16);
            World.MaxPlayers = GetPropInt("max-players", 20);
            World.OnlineMode = GetPropBool("online-mode", true);
            World.WhiteList = GetPropBool("white-list", false) || GetPropBool("enforce-whitelist", false);
            World.Pvp = GetPropBool("pvp", true);
            World.AllowFlight = GetPropBool("allow-flight", false);
            World.AllowCommandBlock = GetPropBool("enable-command-block", true);
            World.ViewDistance = GetPropInt("view-distance", 10);
            World.SimulationDistance = GetPropInt("simulation-distance", 10);
            World.GenerateStructures = GetPropBool("generate-structures", true);
            World.SpawnMonsters = GetPropBool("spawn-monsters", true);
            World.SpawnAnimals = GetPropBool("spawn-animals", true);
            World.SpawnNpcs = GetPropBool("spawn-npcs", true);

            // Performance
            int parsedRam = ParseMaxRam(_originalJvmArgs);
            Performance.MaxRam = parsedRam > 0 ? parsedRam * 1024 : 2048;

            // Bedrock
            Bedrock.ServerPortV6 = GetPropValue("server-portv6", "19133");
            Bedrock.AllowCheats = GetPropBool("allow-cheats", false);
            Bedrock.TexturepackRequired = GetPropBool("texturepack-required", false);
            Bedrock.DefaultPlayerPermissionLevel = GetPropValue("default-player-permission-level", "member");
            Bedrock.TickDistance = GetPropValue("tick-distance", "4");
            Bedrock.ViewDistance = GetPropInt("view-distance", 32);

            // Advanced
            Advanced.Pvp = World.Pvp;
            Advanced.AllowFlight = World.AllowFlight;
            Advanced.EnableCommandBlock = World.AllowCommandBlock;
            Advanced.EnableQuery = GetPropBool("enable-query", false);
            Advanced.QueryPort = GetPropInt("query.port", 25565);
            Advanced.EnableRcon = GetPropBool("enable-rcon", false);
            Advanced.RconPassword = GetPropValue("rcon.password", "");
            Advanced.RconPort = GetPropInt("rcon.port", 25575);
            Advanced.ResourcePack = GetPropValue("resource-pack", "");

            // Scheduled Maintenance
            var schedule = _rebootScheduleService?.GetSchedule(SelectedInstance.Slug) ?? SelectedInstance.RebootSchedule;
            if (schedule != null)
            {
                Advanced.EnableScheduledReboot = schedule.Enabled;
                Advanced.ScheduledRebootMode = string.IsNullOrEmpty(schedule.Mode) ? "Daily" : schedule.Mode;
                Advanced.ScheduledRebootTime = string.IsNullOrEmpty(schedule.Time) ? "04:00" : schedule.Time;
                Advanced.ScheduledRebootIntervalHours = schedule.IntervalHours > 0 ? schedule.IntervalHours : 24;
                Advanced.ScheduledRebootWarning = schedule.WarningEnabled;
                Advanced.ScheduledRebootWarningSeconds = schedule.WarningSeconds > 0 ? schedule.WarningSeconds : 60;
            }

            // AI Summaries
            Summaries.Load(true);

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

        public async Task SaveSettingsAsync()
        {
            if (SelectedInstance == null) return;

            string propPath = Path.Combine(SelectedInstance.Path, "server.properties");
            var updatedProps = new Dictionary<string, string>(_originalProps);

            // General
            updatedProps["motd"] = General.Motd;
            updatedProps["server-port"] = General.ServerPort.ToString();
            updatedProps["server-ip"] = General.ServerIp;

            // World
            updatedProps["level-name"] = World.LevelName;
            updatedProps["difficulty"] = World.Difficulty;
            updatedProps["gamemode"] = World.Gamemode;
            updatedProps["force-gamemode"] = World.ForceGamemode.ToString().ToLowerInvariant();
            updatedProps["hardcore"] = World.Hardcore.ToString().ToLowerInvariant();
            updatedProps["level-seed"] = World.Seed;
            updatedProps["level-type"] = World.LevelType;
            updatedProps["spawn-protection"] = World.SpawnProtection.ToString();
            updatedProps["max-players"] = World.MaxPlayers.ToString();
            updatedProps["online-mode"] = World.OnlineMode.ToString().ToLowerInvariant();
            updatedProps["white-list"] = World.WhiteList.ToString().ToLowerInvariant();
            updatedProps["enforce-whitelist"] = World.WhiteList.ToString().ToLowerInvariant();
            updatedProps["generate-structures"] = World.GenerateStructures.ToString().ToLowerInvariant();
            updatedProps["spawn-monsters"] = World.SpawnMonsters.ToString().ToLowerInvariant();
            updatedProps["spawn-animals"] = World.SpawnAnimals.ToString().ToLowerInvariant();
            updatedProps["spawn-npcs"] = World.SpawnNpcs.ToString().ToLowerInvariant();
            updatedProps["view-distance"] = World.ViewDistance.ToString();
            updatedProps["simulation-distance"] = World.SimulationDistance.ToString();

            // Bedrock
            updatedProps["server-portv6"] = Bedrock.ServerPortV6;
            updatedProps["allow-cheats"] = Bedrock.AllowCheats.ToString().ToLowerInvariant();
            updatedProps["texturepack-required"] = Bedrock.TexturepackRequired.ToString().ToLowerInvariant();
            updatedProps["default-player-permission-level"] = Bedrock.DefaultPlayerPermissionLevel;
            updatedProps["tick-distance"] = Bedrock.TickDistance;

            // Advanced
            updatedProps["pvp"] = Advanced.Pvp.ToString().ToLowerInvariant();
            updatedProps["allow-flight"] = Advanced.AllowFlight.ToString().ToLowerInvariant();
            updatedProps["enable-command-block"] = Advanced.EnableCommandBlock.ToString().ToLowerInvariant();
            updatedProps["enable-query"] = Advanced.EnableQuery.ToString().ToLowerInvariant();
            updatedProps["query.port"] = Advanced.QueryPort.ToString();
            updatedProps["enable-rcon"] = Advanced.EnableRcon.ToString().ToLowerInvariant();
            if (!string.IsNullOrEmpty(Advanced.RconPassword)) updatedProps["rcon.password"] = Advanced.RconPassword;
            updatedProps["rcon.port"] = Advanced.RconPort.ToString();
            if (!string.IsNullOrWhiteSpace(Advanced.ResourcePack)) updatedProps["resource-pack"] = Advanced.ResourcePack;

            // Write back to server.properties
            var lines = new List<string>
            {
                "#PocketMC server properties",
                $"#Saved at {DateTime.UtcNow:o}"
            };
            foreach (var kvp in updatedProps)
            {
                lines.Add($"{kvp.Key}={kvp.Value}");
            }

            await File.WriteAllLinesAsync(propPath, lines);
            Advanced.RawPropertiesText = string.Join(Environment.NewLine, lines);

            // Save RAM changes to JVM args in metadata
            int ramGb = (int)(Performance.MaxRam / 1024.0);
            if (ramGb < 1) ramGb = 1;
            SelectedInstance.JvmArgs = SetMaxRam(_originalJvmArgs, ramGb);
            SelectedInstance.MaxRamMb = (int)Performance.MaxRam;
            SelectedInstance.MinRamMb = (int)Performance.MinRam;
            SelectedInstance.ServerPort = General.ServerPort;

            // Scheduled Maintenance
            var schedule = new RebootScheduleSettings
            {
                Enabled = Advanced.EnableScheduledReboot,
                Mode = Advanced.ScheduledRebootMode,
                Time = Advanced.ScheduledRebootTime,
                IntervalHours = Advanced.ScheduledRebootIntervalHours,
                WarningEnabled = Advanced.ScheduledRebootWarning,
                WarningSeconds = Advanced.ScheduledRebootWarningSeconds
            };

            if (_rebootScheduleService != null)
            {
                try
                {
                    await _rebootScheduleService.SaveScheduleAsync(SelectedInstance.Slug, schedule);
                }
                catch { }
            }
            SelectedInstance.RebootSchedule = schedule;

            // Write local instance.json metadata file
            var metaPath = Path.Combine(SelectedInstance.Path, "instance.json");
            var json = JsonSerializer.Serialize(SelectedInstance, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(metaPath, json);

            // Refresh original copies
            _originalProps = updatedProps;
            _originalJvmArgs = SelectedInstance.JvmArgs;

            if (HasGeyser && _geyserProvisioningService != null)
            {
                if (int.TryParse(General.GeyserBedrockPort, out int gPort))
                {
                    _geyserProvisioningService.PatchGeyserConfigPort(SelectedInstance.Path, gPort);
                }
            }

            HasUnsavedChanges = false;
        }

        private void CancelEdits()
        {
            LoadSettings();
        }

        private void GoBack()
        {
            var navService = App.Services?.GetService(typeof(PocketMC.App.Navigation.IAppNavigationService)) as PocketMC.App.Navigation.IAppNavigationService;
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
    }
}
