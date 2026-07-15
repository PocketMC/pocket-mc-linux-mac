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
                    _ = LoadVersionsAsync();
                }
            }
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
        public IRelayCommand CancelCommand { get; }

        public NewInstanceViewModel(
            IInstanceService instanceService,
            IJavaService javaService,
            IPHPService phpService)
        {
            _instanceService = instanceService;
            _javaService = javaService;
            _phpService = phpService;

            CreateInstanceCommand = new AsyncRelayCommand(CreateInstanceAsync, CanCreate);
            CancelCommand = new RelayCommand(Cancel);

            // Initial load
            _ = LoadVersionsAsync();
        }

        public async Task LoadVersionsAsync()
        {
            IsLoadingVersions = true;
            var list = new List<string>();
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "PocketMC-App");

                if (SelectedEngine == EngineType.PocketMine)
                {
                    var response = await client.GetStringAsync("https://api.github.com/repos/pmmp/PocketMine-MP/releases");
                    using var doc = JsonDocument.Parse(response);
                    foreach (var release in doc.RootElement.EnumerateArray())
                    {
                        var tag = release.GetProperty("tag_name").GetString();
                        if (!string.IsNullOrEmpty(tag))
                        {
                            list.Add(tag);
                        }
                    }
                }
                else if (SelectedEngine == EngineType.Bedrock)
                {
                    var response = await client.GetStringAsync("https://raw.githubusercontent.com/kittizz/bedrock-server-downloads/main/bedrock-server-downloads.json");
                    using var doc = JsonDocument.Parse(response);
                    var releases = doc.RootElement.GetProperty("release");
                    foreach (var property in releases.EnumerateObject())
                    {
                        if (property.Value.TryGetProperty("linux", out var linuxProp))
                        {
                            list.Add(property.Name);
                        }
                    }
                }
                else if (SelectedEngine == EngineType.Paper)
                {
                    var response = await client.GetStringAsync("https://fill.papermc.io/v3/projects/paper");
                    using var doc = JsonDocument.Parse(response);
                    var versionsObj = doc.RootElement.GetProperty("versions");
                    int count = 0;

                    // The v3 API contains version keys mapped to arrays of sub-versions. Let's gather all versions.
                    var allVersions = new List<string>();
                    foreach (var prop in versionsObj.EnumerateObject())
                    {
                        foreach (var verNode in prop.Value.EnumerateArray())
                        {
                            var vStr = verNode.GetString();
                            if (!string.IsNullOrEmpty(vStr))
                            {
                                allVersions.Add(vStr);
                            }
                        }
                    }

                    // Sort them in reverse order using Version parser if possible, or string sort fallback
                    var sortedVersions = allVersions.Select(v => {
                        Version.TryParse(v.Split('-')[0], out var pv);
                        return new { Original = v, Parsed = pv ?? new Version(0, 0) };
                    })
                    .OrderByDescending(x => x.Parsed)
                    .ThenByDescending(x => x.Original)
                    .Select(x => x.Original);

                    foreach (var v in sortedVersions)
                    {
                        list.Add(v);
                        count++;
                        if (count >= 15) break;
                    }
                }
                else if (SelectedEngine == EngineType.Fabric)
                {
                    var response = await client.GetStringAsync("https://meta.fabricmc.net/v2/versions/game");
                    using var doc = JsonDocument.Parse(response);
                    int count = 0;
                    foreach (var ver in doc.RootElement.EnumerateArray())
                    {
                        var id = ver.GetProperty("version").GetString();
                        var stable = ver.GetProperty("stable").GetBoolean();
                        if (stable && !string.IsNullOrEmpty(id))
                        {
                            list.Add(id);
                            count++;
                            if (count >= 15) break;
                        }
                    }
                }
                else if (SelectedEngine == EngineType.Forge)
                {
                    var response = await client.GetStringAsync("https://meta.prismlauncher.org/v1/net.minecraftforge/index.json");
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
                                        mcVersionsSet.Add(mcEquals);
                                    }
                                    break;
                                }
                            }
                        }
                    }

                    var sortedMcVersions = mcVersionsSet.Select(v => {
                        Version.TryParse(v, out var pv);
                        return new { Original = v, Parsed = pv ?? new Version(0, 0) };
                    })
                    .OrderByDescending(x => x.Parsed)
                    .ThenByDescending(x => x.Original)
                    .Select(x => x.Original);

                    int count = 0;
                    foreach (var v in sortedMcVersions)
                    {
                        list.Add(v);
                        count++;
                        if (count >= 15) break;
                    }
                }
                else if (SelectedEngine == EngineType.NeoForge)
                {
                    var response = await client.GetStringAsync("https://meta.prismlauncher.org/v1/net.neoforged/index.json");
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
                                        mcVersionsSet.Add(mcEquals);
                                    }
                                    break;
                                }
                            }
                        }
                    }

                    var sortedMcVersions = mcVersionsSet.Select(v => {
                        Version.TryParse(v, out var pv);
                        return new { Original = v, Parsed = pv ?? new Version(0, 0) };
                    })
                    .OrderByDescending(x => x.Parsed)
                    .ThenByDescending(x => x.Original)
                    .Select(x => x.Original);

                    int count = 0;
                    foreach (var v in sortedMcVersions)
                    {
                        list.Add(v);
                        count++;
                        if (count >= 15) break;
                    }
                }
                else // VanillaJava
                {
                    var response = await client.GetStringAsync("https://launchermeta.mojang.com/mc/game/version_manifest.json");
                    using var doc = JsonDocument.Parse(response);
                    var manifestList = doc.RootElement.GetProperty("versions");
                    int count = 0;
                    foreach (var ver in manifestList.EnumerateArray())
                    {
                        var id = ver.GetProperty("id").GetString();
                        var type = ver.GetProperty("type").GetString();
                        if (type == "release" && !string.IsNullOrEmpty(id))
                        {
                            list.Add(id);
                            count++;
                            if (count >= 15) break;
                        }
                    }
                }

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
                        list.Add("1.21");
                        list.Add("1.20.4");
                        list.Add("1.20.1");
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    Versions.Clear();
                    foreach (var v in list) Versions.Add(v);
                    SelectedVersion = Versions.FirstOrDefault() ?? string.Empty;
                });
            }
            catch (Exception ex)
            {
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
                    list.Add("1.21");
                    list.Add("1.20.4");
                }

                Dispatcher.UIThread.Post(() =>
                {
                    Versions.Clear();
                    foreach (var v in list) Versions.Add(v);
                    SelectedVersion = Versions.FirstOrDefault() ?? string.Empty;
                    ProgressText = $"Error loading versions (using fallbacks): {ex.Message}";
                });
            }
            finally
            {
                IsLoadingVersions = false;
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

                Progress = 1.0;
                ProgressText = "Created successfully!";
                await Task.Delay(1000);

                var mainVM = App.Services.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
                if (mainVM != null)
                {
                    var dashVM = App.Services.GetService(typeof(DashboardViewModel)) as DashboardViewModel;
                    if (dashVM != null)
                    {
                        await dashVM.LoadInstancesAsync();
                        mainVM.CurrentViewModel = dashVM;
                    }
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
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
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

            string url = $"https://api.papermc.io/v2/projects/paper/versions/{mcVersion}";
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

            string downloadUrl = $"https://api.papermc.io/v2/projects/paper/versions/{mcVersion}/builds/{maxBuild}/downloads/paper-{mcVersion}-{maxBuild}.jar";
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

        private void Cancel()
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
    }
}
