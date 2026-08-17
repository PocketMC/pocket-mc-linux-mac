using System;
using System.IO;
using System.Threading.Tasks;
using PocketMC.App.ViewModels;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using Xunit;

namespace PocketMC.Tests
{
    public class FakeSettingsService : ISettingsService
    {
        public Settings Settings { get; set; } = new Settings();
        public string GetSettingsDirectory() => Path.GetTempPath();
        public string GetDataRoot() => Settings.CustomDataRoot ?? GetDefaultDataRoot();
        public string GetDefaultDataRoot() => Path.Combine(Path.GetTempPath(), ".pocketmc");
        public void SetDataRoot(string path)
        {
            Settings.CustomDataRoot = path;
            Settings.HasCompletedInitialSetup = true;
        }
        public string GetInstancesDirectory() => Path.Combine(GetDataRoot(), "Instances");
        public string GetBackupsDirectory() => Path.Combine(GetDataRoot(), "Backups");
        public string GetDownloadsDirectory() => Path.Combine(GetDataRoot(), "Downloads");
        public string GetCacheDirectory() => Path.Combine(GetDataRoot(), "Cache");
        public string GetLogsDirectory() => Path.Combine(GetDataRoot(), "Logs");
        public void Load() { }
        public void Save() { }
    }

    public class ServerSettingsViewModelTests
    {
        [Fact]
        public async Task ServerSettingsViewModel_LoadSaveCancelDraftModel()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                var propPath = Path.Combine(tempDir, "server.properties");
                File.WriteAllText(propPath, "motd=Initial MOTD\ndifficulty=easy\nserver-port=25565");

                var instance = new ServerInstance
                {
                    Name = "Settings Test",
                    Slug = "settings-test",
                    Path = tempDir,
                    JvmArgs = "-Xmx2G"
                };

                var instService = new FakeInstanceService();
                var settingsService = new FakeSettingsService();

                var vm = new ServerSettingsViewModel(instService, settingsService);
                vm.Initialize(instance);

                // 1. Verify initial load
                Assert.Equal("Initial MOTD", vm.Motd);
                Assert.Equal("easy", vm.Difficulty);
                Assert.Equal(25565, vm.ServerPort);
                Assert.Equal(2, vm.MaxRamGb);
                Assert.False(vm.HasUnsavedChanges);

                // 2. Edit properties (Stages in Draft)
                vm.Motd = "New MOTD";
                vm.Difficulty = "hard";
                vm.MaxRamGb = 4;
                Assert.True(vm.HasUnsavedChanges);

                // Read file to verify disk wasn't updated yet
                var diskContent = File.ReadAllText(propPath);
                Assert.Contains("motd=Initial MOTD", diskContent);

                // 3. Cancel Edits (should revert)
                vm.CancelCommand.Execute(null);
                Assert.Equal("Initial MOTD", vm.Motd);
                Assert.Equal("easy", vm.Difficulty);
                Assert.Equal(2, vm.MaxRamGb);
                Assert.False(vm.HasUnsavedChanges);

                // 4. Save Edits (should write to disk)
                vm.Motd = "Saved MOTD";
                vm.Difficulty = "normal";
                vm.MaxRamGb = 8;
                Assert.True(vm.HasUnsavedChanges);

                await vm.SaveCommand.ExecuteAsync(null);
                Assert.False(vm.HasUnsavedChanges);

                var diskContentAfterSave = File.ReadAllText(propPath);
                Assert.Contains("motd=Saved MOTD", diskContentAfterSave);
                Assert.Contains("difficulty=normal", diskContentAfterSave);
                Assert.Contains("-Xmx8G", instance.JvmArgs);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
}
