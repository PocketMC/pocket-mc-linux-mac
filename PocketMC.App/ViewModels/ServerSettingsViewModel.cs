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
using PocketMC.Infrastructure.Mods;

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
        public ServerCloudBackupViewModel? CloudBackups { get; }

        public int SubViewModelCount => 9;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsTab0Selected))]
        [NotifyPropertyChangedFor(nameof(IsTab1Selected))]
        [NotifyPropertyChangedFor(nameof(IsTab2Selected))]
        [NotifyPropertyChangedFor(nameof(IsTab3Selected))]
        [NotifyPropertyChangedFor(nameof(IsTab4Selected))]
        [NotifyPropertyChangedFor(nameof(IsTab5Selected))]
        [NotifyPropertyChangedFor(nameof(IsTab6Selected))]
        [NotifyPropertyChangedFor(nameof(IsTab7Selected))]
        [NotifyPropertyChangedFor(nameof(IsTab8Selected))]
        [NotifyPropertyChangedFor(nameof(IsVersionUpdatesTabActive))]
        private int _selectedTabIndex;

        [ObservableProperty]
        private bool _motdEditorVisible;

        public bool IsTab0Selected => SelectedTabIndex == 0;
        public bool IsTab1Selected => SelectedTabIndex == 1;
        public bool IsTab2Selected => SelectedTabIndex == 2;
        public bool IsTab3Selected => SelectedTabIndex == 3;
        public bool IsTab4Selected => SelectedTabIndex == 4;
        public bool IsTab5Selected => SelectedTabIndex == 5;
        public bool IsTab6Selected => SelectedTabIndex == 6;
        public bool IsTab7Selected => SelectedTabIndex == 7;
        public bool IsTab8Selected => SelectedTabIndex == 8;
        public bool IsVersionUpdatesTabActive => SelectedTabIndex == 1;

        public bool IsJavaSettings => SelectedInstance == null ||
            SelectedInstance.EngineType is not EngineType.Bedrock and not EngineType.PocketMine;

        public bool IsBedrockSettings => SelectedInstance?.EngineType == EngineType.Bedrock;
        public bool SupportsJavaWorldGenerator => SelectedInstance == null ||
            SelectedInstance.EngineType is not EngineType.Bedrock and not EngineType.PocketMine;
        public bool SupportsWorldImport => SelectedInstance?.EngineType != EngineType.Pumpkin;
        public string DisplayNameLabel => IsJavaSettings ? "Server Message (MOTD)" : "Server Name (MOTD)";
        public bool IsAiSummarizationAvailable =>
            (_settingsService.Settings.EnableAiSummarization && _settingsService.Settings.IsAiConfigured()) ||
            Summaries.HasSummaries;

        public IRelayCommand<string> SelectTabCommand { get; }
        public IRelayCommand ToggleMotdEditorCommand { get; }

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
            SummaryStorageService? summaryStorageService = null,
            WorldManager? worldManager = null,
            BedrockAddonInstaller? bedrockInstaller = null,
            IEnumerable<ICloudBackupProvider>? cloudProviders = null)
        {
            _instanceService = instanceService;
            _settingsService = settingsService;
            _updateService = updateService;
            _geyserDetector = geyserDetector ?? new GeyserDetector(new AddonManifestService());
            _geyserProvisioningService = geyserProvisioningService;
            _rebootScheduleService = rebootScheduleService;
            _dialogService = dialogService ?? new AvaloniaDialogService();
            _imageProcessingService = imageProcessingService ?? new ImageProcessingService();
            _backupService = backupService ?? (App.Services?.GetService(typeof(BackupService)) as BackupService);
            _summaryStorageService = summaryStorageService ?? new SummaryStorageService();

            var manifestService = App.Services?.GetService(typeof(AddonManifestService)) as AddonManifestService ?? new AddonManifestService();
            var addonUpdateService = App.Services?.GetService(typeof(AddonUpdateService)) as AddonUpdateService;
            var processRunner = App.Services?.GetService(typeof(IProcessRunner)) as IProcessRunner;
            bool IsRunningCheck() => SelectedInstance != null && processRunner?.TryGetRunningInfo(SelectedInstance.Slug, out _, out var st) == true &&
                                     (st == "Running" || st == "Online" || st == "Starting");

            General = new SettingsGeneralVM(string.Empty, _dialogService, _imageProcessingService, MarkDirty);
            World = new SettingsWorldVM(MarkDirty, worldManager, _dialogService, IsRunningCheck);
            Performance = new SettingsPerformanceVM(MarkDirty, _dialogService);
            Bedrock = new SettingsBedrockVM(MarkDirty);
            Backups = new SettingsBackupsVM(_dialogService, MarkDirty, _backupService, IsRunningCheck);
            Addons = new SettingsAddonsVM(_dialogService, MarkDirty, manifestService, addonUpdateService, bedrockInstaller, IsRunningCheck);
            VersionUpdates = new SettingsVersionUpdatesVM(_updateService, IsRunningCheck);
            Summaries = new SettingsSummariesVM(string.Empty, _summaryStorageService, _dialogService);
            Advanced = new SettingsAdvancedVM(MarkDirty);

            var resolvedCloudProviders = cloudProviders ?? (App.Services?.GetService(typeof(IEnumerable<ICloudBackupProvider>)) as IEnumerable<ICloudBackupProvider>);
            if (resolvedCloudProviders != null)
            {
                CloudBackups = new ServerCloudBackupViewModel(_settingsService, resolvedCloudProviders, _dialogService, _backupService, IsRunningCheck);
            }

            SaveCommand = new AsyncRelayCommand(SaveSettingsAsync);
            CancelCommand = new RelayCommand(CancelEdits);
            GoBackCommand = new RelayCommand(GoBack);
            SelectTabCommand = new RelayCommand<string>(idx =>
            {
                if (int.TryParse(idx, out int parsed))
                {
                    SelectedTabIndex = Math.Clamp(parsed, 0, 8);
                }
            });
            ToggleMotdEditorCommand = new RelayCommand(() => MotdEditorVisible = !MotdEditorVisible);
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
                General.AutoStartWithApp = value.AutoStartOnLaunch;
                General.ServerPort = value.ServerPort.HasValue && value.ServerPort.Value > 0 ? value.ServerPort.Value : 25565;
                General.GeyserBedrockPort = (value.GeyserBedrockPort ?? 19132).ToString();
                General.PumpkinBedrockPort = (value.PumpkinBedrockPort ?? 19132).ToString();

                World.SetInstance(value);
                Performance.MaxRam = value.MaxRamMb > 0 ? value.MaxRamMb : 2048;
                Performance.MinRam = value.MinRamMb > 0 ? value.MinRamMb : 1024;
                Performance.AdvancedJvmArgs = value.JvmArgs;

                Backups.MaxBackupsToKeep = value.MaxBackupsToKeep > 0 ? value.MaxBackupsToKeep : 5;
                Backups.CustomBackupDirectory = value.CustomBackupDirectory;
                Backups.SetInstance(value);
                Addons.SetInstance(value);
                VersionUpdates.SetInstance(value);
                Summaries.UpdateServerDir(value.Path);
                Advanced.EnableAutoRestart = value.EnableAutoRestart;
                Advanced.MaxAutoRestarts = value.MaxAutoRestarts.ToString();
                Advanced.AutoRestartDelay = value.AutoRestartDelaySeconds.ToString();
                Advanced.UpdateServerDir(value.Path);
                CloudBackups?.SetInstance(value);

                OnPropertyChanged(nameof(IsJavaSettings));
                OnPropertyChanged(nameof(IsBedrockSettings));
                OnPropertyChanged(nameof(SupportsJavaWorldGenerator));
                OnPropertyChanged(nameof(SupportsWorldImport));
                OnPropertyChanged(nameof(DisplayNameLabel));
                OnPropertyChanged(nameof(IsAiSummarizationAvailable));

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
            SelectedInstance.Name = string.IsNullOrWhiteSpace(General.InstanceName) ? SelectedInstance.Name : General.InstanceName.Trim();
            SelectedInstance.Description = General.InstanceDescription ?? string.Empty;
            SelectedInstance.AutoStartOnLaunch = General.AutoStartWithApp;
            SelectedInstance.JvmArgs = SetMaxRam(_originalJvmArgs, ramGb);
            SelectedInstance.MaxRamMb = (int)Performance.MaxRam;
            SelectedInstance.MinRamMb = (int)Performance.MinRam;
            SelectedInstance.ServerPort = General.ServerPort;
            SelectedInstance.MaxPlayers = World.MaxPlayers;
            SelectedInstance.MaxBackupsToKeep = Backups.MaxBackupsToKeep;
            SelectedInstance.CustomBackupDirectory = Backups.CustomBackupDirectory;
            SelectedInstance.EnableAutoRestart = Advanced.EnableAutoRestart;
            if (int.TryParse(Advanced.MaxAutoRestarts, out int maxRestarts)) SelectedInstance.MaxAutoRestarts = maxRestarts;
            if (int.TryParse(Advanced.AutoRestartDelay, out int restartDelay)) SelectedInstance.AutoRestartDelaySeconds = restartDelay;

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
