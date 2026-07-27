using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.App.Views;

namespace PocketMC.App.ViewModels
{
    public partial class NewInstanceViewModel : ObservableValidator
    {
        private readonly IInstanceService _instanceService;
        private readonly IJavaService _javaService;
        private readonly IPHPService _phpService;

        private string _name = string.Empty;

        [Required(ErrorMessage = "Instance name is required")]
        [MinLength(3, ErrorMessage = "Name must be at least 3 characters")]
        [RegularExpression(@"^[a-zA-Z0-9_\-]+$", ErrorMessage = "Only letters, numbers, hyphens, and underscores are allowed")]
        public string Name
        {
            get => _name;
            set
            {
                SetProperty(ref _name, value, true);
                CreateInstanceCommand.NotifyCanExecuteChanged();
            }
        }

        private EngineType _selectedEngine = EngineType.VanillaJava;
        public EngineType SelectedEngine
        {
            get => _selectedEngine;
            set
            {
                if (SetProperty(ref _selectedEngine, value))
                {
                    OnPropertyChanged(nameof(IsJavaEngine));
                    _ = LoadVersionsAsync();
                }
            }
        }

        private string _description = string.Empty;
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        private bool _enableGeyser;
        public bool EnableGeyser
        {
            get => _enableGeyser;
            set => SetProperty(ref _enableGeyser, value);
        }

        private bool _showSnapshots;
        public bool ShowSnapshots
        {
            get => _showSnapshots;
            set
            {
                if (SetProperty(ref _showSnapshots, value))
                {
                    _ = LoadVersionsAsync();
                }
            }
        }

        private string _worldSeed = string.Empty;
        public string WorldSeed
        {
            get => _worldSeed;
            set => SetProperty(ref _worldSeed, value);
        }

        private string _selectedLevelType = "Default";
        public string SelectedLevelType
        {
            get => _selectedLevelType;
            set => SetProperty(ref _selectedLevelType, value);
        }

        private string _selectedGamemode = "Survival";
        public string SelectedGamemode
        {
            get => _selectedGamemode;
            set => SetProperty(ref _selectedGamemode, value);
        }

        private string _selectedDifficulty = "Easy";
        public string SelectedDifficulty
        {
            get => _selectedDifficulty;
            set => SetProperty(ref _selectedDifficulty, value);
        }

        private string _maxPlayers = "20";
        public string MaxPlayers
        {
            get => _maxPlayers;
            set => SetProperty(ref _maxPlayers, value);
        }

        private string _customWorldPath = string.Empty;
        public string CustomWorldPath
        {
            get => _customWorldPath;
            set => SetProperty(ref _customWorldPath, value);
        }

        public bool IsJavaEngine => SelectedEngine != EngineType.Bedrock && SelectedEngine != EngineType.PocketMine;

        public List<string> LevelTypes { get; } = new() { "Default", "Flat", "LargeBiomes", "Amplified" };
        public List<string> Gamemodes { get; } = new() { "Survival", "Creative", "Adventure", "Spectator" };
        public List<string> Difficulties { get; } = new() { "Peaceful", "Easy", "Normal", "Hard" };

        private double _progress;
        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        private string _progressText = string.Empty;
        public string ProgressText
        {
            get => _progressText;
            set => SetProperty(ref _progressText, value);
        }

        public ObservableCollection<string> Versions { get; } = new();
        public List<EngineType> EngineTypes { get; } = Enum.GetValues(typeof(EngineType)).Cast<EngineType>().ToList();

        public IAsyncRelayCommand CreateInstanceCommand { get; }
        public IAsyncRelayCommand CancelCommand { get; }
        public IAsyncRelayCommand BrowseWorldCommand { get; }
        public IRelayCommand OpenEulaLinkCommand { get; }

        public NewInstanceViewModel(
            IInstanceService instanceService,
            IJavaService javaService,
            IPHPService phpService)
        {
            _instanceService = instanceService;
            _javaService = javaService;
            _phpService = phpService;

            CreateInstanceCommand = new AsyncRelayCommand(CreateInstanceAsync, CanCreate);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            BrowseWorldCommand = new AsyncRelayCommand(BrowseWorldAsync);
            OpenEulaLinkCommand = new RelayCommand(OpenEulaLink);

            // Initial load
            _ = LoadVersionsAsync();
        }

        private async Task BrowseWorldAsync()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(desktop.MainWindow);
                if (topLevel != null)
                {
                    var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
                    {
                        Title = "Select World Archive (.zip / .mcworld)",
                        AllowMultiple = false,
                        FileTypeFilter = new[]
                        {
                            new Avalonia.Platform.Storage.FilePickerFileType("World Archive") { Patterns = new[] { "*.zip", "*.mcworld" } }
                        }
                    });

                    if (files.Count > 0)
                    {
                        CustomWorldPath = files[0].Path.LocalPath;
                    }
                }
            }
        }

        private void OpenEulaLink()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://aka.ms/MinecraftEULA",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        [RelayCommand]
        private void ApplyPresetVanillaJava()
        {
            Name = "Vanilla-Survival-Server";
            Description = "Standard Vanilla Minecraft Survival Server";
            SelectedEngine = EngineType.VanillaJava;
            EnableGeyser = false;
            SelectedGamemode = "Survival";
            SelectedDifficulty = "Easy";
            MaxPlayers = "20";
        }

        [RelayCommand]
        private void ApplyPresetCrossplayGeyser()
        {
            Name = "Crossplay-Java-Bedrock";
            Description = "Paper Minecraft Server with Geyser + Floodgate Crossplay";
            SelectedEngine = EngineType.Paper;
            EnableGeyser = true;
            SelectedGamemode = "Survival";
            SelectedDifficulty = "Easy";
            MaxPlayers = "30";
        }

        [RelayCommand]
        private void ApplyPresetBedrock()
        {
            Name = "Bedrock-Dedicated-Server";
            Description = "Native Bedrock Dedicated Server";
            SelectedEngine = EngineType.Bedrock;
            EnableGeyser = false;
            SelectedGamemode = "Survival";
            SelectedDifficulty = "Easy";
            MaxPlayers = "20";
        }

        [RelayCommand]
        private void ApplyPresetPocketMine()
        {
            Name = "PocketMine-Bedrock-Server";
            Description = "PocketMine-MP PHP Bedrock Server";
            SelectedEngine = EngineType.PocketMine;
            EnableGeyser = false;
            SelectedGamemode = "Survival";
            SelectedDifficulty = "Easy";
            MaxPlayers = "20";
        }

        private string _selectedVersion = string.Empty;
        public string SelectedVersion
        {
            get => _selectedVersion;
            set
            {
                SetProperty(ref _selectedVersion, value);
                CreateInstanceCommand.NotifyCanExecuteChanged();
            }
        }

        private bool _acceptEula;
        public bool AcceptEula
        {
            get => _acceptEula;
            set
            {
                if (SetProperty(ref _acceptEula, value))
                {
                    CreateInstanceCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private bool _isCreating;
        public bool IsCreating
        {
            get => _isCreating;
            set
            {
                if (SetProperty(ref _isCreating, value))
                {
                    CreateInstanceCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private bool _isLoadingVersions;
        public bool IsLoadingVersions
        {
            get => _isLoadingVersions;
            set
            {
                if (SetProperty(ref _isLoadingVersions, value))
                {
                    CreateInstanceCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private static List<string> SortVersionsDescending(IEnumerable<string> versions)
        {
            return versions.Distinct()
                .Select(v =>
                {
                    var clean = v.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? v[1..] : v;
                    var coreVer = clean.Split(new[] { '-', '+' }, 2)[0];
                    Version.TryParse(coreVer, out var parsed);
                    return new { Original = v, Parsed = parsed ?? new Version(0, 0) };
                })
                .OrderByDescending(x => x.Parsed)
                .ThenByDescending(x => x.Original, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.Original)
                .ToList();
        }

        private System.Threading.CancellationTokenSource? _loadVersionsCts;

        private static bool IsSnapshotOrPreRelease(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return false;
            var lower = version.ToLowerInvariant();
            return lower.Contains("-rc") ||
                   lower.Contains(".rc") ||
                   lower.Contains("-pre") ||
                   lower.Contains(".pre") ||
                   lower.Contains("snapshot") ||
                   lower.Contains("preview") ||
                   lower.Contains("beta") ||
                   lower.Contains("alpha") ||
                   lower.Contains("-dev") ||
                   System.Text.RegularExpressions.Regex.IsMatch(lower, @"\d{2}w\d{2}[a-z]");
        }

        public async Task LoadVersionsAsync()
        {
            _loadVersionsCts?.Cancel();
            _loadVersionsCts = new System.Threading.CancellationTokenSource();
            var ct = _loadVersionsCts.Token;

            IsLoadingVersions = true;
            var list = new List<string>();
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "PocketMC-App");

                if (SelectedEngine == EngineType.PocketMine)
                {
                    var response = await client.GetStringAsync("https://api.github.com/repos/pmmp/PocketMine-MP/releases", ct);
                    using var doc = JsonDocument.Parse(response);
                    var versionList = new List<string>();
                    foreach (var release in doc.RootElement.EnumerateArray())
                    {
                        var isPrerelease = release.TryGetProperty("prerelease", out var preProp) && preProp.GetBoolean();
                        var tag = release.GetProperty("tag_name").GetString();
                        if (!string.IsNullOrEmpty(tag))
                        {
                            if (ShowSnapshots || (!isPrerelease && !IsSnapshotOrPreRelease(tag)))
                            {
                                versionList.Add(tag);
                            }
                        }
                    }
                    list.AddRange(SortVersionsDescending(versionList).Take(25));
                }
                else if (SelectedEngine == EngineType.Bedrock)
                {
                    var response = await client.GetStringAsync("https://raw.githubusercontent.com/kittizz/bedrock-server-downloads/main/bedrock-server-downloads.json", ct);
                    using var doc = JsonDocument.Parse(response);
                    var versionList = new List<string>();

                    if (doc.RootElement.TryGetProperty("release", out var releases))
                    {
                        foreach (var property in releases.EnumerateObject())
                        {
                            if (ShowSnapshots || !IsSnapshotOrPreRelease(property.Name))
                            {
                                versionList.Add(property.Name);
                            }
                        }
                    }

                    if (ShowSnapshots && doc.RootElement.TryGetProperty("preview", out var previews))
                    {
                        foreach (var property in previews.EnumerateObject())
                        {
                            versionList.Add(property.Name);
                        }
                    }

                    list.AddRange(SortVersionsDescending(versionList).Take(25));
                }
                else if (SelectedEngine == EngineType.Paper)
                {
                    var response = await client.GetStringAsync("https://fill.papermc.io/v3/projects/paper", ct);
                    using var doc = JsonDocument.Parse(response);
                    var versionsObj = doc.RootElement.GetProperty("versions");
                    var allVersions = new List<string>();

                    foreach (var prop in versionsObj.EnumerateObject())
                    {
                        foreach (var verNode in prop.Value.EnumerateArray())
                        {
                            var vStr = verNode.GetString();
                            if (!string.IsNullOrEmpty(vStr))
                            {
                                if (ShowSnapshots || !IsSnapshotOrPreRelease(vStr))
                                {
                                    allVersions.Add(vStr);
                                }
                            }
                        }
                    }

                    list.AddRange(SortVersionsDescending(allVersions).Take(25));
                }
                else if (SelectedEngine == EngineType.Fabric)
                {
                    var response = await client.GetStringAsync("https://meta.fabricmc.net/v2/versions/game", ct);
                    using var doc = JsonDocument.Parse(response);
                    var versionList = new List<string>();

                    foreach (var ver in doc.RootElement.EnumerateArray())
                    {
                        string? id = null;
                        if (ver.TryGetProperty("version", out var vProp)) id = vProp.GetString();
                        else if (ver.TryGetProperty("id", out var iProp)) id = iProp.GetString();

                        var stable = ver.TryGetProperty("stable", out var sProp) && sProp.GetBoolean();
                        if (!string.IsNullOrEmpty(id))
                        {
                            if (ShowSnapshots || (stable && !IsSnapshotOrPreRelease(id)))
                            {
                                versionList.Add(id);
                            }
                        }
                    }

                    list.AddRange(SortVersionsDescending(versionList).Take(25));
                }
                else if (SelectedEngine == EngineType.Forge)
                {
                    var response = await client.GetStringAsync("https://meta.prismlauncher.org/v1/net.minecraftforge/index.json", ct);
                    using var doc = JsonDocument.Parse(response);
                    var versionsArray = doc.RootElement.GetProperty("versions");
                    var mcVersionsSet = new HashSet<string>();

                    foreach (var verNode in versionsArray.EnumerateArray())
                    {
                        if (verNode.TryGetProperty("requires", out var reqArray))
                        {
                            foreach (var req in reqArray.EnumerateArray())
                            {
                                if (req.TryGetProperty("uid", out var uid) && uid.GetString() == "net.minecraft")
                                {
                                    var mcEquals = req.GetProperty("equals").GetString();
                                    if (!string.IsNullOrEmpty(mcEquals))
                                    {
                                        if (ShowSnapshots || !IsSnapshotOrPreRelease(mcEquals))
                                        {
                                            mcVersionsSet.Add(mcEquals);
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                    }

                    list.AddRange(SortVersionsDescending(mcVersionsSet).Take(25));
                }
                else if (SelectedEngine == EngineType.NeoForge)
                {
                    var response = await client.GetStringAsync("https://meta.prismlauncher.org/v1/net.neoforged/index.json", ct);
                    using var doc = JsonDocument.Parse(response);
                    var versionsArray = doc.RootElement.GetProperty("versions");
                    var mcVersionsSet = new HashSet<string>();

                    foreach (var verNode in versionsArray.EnumerateArray())
                    {
                        if (verNode.TryGetProperty("requires", out var reqArray))
                        {
                            foreach (var req in reqArray.EnumerateArray())
                            {
                                if (req.TryGetProperty("uid", out var uid) && uid.GetString() == "net.minecraft")
                                {
                                    var mcEquals = req.GetProperty("equals").GetString();
                                    if (!string.IsNullOrEmpty(mcEquals))
                                    {
                                        if (ShowSnapshots || !IsSnapshotOrPreRelease(mcEquals))
                                        {
                                            mcVersionsSet.Add(mcEquals);
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                    }

                    list.AddRange(SortVersionsDescending(mcVersionsSet).Take(25));
                }
                else // VanillaJava
                {
                    var response = await client.GetStringAsync("https://launchermeta.mojang.com/mc/game/version_manifest.json", ct);
                    using var doc = JsonDocument.Parse(response);
                    var manifestList = doc.RootElement.GetProperty("versions");
                    var versionList = new List<string>();

                    foreach (var ver in manifestList.EnumerateArray())
                    {
                        var id = ver.GetProperty("id").GetString();
                        var type = ver.GetProperty("type").GetString();
                        if (!string.IsNullOrEmpty(id))
                        {
                            if (type == "release" && (!IsSnapshotOrPreRelease(id) || ShowSnapshots))
                            {
                                versionList.Add(id);
                            }
                            else if (ShowSnapshots && (type == "snapshot" || type == "old_beta" || type == "old_alpha"))
                            {
                                versionList.Add(id);
                            }
                        }
                    }

                    list.AddRange(SortVersionsDescending(versionList).Take(35));
                }

                if (ct.IsCancellationRequested) return;

                if (list.Count == 0)
                {
                    if (SelectedEngine == EngineType.PocketMine)
                    {
                        list.Add("5.1.0");
                        list.Add("5.0.0");
                    }
                    else if (SelectedEngine == EngineType.Bedrock)
                    {
                        list.Add("1.21.0");
                        list.Add("1.20.80");
                    }
                    else
                    {
                        list.Add("1.21.4");
                        list.Add("1.21");
                        list.Add("1.20.4");
                        list.Add("1.20.1");
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    if (ct.IsCancellationRequested) return;
                    Versions.Clear();
                    foreach (var v in list) Versions.Add(v);
                    SelectedVersion = Versions.FirstOrDefault() ?? string.Empty;
                });
            }
            catch (OperationCanceledException)
            {
                // Ignored due to cancelled task
            }
            catch (Exception ex)
            {
                if (ct.IsCancellationRequested) return;

                if (SelectedEngine == EngineType.PocketMine)
                {
                    list.Add("5.1.0");
                }
                else if (SelectedEngine == EngineType.Bedrock)
                {
                    list.Add("1.21.0");
                }
                else
                {
                    list.Add("1.21.4");
                    list.Add("1.21");
                    list.Add("1.20.4");
                }

                Dispatcher.UIThread.Post(() =>
                {
                    if (ct.IsCancellationRequested) return;
                    Versions.Clear();
                    foreach (var v in list) Versions.Add(v);
                    SelectedVersion = Versions.FirstOrDefault() ?? string.Empty;
                    ProgressText = $"Error loading versions (using fallbacks): {ex.Message}";
                });
            }
            finally
            {
                if (!ct.IsCancellationRequested)
                {
                    IsLoadingVersions = false;
                }
            }
        }

        private async Task CreateInstanceAsync()
        {
            ValidateAllProperties();
            if (HasErrors || !AcceptEula) return;

            IsCreating = true;
            Progress = 0.0;
            ProgressText = "Preparing instance directory...";

            try
            {
                var instance = await _instanceService.CreateInstanceAsync(Name, SelectedEngine, SelectedVersion);
                var targetDir = instance.Path;

                Progress = 0.1;
                ProgressText = "Folder initialized. Checking runtimes...";

                if (SelectedEngine == EngineType.VanillaJava ||
                    SelectedEngine == EngineType.Fabric ||
                    SelectedEngine == EngineType.Paper ||
                    SelectedEngine == EngineType.Forge ||
                    SelectedEngine == EngineType.NeoForge)
                {
                    string javaVersion = MapMinecraftVersionToJavaVersion(SelectedVersion);
                    var javaProgress = new Progress<double>(p =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            Progress = 0.2 + (p * 0.3); // Map 0.2 -> 0.5
                            ProgressText = $"Downloading Java {javaVersion} runtime: {p * 100.0:F0}%";
                        });
                    });
                    await _javaService.ProvisionJavaRuntimeAsync(javaVersion, javaProgress);

                    string eulaPath = Path.Combine(targetDir, "eula.txt");
                    await File.WriteAllTextAsync(eulaPath, "eula=true\n");

                    string jarPath = Path.Combine(targetDir, "server.jar");

                    if (SelectedEngine == EngineType.VanillaJava)
                    {
                        ProgressText = $"Resolving Minecraft {SelectedVersion} download url...";
                        Progress = 0.5;
                        await DownloadVanillaServerJarAsync(SelectedVersion, jarPath);
                    }
                    else if (SelectedEngine == EngineType.Fabric)
                    {
                        ProgressText = $"Downloading Fabric {SelectedVersion} server jar...";
                        Progress = 0.5;
                        await DownloadFabricServerJarAsync(SelectedVersion, jarPath);
                    }
                    else if (SelectedEngine == EngineType.Paper)
                    {
                        ProgressText = $"Downloading Paper {SelectedVersion} server jar...";
                        Progress = 0.5;
                        await DownloadPaperServerJarAsync(SelectedVersion, jarPath);
                    }
                    else if (SelectedEngine == EngineType.Forge)
                    {
                        ProgressText = $"Downloading Forge installer for Minecraft {SelectedVersion}...";
                        Progress = 0.5;
                        string installerPath = Path.Combine(targetDir, "installer.jar");
                        await DownloadForgeInstallerAsync(SelectedVersion, installerPath);

                        ProgressText = "Running Forge installer (this may take several minutes)...";
                        Progress = 0.7;
                        string javaExec = await _javaService.GetJavaExecutablePathAsync(javaVersion);
                        await RunInstallerAsync(javaExec, targetDir, installerPath);
                    }
                    else if (SelectedEngine == EngineType.NeoForge)
                    {
                        ProgressText = $"Downloading NeoForge installer for Minecraft {SelectedVersion}...";
                        Progress = 0.5;
                        string installerPath = Path.Combine(targetDir, "installer.jar");
                        await DownloadNeoForgeInstallerAsync(SelectedVersion, installerPath);

                        ProgressText = "Running NeoForge installer (this may take several minutes)...";
                        Progress = 0.7;
                        string javaExec = await _javaService.GetJavaExecutablePathAsync(javaVersion);
                        await RunInstallerAsync(javaExec, targetDir, installerPath);
                    }
                    // Write customized server.properties
                    string propPath = Path.Combine(targetDir, "server.properties");
                    string propsContent = $"# Minecraft server properties\n" +
                                          $"level-name=world\n" +
                                          $"level-seed={WorldSeed}\n" +
                                          $"level-type={SelectedLevelType.ToLower()}\n" +
                                          $"gamemode={SelectedGamemode.ToLower()}\n" +
                                          $"difficulty={SelectedDifficulty.ToLower()}\n" +
                                          $"max-players={(string.IsNullOrWhiteSpace(MaxPlayers) ? "20" : MaxPlayers)}\n" +
                                          $"motd={(string.IsNullOrWhiteSpace(Description) ? "A Minecraft Server created with PocketMC" : Description)}\n" +
                                          $"server-port=25565\n" +
                                          $"enable-rcon=false\n";
                    await File.WriteAllTextAsync(propPath, propsContent);

                    if (EnableGeyser)
                    {
                        try
                        {
                            ProgressText = "Downloading Geyser & Floodgate cross-play plugins...";
                            using var client = new HttpClient();
                            client.DefaultRequestHeaders.Add("User-Agent", "PocketMC-App");

                            if (SelectedEngine == EngineType.Fabric)
                            {
                                string modsDir = Path.Combine(targetDir, "mods");
                                Directory.CreateDirectory(modsDir);
                                await DownloadFileWithProgressAsync(client, "https://download.geysermc.org/v2/projects/geyser/versions/latest/builds/latest/downloads/fabric", Path.Combine(modsDir, "Geyser-Fabric.jar"), "Downloading Geyser Fabric mod");
                                await DownloadFileWithProgressAsync(client, "https://download.geysermc.org/v2/projects/floodgate/versions/latest/builds/latest/downloads/fabric", Path.Combine(modsDir, "Floodgate-Fabric.jar"), "Downloading Floodgate Fabric mod");
                            }
                            else if (SelectedEngine == EngineType.NeoForge || SelectedEngine == EngineType.Forge)
                            {
                                string modsDir = Path.Combine(targetDir, "mods");
                                Directory.CreateDirectory(modsDir);
                                await DownloadFileWithProgressAsync(client, "https://download.geysermc.org/v2/projects/geyser/versions/latest/builds/latest/downloads/neoforge", Path.Combine(modsDir, "Geyser-NeoForge.jar"), "Downloading Geyser NeoForge mod");
                            }
                            else
                            {
                                string pluginsDir = Path.Combine(targetDir, "plugins");
                                Directory.CreateDirectory(pluginsDir);
                                await DownloadFileWithProgressAsync(client, "https://download.geysermc.org/v2/projects/geyser/versions/latest/builds/latest/downloads/spigot", Path.Combine(pluginsDir, "Geyser-Spigot.jar"), "Downloading Geyser plugin");
                                await DownloadFileWithProgressAsync(client, "https://download.geysermc.org/v2/projects/floodgate/versions/latest/builds/latest/downloads/spigot", Path.Combine(pluginsDir, "Floodgate-Spigot.jar"), "Downloading Floodgate plugin");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to download Geyser/Floodgate: {ex.Message}");
                        }
                    }

                    Progress = 0.8;
                }
                else if (SelectedEngine == EngineType.PocketMine)
                {
                    var phpProgress = new Progress<double>(p =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            Progress = 0.2 + (p * 0.3); // Map 0.2 -> 0.5
                            ProgressText = $"Downloading PHP runtime: {p * 100.0:F0}%";
                        });
                    });
                    await _phpService.ProvisionPHPRuntimeAsync("8.2", phpProgress);

                    ProgressText = $"Downloading PocketMine-MP {SelectedVersion} server file...";
                    Progress = 0.5;
                    string pharPath = Path.Combine(targetDir, "PocketMine-MP.phar");
                    await DownloadPocketMinePharAsync(SelectedVersion, pharPath);
                    Progress = 0.8;
                }
                else if (SelectedEngine == EngineType.Bedrock)
                {
                    ProgressText = $"Downloading Bedrock Dedicated Server {SelectedVersion}...";
                    Progress = 0.3;
                    string zipPath = Path.Combine(targetDir, "bds.zip");
                    await DownloadBedrockZipAsync(SelectedVersion, zipPath);

                    ProgressText = "Extracting Bedrock Dedicated Server files...";
                    Progress = 0.7;
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, targetDir);
                    try { File.Delete(zipPath); } catch { }

                    try
                    {
                        string execPath = Path.Combine(targetDir, "bedrock_server");
                        if (File.Exists(execPath))
                        {
                            System.Diagnostics.Process.Start("chmod", $"+x \"{execPath}\"")?.WaitForExit();
                        }
                    }
                    catch { }
                    Progress = 0.9;
                }

                if (!string.IsNullOrWhiteSpace(CustomWorldPath) && File.Exists(CustomWorldPath))
                {
                    try
                    {
                        ProgressText = "Extracting custom world archive...";
                        string worldDir = Path.Combine(targetDir, "world");
                        Directory.CreateDirectory(worldDir);
                        System.IO.Compression.ZipFile.ExtractToDirectory(CustomWorldPath, worldDir, true);
                    }
                    catch { }
                }

                Progress = 1.0;
                ProgressText = "Created successfully!";
                await Task.Delay(1000);

                var mainVM = App.Services.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
                var dashVM = App.Services.GetService(typeof(DashboardViewModel)) as DashboardViewModel;
                if (mainVM != null && dashVM != null)
                {
                    await dashVM.LoadInstancesAsync();
                    var createdInstance = dashVM.Instances.FirstOrDefault(i => string.Equals(i.Name, instance.Name, StringComparison.OrdinalIgnoreCase)) ?? dashVM.Instances.LastOrDefault();
                    if (createdInstance != null)
                    {
                        dashVM.SelectedInstance = createdInstance;
                    }
                    mainVM.CurrentViewModel = dashVM;
                }
            }
            catch (Exception ex)
            {
                ProgressText = $"Creation failed: {ex.Message}";
                try
                {
                    await _instanceService.DeleteInstanceAsync(Slugify(Name));
                }
                catch { }

                Dispatcher.UIThread.Post(async () =>
                {
                    await ConfirmationDialogWindow.ShowAsync(
                        "Instance Creation Failed",
                        $"Could not create server instance '{Name}':\n\n{ex.Message}\n\nPlease select a supported server version and try again.",
                        "OK"
                    );
                });
            }
            finally
            {
                IsCreating = false;
            }
        }

        private string MapMinecraftVersionToJavaVersion(string mcVersion)
        {
            if (Version.TryParse(mcVersion, out var version))
            {
                if (version >= new Version(1, 22, 0)) return "25";
                if (version >= new Version(1, 20, 5)) return "21";
                if (version >= new Version(1, 17, 0)) return "17";
                if (version >= new Version(1, 12, 0)) return "11";
                return "8";
            }
            if (mcVersion.StartsWith("1.22") || mcVersion.StartsWith("1.23") || mcVersion.StartsWith("1.24")) return "25";
            if (mcVersion.StartsWith("1.21")) return "21";
            if (mcVersion.StartsWith("1.20") || mcVersion.StartsWith("1.19") || mcVersion.StartsWith("1.18") || mcVersion.StartsWith("1.17")) return "17";
            return "8";
        }

        private string Slugify(string name)
        {
            return string.Concat(name.Select(c => char.IsLetterOrDigit(c) ? char.ToLower(c) : '-'))
                .Replace("--", "-").Trim('-');
        }

        private async Task DownloadFileWithProgressAsync(HttpClient client, string url, string destinationPath, string prefixText)
        {
            var currentUrl = url;
            HttpResponseMessage response;
            int redirects = 0;

            while (true)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, currentUrl);
                response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                int statusCode = (int)response.StatusCode;
                if ((statusCode == 301 || statusCode == 302 || statusCode == 303 || statusCode == 307 || statusCode == 308) && response.Headers.Location != null)
                {
                    redirects++;
                    if (redirects > 10)
                        throw new Exception("Too many redirects while downloading file.");

                    var loc = response.Headers.Location;
                    if (!loc.IsAbsoluteUri)
                    {
                        currentUrl = new Uri(new Uri(currentUrl), loc).ToString();
                    }
                    else
                    {
                        currentUrl = loc.ToString();
                    }
                    response.Dispose();
                    continue;
                }
                break;
            }

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var downloadStream = await response.Content.ReadAsStreamAsync();
            using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            var totalReadBytes = 0L;
            var bytesRead = 0;
            var startTime = DateTime.UtcNow;

            while ((bytesRead = await downloadStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fs.WriteAsync(buffer, 0, bytesRead);
                totalReadBytes += bytesRead;

                double elapsedSeconds = (DateTime.UtcNow - startTime).TotalSeconds;
                double speedMBs = elapsedSeconds > 0 ? (totalReadBytes / (1024.0 * 1024.0)) / elapsedSeconds : 0.0;

                if (totalBytes > 0)
                {
                    double progressFraction = (double)totalReadBytes / totalBytes;
                    double percent = progressFraction * 100.0;

                    Dispatcher.UIThread.Post(() =>
                    {
                        double scaledProgress = 0.0;
                        if (prefixText.Contains("Java") || prefixText.Contains("PHP"))
                        {
                            scaledProgress = 0.2 + (progressFraction * 0.3);
                        }
                        else if (prefixText.Contains("Vanilla") || prefixText.Contains("Fabric") || prefixText.Contains("Paper"))
                        {
                            scaledProgress = 0.5 + (progressFraction * 0.3);
                        }
                        else if (prefixText.Contains("Bedrock"))
                        {
                            scaledProgress = 0.3 + (progressFraction * 0.4);
                        }
                        else
                        {
                            scaledProgress = 0.5 + (progressFraction * 0.3);
                        }

                        Progress = Math.Clamp(scaledProgress, 0.0, 1.0);
                        ProgressText = $"{prefixText}: {totalReadBytes / (1024.0 * 1024.0):F1} / {totalBytes / (1024.0 * 1024.0):F1} MB ({percent:F0}%) @ {speedMBs:F2} MB/s";
                    });
                }
                else
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        ProgressText = $"{prefixText}: {totalReadBytes / (1024.0 * 1024.0):F1} MB @ {speedMBs:F2} MB/s";
                    });
                }
            }
        }

        private async Task DownloadVanillaServerJarAsync(string mcVersion, string destinationPath)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "PocketMC-App");

            string manifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest.json";
            var manifestStr = await client.GetStringAsync(manifestUrl);
            using var manifestDoc = JsonDocument.Parse(manifestStr);
            var versions = manifestDoc.RootElement.GetProperty("versions");
            
            string? versionMetaUrl = null;
            foreach (var version in versions.EnumerateArray())
            {
                if (version.GetProperty("id").GetString() == mcVersion)
                {
                    versionMetaUrl = version.GetProperty("url").GetString();
                    break;
                }
            }

            if (string.IsNullOrEmpty(versionMetaUrl))
                throw new Exception($"Version {mcVersion} not found in Mojang manifest.");

            var metaStr = await client.GetStringAsync(versionMetaUrl);
            using var metaDoc = JsonDocument.Parse(metaStr);
            var serverDownloadUrl = metaDoc.RootElement
                .GetProperty("downloads")
                .GetProperty("server")
                .GetProperty("url")
                .GetString();

            if (string.IsNullOrEmpty(serverDownloadUrl))
                throw new Exception($"No server download found for Vanilla {mcVersion}.");

            await DownloadFileWithProgressAsync(client, serverDownloadUrl, destinationPath, "Downloading Vanilla server jar");
        }

        private async Task DownloadFabricServerJarAsync(string mcVersion, string destinationPath)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "PocketMC-App");

            string loaderVersion = "0.15.11";
            try
            {
                var loadersStr = await client.GetStringAsync("https://meta.fabricmc.net/v2/versions/loader");
                using var loadersDoc = JsonDocument.Parse(loadersStr);
                foreach (var item in loadersDoc.RootElement.EnumerateArray())
                {
                    if (item.GetProperty("stable").GetBoolean())
                    {
                        loaderVersion = item.GetProperty("version").GetString() ?? loaderVersion;
                        break;
                    }
                }
            }
            catch { }

            string installerVersion = "1.0.1";
            try
            {
                var installersStr = await client.GetStringAsync("https://meta.fabricmc.net/v2/versions/installer");
                using var installersDoc = JsonDocument.Parse(installersStr);
                foreach (var item in installersDoc.RootElement.EnumerateArray())
                {
                    if (item.GetProperty("stable").GetBoolean())
                    {
                        installerVersion = item.GetProperty("version").GetString() ?? installerVersion;
                        break;
                    }
                }
            }
            catch { }

            string url = $"https://meta.fabricmc.net/v2/versions/loader/{mcVersion}/{loaderVersion}/{installerVersion}/server/jar";
            await DownloadFileWithProgressAsync(client, url, destinationPath, "Downloading Fabric server jar");
        }

        private async Task DownloadPaperServerJarAsync(string mcVersion, string destinationPath)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "PocketMC-App");

            string url = $"https://fill.papermc.io/v3/projects/paper/versions/{mcVersion}";
            var responseStr = await client.GetStringAsync(url);
            using var doc = JsonDocument.Parse(responseStr);
            var builds = doc.RootElement.GetProperty("builds");
            
            int maxBuild = 0;
            foreach (var build in builds.EnumerateArray())
            {
                int b = build.GetInt32();
                if (b > maxBuild) maxBuild = b;
            }

            if (maxBuild == 0)
                throw new Exception($"No builds found for Paper version {mcVersion}.");

            string buildUrl = $"https://fill.papermc.io/v3/projects/paper/versions/{mcVersion}/builds/{maxBuild}";
            var buildStr = await client.GetStringAsync(buildUrl);
            using var buildDoc = JsonDocument.Parse(buildStr);

            string? downloadUrl = null;
            if (buildDoc.RootElement.TryGetProperty("downloads", out var downloadsEl))
            {
                foreach (var prop in downloadsEl.EnumerateObject())
                {
                    if (prop.Value.TryGetProperty("url", out var u))
                    {
                        downloadUrl = u.GetString();
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                throw new Exception($"Could not resolve download URL for Paper {mcVersion} build {maxBuild}.");
            }

            await DownloadFileWithProgressAsync(client, downloadUrl, destinationPath, "Downloading Paper server jar");
        }

        private async Task DownloadPocketMinePharAsync(string versionId, string destinationPath)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "PocketMC-App");

            var responseStr = await client.GetStringAsync("https://api.github.com/repos/pmmp/PocketMine-MP/releases");
            using var doc = JsonDocument.Parse(responseStr);
            string? downloadUrl = null;

            foreach (var release in doc.RootElement.EnumerateArray())
            {
                if (release.GetProperty("tag_name").GetString() == versionId)
                {
                    var assets = release.GetProperty("assets");
                    foreach (var asset in assets.EnumerateArray())
                    {
                        if (asset.GetProperty("name").GetString() == "PocketMine-MP.phar")
                        {
                            downloadUrl = asset.GetProperty("browser_download_url").GetString();
                            break;
                        }
                    }
                    break;
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                throw new Exception($"Could not find PocketMine-MP.phar download URL for version {versionId}.");
            }

            await DownloadFileWithProgressAsync(client, downloadUrl, destinationPath, "Downloading PocketMine-MP.phar");
        }

        private async Task DownloadBedrockZipAsync(string versionId, string destinationPath)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "PocketMC-App");

            var manifestStr = await client.GetStringAsync("https://raw.githubusercontent.com/kittizz/bedrock-server-downloads/main/bedrock-server-downloads.json");
            using var doc = JsonDocument.Parse(manifestStr);
            var releases = doc.RootElement.GetProperty("release");
            
            string? downloadUrl = null;
            if (releases.TryGetProperty(versionId, out var releaseObj))
            {
                if (releaseObj.TryGetProperty("linux", out var linuxObj))
                {
                    downloadUrl = linuxObj.GetProperty("url").GetString();
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                foreach (var prop in releases.EnumerateObject())
                {
                    if (prop.Value.TryGetProperty("linux", out var linuxObj))
                    {
                        downloadUrl = linuxObj.GetProperty("url").GetString();
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                throw new Exception($"Could not find Bedrock Dedicated Server download URL for version {versionId}.");
            }

            await DownloadFileWithProgressAsync(client, downloadUrl, destinationPath, "Downloading Bedrock server zip");
        }

        private async Task DownloadForgeInstallerAsync(string mcVersion, string destinationPath)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "PocketMC-App");

            string forgeVersion = "latest";
            try
            {
                var response = await client.GetFromJsonAsync<JsonElement>("https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json");
                if (response.TryGetProperty("promos", out var promos))
                {
                    if (promos.TryGetProperty($"{mcVersion}-recommended", out var rec))
                    {
                        forgeVersion = rec.GetString() ?? forgeVersion;
                    }
                    else if (promos.TryGetProperty($"{mcVersion}-latest", out var lat))
                    {
                        forgeVersion = lat.GetString() ?? forgeVersion;
                    }
                }
            }
            catch { }

            if (forgeVersion == "latest")
            {
                // Fallback: Query Prism Launcher metadata to resolve the latest Forge version for this Minecraft version
                try
                {
                    var prismResponse = await client.GetFromJsonAsync<JsonElement>("https://meta.prismlauncher.org/v1/net.minecraftforge/index.json");
                    if (prismResponse.TryGetProperty("versions", out var versionsArray))
                    {
                        foreach (var verNode in versionsArray.EnumerateArray())
                        {
                            if (verNode.TryGetProperty("requires", out var reqArray))
                            {
                                foreach (var req in reqArray.EnumerateArray())
                                {
                                    if (req.TryGetProperty("uid", out var uid) && uid.GetString() == "net.minecraft")
                                    {
                                        var mcEquals = req.GetProperty("equals").GetString();
                                        if (mcEquals == mcVersion)
                                        {
                                            forgeVersion = verNode.GetProperty("version").GetString() ?? forgeVersion;
                                            break;
                                        }
                                    }
                                }
                            }
                            if (forgeVersion != "latest") break;
                        }
                    }
                }
                catch { }
            }

            if (forgeVersion == "latest")
                throw new Exception($"Could not find a valid Forge version for Minecraft {mcVersion}.");

            string url = $"https://maven.minecraftforge.net/net/minecraftforge/forge/{mcVersion}-{forgeVersion}/forge-{mcVersion}-{forgeVersion}-installer.jar";
            await DownloadFileWithProgressAsync(client, url, destinationPath, "Downloading Forge installer");
        }

        private async Task DownloadNeoForgeInstallerAsync(string mcVersion, string destinationPath)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "PocketMC-App");

            string? neoforgeVersion = null;
            try
            {
                var prismResponse = await client.GetFromJsonAsync<JsonElement>("https://meta.prismlauncher.org/v1/net.neoforged/index.json");
                if (prismResponse.TryGetProperty("versions", out var versionsArray))
                {
                    foreach (var verNode in versionsArray.EnumerateArray())
                    {
                        if (verNode.TryGetProperty("requires", out var reqArray))
                        {
                            foreach (var req in reqArray.EnumerateArray())
                            {
                                if (req.TryGetProperty("uid", out var uid) && uid.GetString() == "net.minecraft")
                                {
                                    var mcEquals = req.GetProperty("equals").GetString();
                                    if (mcEquals == mcVersion)
                                    {
                                        neoforgeVersion = verNode.GetProperty("version").GetString();
                                        break;
                                    }
                                }
                            }
                        }
                        if (neoforgeVersion != null) break;
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(neoforgeVersion))
                throw new Exception($"Could not find a valid NeoForge version for Minecraft {mcVersion}.");

            string url = $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{neoforgeVersion}/neoforge-{neoforgeVersion}-installer.jar";
            await DownloadFileWithProgressAsync(client, url, destinationPath, "Downloading NeoForge installer");
        }

        private async Task RunInstallerAsync(string javaExec, string workingDir, string installerPath)
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = javaExec,
                WorkingDirectory = workingDir,
                Arguments = "-Djava.awt.headless=true -Dforge.stdout=true -jar installer.jar --installServer",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            await Task.Run(() =>
            {
                using var proc = System.Diagnostics.Process.Start(startInfo);
                if (proc != null)
                {
                    proc.WaitForExit();
                    if (proc.ExitCode != 0)
                    {
                        throw new Exception($"Installer failed with exit code {proc.ExitCode}");
                    }
                }
            });

            // Clean up installer to save disk space
            try { File.Delete(installerPath); } catch { }
            try { File.Delete(Path.Combine(workingDir, "installer.jar.log")); } catch { }
        }

        private bool CanCreate()
        {
            return !HasErrors && 
                   AcceptEula && 
                   !IsCreating && 
                   !IsLoadingVersions && 
                   Name?.Length >= 3 &&
                   !string.IsNullOrWhiteSpace(SelectedVersion);
        }

        private async Task CancelAsync()
        {
            if (IsCreating)
            {
                bool confirmed = await PocketMC.App.Views.ConfirmationDialogWindow.ShowAsync(
                    "Cancel Download?",
                    "A server instance download is currently in progress. Navigating away or cancelling will abort the download and clean up temporary files.",
                    "Yes, Cancel Download");

                if (!confirmed) return;

                _loadVersionsCts?.Cancel();
                IsCreating = false;
                ProgressText = "Download cancelled.";
                
                try
                {
                    await _instanceService.DeleteInstanceAsync(Slugify(Name));
                }
                catch { }
            }

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
    }
}
