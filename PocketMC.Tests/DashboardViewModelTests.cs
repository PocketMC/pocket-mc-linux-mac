using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PocketMC.App.ViewModels;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests
{
    public class FakeInstanceService : IInstanceService
    {
        public List<ServerInstance> Instances { get; set; } = new();

        public Task<ServerInstance> CreateInstanceAsync(string name, EngineType engineType, string version) => Task.FromResult(new ServerInstance());
        public Task DeleteInstanceAsync(string slug) => Task.CompletedTask;
        public Task<ServerInstance> RenameInstanceAsync(string slug, string newName) => Task.FromResult(new ServerInstance());
        public Task<ServerInstance> CloneInstanceAsync(string slug, string newName) => Task.FromResult(new ServerInstance());
        public Task ExportInstanceAsync(string slug, string targetZipPath) => Task.CompletedTask;
        public Task<ServerInstance> ImportInstanceAsync(string sourceZipPath, string name) => Task.FromResult(new ServerInstance());
        public Task<List<ServerInstance>> ListInstancesAsync() => Task.FromResult(Instances);
    }

    public class FakeProcessRunner : IProcessRunner
    {
        public Dictionary<string, (int Pgid, string State)> RunningInfo { get; set; } = new();
        public event Action<string, string>? StateChanged;

        public Task StartAsync(ServerInstance instance, bool isAutoRestart = false) => Task.CompletedTask;
        public Task StopAsync(ServerInstance instance) => Task.CompletedTask;
        public Task SendCommandAsync(ServerInstance instance, string command) => Task.CompletedTask;

        public bool TryGetRunningInfo(string slug, out int pgid, out string state)
        {
            if (RunningInfo.TryGetValue(slug, out var info))
            {
                pgid = info.Pgid;
                state = info.State;
                return true;
            }
            pgid = 0;
            state = "Stopped";
            return false;
        }

        public void TriggerStateChange(string slug, string state)
        {
            StateChanged?.Invoke(slug, state);
        }
    }

    public class FakePlayerService : IPlayerService
    {
        public List<string> Players { get; set; } = new();

        public Task<List<string>> GetOpsAsync(ServerInstance instance) => Task.FromResult(new List<string>());
        public Task AddOpAsync(ServerInstance instance, string username) => Task.CompletedTask;
        public Task RemoveOpAsync(ServerInstance instance, string username) => Task.CompletedTask;
        public Task<List<string>> GetWhitelistAsync(ServerInstance instance) => Task.FromResult(new List<string>());
        public Task AddWhitelistAsync(ServerInstance instance, string username) => Task.CompletedTask;
        public Task RemoveWhitelistAsync(ServerInstance instance, string username) => Task.CompletedTask;
        public Task<List<string>> GetOnlinePlayersAsync(ServerInstance instance) => Task.FromResult(Players);
    }

    public class DashboardViewModelTests
    {
        [Fact]
        public async Task DashboardViewModel_LoadsInstancesOnCreation()
        {
            var instanceService = new FakeInstanceService();
            var instance = new ServerInstance { Name = "Test Server", Slug = "test-server", EngineType = EngineType.VanillaJava };
            instanceService.Instances.Add(instance);

            var processRunner = new FakeProcessRunner();
            var playerService = new FakePlayerService();
            var metricsTracker = new ProcessMetricsTracker();
            var themeManager = new ThemeManager();

            using var vm = new DashboardViewModel(instanceService, processRunner, playerService, metricsTracker, themeManager);

            await Task.Delay(100);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.True(vm.HasInstances);
            Assert.Single(vm.Instances);
            Assert.Equal("test-server", vm.SelectedInstance?.Slug);
        }

        [Fact]
        public async Task DashboardViewModel_BindsCommandsAndHandlesStateChanges()
        {
            var instanceService = new FakeInstanceService();
            var instance = new ServerInstance { Name = "Test Server", Slug = "test-server", EngineType = EngineType.VanillaJava };
            instanceService.Instances.Add(instance);

            var processRunner = new FakeProcessRunner();
            var playerService = new FakePlayerService();
            var metricsTracker = new ProcessMetricsTracker();
            var themeManager = new ThemeManager();

            using var vm = new DashboardViewModel(instanceService, processRunner, playerService, metricsTracker, themeManager);

            await Task.Delay(100);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal("Stopped", vm.ServerState);

            processRunner.TriggerStateChange("test-server", "Running");
            await Task.Delay(50); 
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal("Running", vm.ServerState);
        }
    }
}
