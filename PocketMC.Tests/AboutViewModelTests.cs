using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PocketMC.App.ViewModels;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.WhatsNew;
using Xunit;

namespace PocketMC.Tests;

public class AboutViewModelTests
{
    private class FakeSettingsService : ISettingsService
    {
        public Settings Settings { get; set; } = new();
        public void Load() { }
        public void Save() { }
        public string GetSettingsDirectory() => "/tmp/pocketmc";
        public string GetDataRoot() => "/tmp/pocketmc/data";
        public string GetDefaultDataRoot() => "/tmp/pocketmc/data";
        public void SetDataRoot(string path) { Settings.CustomDataRoot = path; }
        public string GetInstancesDirectory() => "/tmp/pocketmc/data/instances";
        public string GetBackupsDirectory() => "/tmp/pocketmc/data/backups";
        public string GetDownloadsDirectory() => "/tmp/pocketmc/downloads";
        public string GetCacheDirectory() => "/tmp/pocketmc/cache";
        public string GetLogsDirectory() => "/tmp/pocketmc/logs";
    }

    private class FakeInstanceService : IInstanceService
    {
        public Task<ServerInstance> CreateInstanceAsync(string name, EngineType engineType, string version) =>
            Task.FromResult(new ServerInstance { Name = name, EngineType = engineType, EngineVersion = version });
        public Task DeleteInstanceAsync(string slug) => Task.CompletedTask;
        public Task<ServerInstance> RenameInstanceAsync(string slug, string newName) =>
            Task.FromResult(new ServerInstance { Name = newName, Slug = slug });
        public Task<ServerInstance> CloneInstanceAsync(string slug, string newName) =>
            Task.FromResult(new ServerInstance { Name = newName, Slug = slug + "-clone" });
        public Task SaveInstanceAsync(ServerInstance instance) => Task.CompletedTask;
        public Task ExportInstanceAsync(string slug, string targetZipPath) => Task.CompletedTask;
        public Task<ServerInstance> ImportInstanceAsync(string sourceZipPath, string name) =>
            Task.FromResult(new ServerInstance { Name = name });
        public Task<List<ServerInstance>> ListInstancesAsync() =>
            Task.FromResult(new List<ServerInstance>());
    }

    [Fact]
    public void AboutViewModel_HasExpectedCommandsAndUrls()
    {
        var settingsService = new FakeSettingsService();
        var instanceService = new FakeInstanceService();
        var whatsNewService = new WhatsNewService(settingsService);

        var vm = new AboutViewModel(settingsService, instanceService, whatsNewService);

        Assert.NotNull(vm.OpenWebsiteCommand);
        Assert.NotNull(vm.OpenStudioCommand);
        Assert.NotNull(vm.OpenDivyCommand);
        Assert.NotNull(vm.OpenSahajCommand);
        Assert.NotNull(vm.OpenJohndinglesinCommand);
        Assert.NotNull(vm.OpenNexViceCommand);
        Assert.NotNull(vm.OpenWhatsNewCommand);
        Assert.Equal("/tmp/pocketmc/data", vm.CurrentDataRoot);
    }
}
