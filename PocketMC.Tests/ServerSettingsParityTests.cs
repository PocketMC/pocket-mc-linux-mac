using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using PocketMC.App.ViewModels.Settings;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Mods;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests;

public class ServerSettingsParityTests
{
    [Fact]
    public async Task WorldManager_ImportsNestedMcworldZipAndResolvesLevelDatRoot()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "pmc_world_" + Guid.NewGuid().ToString("N"));
        string instanceDir = Path.Combine(tempRoot, "server");
        string archivePath = Path.Combine(tempRoot, "skyblock.mcworld");
        string targetWorldDir = Path.Combine(instanceDir, "worlds", "Bedrock level");
        Directory.CreateDirectory(instanceDir);

        try
        {
            using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                var levelEntry = zip.CreateEntry("wrapper_folder/MyMap/level.dat");
                await using (var s = levelEntry.Open())
                await using (var w = new StreamWriter(s))
                {
                    await w.WriteAsync("BEDROCK-LEVEL-DATA");
                }

                var txtEntry = zip.CreateEntry("wrapper_folder/MyMap/levelname.txt");
                await using (var s = txtEntry.Open())
                await using (var w = new StreamWriter(s))
                {
                    await w.WriteAsync("SkyBlock");
                }
            }

            var worldManager = new WorldManager();
            await worldManager.ImportWorldZipAsync(archivePath, targetWorldDir);

            Assert.True(File.Exists(Path.Combine(targetWorldDir, "level.dat")));
            Assert.True(File.Exists(Path.Combine(targetWorldDir, "levelname.txt")));
            Assert.Equal("BEDROCK-LEVEL-DATA", await File.ReadAllTextAsync(Path.Combine(targetWorldDir, "level.dat")));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    [Fact]
    public async Task JavaModMetadataService_ExtractsFabricAndPluginMetadataFromJar()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "pmc_modmeta_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        string fabricJar = Path.Combine(tempRoot, "lithium-0.12.jar");
        string pluginJar = Path.Combine(tempRoot, "essentials.jar");

        try
        {
            using (var zip = ZipFile.Open(fabricJar, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("fabric.mod.json");
                await using var w = new StreamWriter(entry.Open());
                await w.WriteAsync("{\"id\":\"lithium\",\"version\":\"0.12.1\",\"name\":\"Lithium\",\"environment\":\"*\",\"depends\":{\"fabric-api\":\"*\",\"cloth-config\":\"*\"}}");
            }

            using (var zip = ZipFile.Open(pluginJar, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("plugin.yml");
                await using var w = new StreamWriter(entry.Open());
                await w.WriteAsync("name: EssentialsX\nversion: 2.20.1\ndescription: Essential commands\n");
            }

            var fabricMeta = JavaModMetadataService.ScanJar(fabricJar, "Fabric");
            Assert.Equal("lithium", fabricMeta.ModId);
            Assert.Equal("Lithium", fabricMeta.DisplayName);
            Assert.Equal("0.12.1", fabricMeta.Version);
            Assert.Equal("Fabric", fabricMeta.LoaderType);
            Assert.Contains("cloth-config", fabricMeta.RequiredDependencies);

            var pluginMeta = JavaModMetadataService.ScanJar(pluginJar, "Paper");
            Assert.Equal("EssentialsX", pluginMeta.DisplayName);
            Assert.Equal("2.20.1", pluginMeta.Version);
            Assert.Equal("Plugin", pluginMeta.LoaderType);
            Assert.True(pluginMeta.HasPluginMetadata);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    [Fact]
    public async Task BedrockAddonInstaller_InstallsTogglesAndRemovesMcpack()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "pmc_bdsaddon_" + Guid.NewGuid().ToString("N"));
        string serverDir = Path.Combine(tempRoot, "bedrock");
        string addonPath = Path.Combine(tempRoot, "custom.mcpack");
        Directory.CreateDirectory(serverDir);
        string uuid = "11111111-2222-3333-4444-555555555555";

        try
        {
            using (var zip = ZipFile.Open(addonPath, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("manifest.json");
                await using var w = new StreamWriter(entry.Open());
                await w.WriteAsync($$"""
                {
                  "format_version": 2,
                  "header": {
                    "name": "Cool Behavior",
                    "description": "Adds cool mobs",
                    "uuid": "{{uuid}}",
                    "version": [1, 2, 0]
                  },
                  "modules": [
                    { "type": "data", "uuid": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", "version": [1, 0, 0] }
                  ]
                }
                """);
            }

            var installer = new BedrockAddonInstaller();
            var installed = await installer.InstallAddonAsync(addonPath, serverDir);
            Assert.Single(installed);
            Assert.Equal("Cool Behavior", installed[0].Name);
            Assert.Equal(BedrockPackType.Behavior, installed[0].PackType);
            Assert.True(installed[0].IsEnabled);

            await installer.SetPackEnabledAsync(serverDir, uuid, BedrockPackType.Behavior, false);
            var packsAfterDisable = installer.GetPacks(serverDir);
            Assert.Single(packsAfterDisable);
            Assert.False(packsAfterDisable[0].IsEnabled);

            await installer.DeletePackAsync(serverDir, uuid, BedrockPackType.Behavior);
            Assert.Empty(installer.GetPacks(serverDir));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    [Fact]
    public async Task SettingsAddonsAndBackupsVM_PopulateMetadataAndKpiStats()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "pmc_addonsvm_" + Guid.NewGuid().ToString("N"));
        string serverDir = Path.Combine(tempRoot, "server");
        string modsDir = Path.Combine(serverDir, "mods");
        string backupsDir = Path.Combine(serverDir, "backups");
        Directory.CreateDirectory(modsDir);
        Directory.CreateDirectory(backupsDir);

        try
        {
            string modJar = Path.Combine(modsDir, "sodium.jar");
            using (var zip = ZipFile.Open(modJar, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("fabric.mod.json");
                await using var w = new StreamWriter(entry.Open());
                await w.WriteAsync("{\"id\":\"sodium\",\"version\":\"0.6.0\",\"name\":\"Sodium\"}");
            }

            string backupZip = Path.Combine(backupsDir, "backup_1.zip");
            await File.WriteAllBytesAsync(backupZip, new byte[2048]);
            var manifest = BackupManifest.Load(serverDir);
            manifest.Entries.Add(new BackupMetadataEntry
            {
                FileName = "backup_1.zip",
                Version = 1,
                SizeBytes = 2048,
                CreatedAtUtc = DateTime.UtcNow,
                Trigger = BackupTrigger.Manual,
                Label = "Pre-upgrade"
            });
            manifest.Save(serverDir);

            var dialog = new FakeDialogServiceForSettings();
            var instance = new ServerInstance
            {
                Id = Guid.NewGuid(),
                Name = "FabricSMP",
                Slug = "fabricsmp",
                Path = serverDir,
                EngineType = EngineType.Fabric,
                EngineVersion = "1.21.4"
            };

            var addonsVm = new SettingsAddonsVM(dialog, () => { });
            addonsVm.SetInstance(instance);

            Assert.True(addonsVm.SupportsMods);
            Assert.Single(addonsVm.Mods);
            Assert.Equal("Sodium", addonsVm.Mods[0].DisplayName);
            Assert.Equal("0.6.0", addonsVm.Mods[0].Version);
            Assert.Equal("Fabric", addonsVm.Mods[0].LoaderType);

            var backupsVm = new SettingsBackupsVM(dialog, () => { });
            backupsVm.SetInstance(instance);

            Assert.Single(backupsVm.BackupList);
            Assert.Equal("#1", backupsVm.BackupList[0].VersionText);
            Assert.False(string.IsNullOrWhiteSpace(backupsVm.TotalBackupsSizeText));
            Assert.False(string.IsNullOrWhiteSpace(backupsVm.DiskSpaceText));

            await File.WriteAllTextAsync(Path.Combine(tempRoot, "dummy"), "x");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    [Fact]
    public async Task BedrockSnapshotService_StageAsync_CopiesConfirmedSnapshotLengthAndResumesSaving()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "pmc_bdssnap_" + Guid.NewGuid().ToString("N"));
        string serverDir = Path.Combine(tempRoot, "server");
        string worldDir = Path.Combine(serverDir, "worlds", "Bedrock level");
        string stageDir = Path.Combine(tempRoot, "stage");
        Directory.CreateDirectory(worldDir);
        await File.WriteAllTextAsync(Path.Combine(worldDir, "level.dat"), "world-extra");

        try
        {
            var instance = new ServerInstance
            {
                Id = Guid.NewGuid(),
                Name = "BedrockSrv",
                Slug = "bedrock-srv",
                Path = serverDir,
                EngineType = EngineType.Bedrock,
                EngineVersion = "1.21.50"
            };

            var logService = new ConsoleLogService(new FakeSettingsService());
            var runner = new FakeBedrockProcessRunner(logService);

            string stagedWorld = await BedrockSnapshotService.StageAsync(
                runner,
                logService,
                instance,
                worldDir,
                stageDir);

            Assert.Equal("world", await File.ReadAllTextAsync(Path.Combine(stagedWorld, "level.dat")));
            Assert.Equal(new[] { "save hold", "save query", "save resume" }, runner.Commands);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    [Theory]
    [InlineData("../outside:1")]
    [InlineData("worlds/Bedrock level/../../outside:1")]
    [InlineData("worlds/Bedrock level/level.dat:999")]
    public async Task BedrockSnapshotService_StageAsync_RejectsUnsafeOrTruncatedSnapshotAndResumes(string fileList)
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "pmc_bdsreject_" + Guid.NewGuid().ToString("N"));
        string serverDir = Path.Combine(tempRoot, "server");
        string worldDir = Path.Combine(serverDir, "worlds", "Bedrock level");
        string stageDir = Path.Combine(tempRoot, "stage");
        Directory.CreateDirectory(worldDir);
        await File.WriteAllTextAsync(Path.Combine(worldDir, "level.dat"), "world");

        try
        {
            var instance = new ServerInstance
            {
                Id = Guid.NewGuid(),
                Name = "BedrockSrv",
                Slug = "bedrock-srv",
                Path = serverDir,
                EngineType = EngineType.Bedrock,
                EngineVersion = "1.21.50"
            };

            var logService = new ConsoleLogService(new FakeSettingsService());
            var runner = new FakeBedrockProcessRunner(logService, fileList);

            var error = await Record.ExceptionAsync(() => BedrockSnapshotService.StageAsync(
                runner,
                logService,
                instance,
                worldDir,
                stageDir));

            Assert.True(error is InvalidDataException or EndOfStreamException, error?.ToString() ?? "Unsafe snapshot was accepted.");
            Assert.Equal("save resume", runner.Commands[^1]);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    [Fact]
    public async Task ServerSettingsViewModel_SelectTabAndCloudBackups_ToggleActiveTab()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "pmc_ssvm_tabs_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var instance = new ServerInstance
            {
                Id = Guid.NewGuid(),
                Name = "PaperSMP",
                Slug = "papersmp",
                Path = tempRoot,
                EngineType = EngineType.Paper,
                EngineVersion = "1.21.4"
            };

            var settingsService = new FakeSettingsService();
            var instanceService = new FakeInstanceService();

            var vm = new PocketMC.App.ViewModels.ServerSettingsViewModel(
                instanceService,
                settingsService,
                cloudProviders: Array.Empty<ICloudBackupProvider>());
            vm.Initialize(instance);

            Assert.NotNull(vm.CloudBackups);
            Assert.True(vm.IsTab0Selected);

            vm.SelectTabCommand.Execute("5");
            Assert.Equal(5, vm.SelectedTabIndex);
            Assert.False(vm.IsTab0Selected);
            Assert.True(vm.IsTab5Selected);

            vm.SelectTabCommand.Execute("8");
            Assert.Equal(8, vm.SelectedTabIndex);
            Assert.False(vm.IsTab5Selected);
            Assert.True(vm.IsTab8Selected);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    private sealed class FakeBedrockProcessRunner : IProcessRunner
    {
        private readonly IConsoleLogService _logService;
        private readonly string _queryResponseLine;
        public System.Collections.Generic.List<string> Commands { get; } = new();

        public FakeBedrockProcessRunner(IConsoleLogService logService, string queryResponseLine = "worlds/Bedrock level/level.dat:5")
        {
            _logService = logService;
            _queryResponseLine = queryResponseLine;
        }

        public event Action<string, string>? StateChanged
        {
            add { }
            remove { }
        }
        public Task StartAsync(ServerInstance instance, bool isAutoRestart = false) => Task.CompletedTask;
        public Task StopAsync(ServerInstance instance) => Task.CompletedTask;
        public Task StopAllAsync() => Task.CompletedTask;
        public System.Collections.Generic.IReadOnlyList<string> GetRunningSlugs() => new[] { "bedrock-srv" };
        public bool TryGetRunningInfo(string slug, out int pgid, out string state)
        {
            pgid = 1234;
            state = "Running";
            return string.Equals(slug, "bedrock-srv", StringComparison.Ordinal);
        }
        public DateTime? GetSessionStartTime(string slug) => DateTime.UtcNow;

        public Task SendCommandAsync(ServerInstance instance, string command)
        {
            Commands.Add(command);
            if (command == "save query")
            {
                _logService.WriteLog(instance.Slug, "Data saved. Files are now ready to be copied.");
                _logService.WriteLog(instance.Slug, _queryResponseLine);
            }
            else if (command == "save resume")
            {
                _logService.WriteLog(instance.Slug, "Changes to the level are resumed.");
            }
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDialogServiceForSettings : IDialogService
    {
        public void ShowMessage(string title, string message, DialogType type = DialogType.Information) { }
        public Task<DialogResult> ShowDialogAsync(
            string title,
            string message,
            DialogType type = DialogType.Information,
            bool showCancel = false,
            string? primaryButtonText = null,
            string? secondaryButtonText = null,
            string? cancelButtonText = null,
            string? linkText = null,
            string? linkUrl = null)
            => Task.FromResult(DialogResult.Ok);
        public Task<string?> OpenFileDialogAsync(string title, string filter = "All Files (*.*)|*.*") => Task.FromResult<string?>(null);
        public Task<string[]> OpenFilesDialogAsync(string title, string filter = "All Files (*.*)|*.*") => Task.FromResult(Array.Empty<string>());
        public Task<string?> OpenFolderDialogAsync(string title) => Task.FromResult<string?>(null);
        public Task<(string? Username, string? Password)> PromptCredentialsAsync(string title, string message, bool askUsername, bool askPassword)
            => Task.FromResult<(string?, string?)>((null, null));
        public Task ShowProgressDialogAsync(string title, string message, Func<IProgress<double>, Task> action) => action(new Progress<double>());
        public Task ShowProgressDialogAsync(string title, string message, Func<IProgress<ProgressDialogUpdate>, Task> action) => action(new Progress<ProgressDialogUpdate>());
    }
}
