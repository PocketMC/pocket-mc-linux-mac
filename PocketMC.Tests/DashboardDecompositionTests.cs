using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.App.ViewModels;

namespace PocketMC.Tests
{
    public class DashboardDecompositionTests
    {
        [Fact]
        public void InstanceCardViewModel_InitializesWithCorrectMetadataAndStoppedState()
        {
            var instance = new ServerInstance
            {
                Id = Guid.NewGuid(),
                Name = "Test Server",
                EngineType = EngineType.Paper,
                EngineVersion = "1.20.4",
                MaxRamMb = 4096,
                ServerPort = 25565,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                LastPlayedAt = DateTime.UtcNow.AddHours(-3)
            };

            var card = new InstanceCardViewModel(instance);

            Assert.Equal("Test Server", card.Name);
            Assert.Equal("1.20.4", card.MinecraftVersion);
            Assert.Equal("Paper", card.ServerType);
            Assert.False(card.IsRunning);
            Assert.False(card.IsBusy);
            Assert.Equal("Stopped", card.StatusText);
            Assert.Equal("· · ·", card.CpuText);
            Assert.Equal("· · ·", card.RamText);
        }

        [Fact]
        public void DashboardMetricsViewModel_AppliesLiveMetricsWhenServerIsRunningAndResetsWhenStopped()
        {
            var instance = new ServerInstance
            {
                Id = Guid.NewGuid(),
                Name = "Running Server",
                EngineType = EngineType.Paper,
                EngineVersion = "1.20.4",
                MaxRamMb = 4096,
                ServerPort = 25565
            };

            var card = new InstanceCardViewModel(instance);
            var metricsVm = new DashboardMetricsViewModel();

            card.UpdateState("Online", DateTime.UtcNow.AddMinutes(-10));
            Assert.True(card.IsRunning);

            metricsVm.ApplyMetrics(card, cpuUsagePercent: 25.4, ramUsageMb: 2048, playerCount: 5);

            Assert.Equal("25%", card.CpuText);
            Assert.Equal("2.0 / 4 GB", card.RamText);
            Assert.Equal("5 / 20", card.PlayerStatus);

            card.UpdateState("Stopped");
            Assert.False(card.IsRunning);
            metricsVm.ApplyMetrics(card, cpuUsagePercent: 25.4, ramUsageMb: 2048, playerCount: 5);

            Assert.Equal("· · ·", card.CpuText);
            Assert.Equal("· · ·", card.RamText);
            Assert.Equal("· · ·", card.PlayerStatus);
        }

        [Theory]
        [InlineData("Running", true, "#10b981")]
        [InlineData("Online", true, "#10b981")]
        [InlineData("Starting", true, "#f59e0b")]
        [InlineData("SettingUp", true, "#f59e0b")]
        [InlineData("Installing", true, "#f59e0b")]
        [InlineData("Stopping", true, "#ef4444")]
        [InlineData("Crashed", false, "#ef4444")]
        [InlineData("Stopped", false, "#9ca3af")]
        public void InstanceCardViewModel_StatusMapping_MatchesExpectedRunningAndBrush(string status, bool expectedIsRunning, string expectedBrush)
        {
            var instance = new ServerInstance
            {
                Id = Guid.NewGuid(),
                Name = "Test Instance",
                EngineType = EngineType.Paper,
                EngineVersion = "1.20.4"
            };

            var card = new InstanceCardViewModel(instance);
            card.UpdateState(status);

            Assert.Equal(expectedIsRunning, card.IsRunning);
            Assert.Equal(expectedBrush, card.StatusBrush);
            Assert.Equal(expectedIsRunning, card.ShowRunningControls);
            Assert.Equal(!expectedIsRunning, card.ShowStoppedControls);
        }

        [Fact]
        public void InstanceCardViewModel_ExposesRunningBorderSkeletonAndPortIssueBadges()
        {
            var instance = new ServerInstance
            {
                Id = Guid.NewGuid(),
                Name = "Modpack CrossPlay",
                EngineType = EngineType.Paper,
                EngineVersion = "1.21.4",
                IsImportedFromModpack = true,
                ModpackName = "BetterMC"
            };

            var card = new InstanceCardViewModel(instance);

            Assert.Equal("Pin to Top", card.PinMenuHeader);
            Assert.True(card.IsImportedFromModpack);
            Assert.Equal("Modpack", card.ModpackBadgeText);
            Assert.False(card.ShowRunningBorder);

            card.SetPinned(true);
            Assert.Equal("Unpin from Top", card.PinMenuHeader);

            card.UpdateState("Online", DateTime.UtcNow);
            card.IsTunnelResolving = true;
            Assert.True(card.ShowRunningBorder);
            Assert.True(card.ShowPrimaryTunnelSkeleton);

            card.SetPortIssue("Port Conflict", "Port 25565 already in use");
            Assert.True(card.HasPortIssue);
            Assert.Equal("Port Conflict", card.PortIssueBadgeText);
            card.ClearPortIssue();
            Assert.False(card.HasPortIssue);

            card.UpdateVoiceChatTunnel("voice.playit.gg:19133");
            Assert.True(card.HasVoiceChatTunnelAddress);
        }

        [Fact]
        public async Task DashboardInstanceListViewModel_LoadsAndSortsPinnedInstancesFirst()
        {
            var unpinnedOlder = new ServerInstance
            {
                Id = Guid.NewGuid(),
                Name = "Unpinned Older",
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            };
            var unpinnedNewer = new ServerInstance
            {
                Id = Guid.NewGuid(),
                Name = "Unpinned Newer",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };
            var pinned = new ServerInstance
            {
                Id = Guid.NewGuid(),
                Name = "Pinned Server",
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                PinnedAt = DateTime.UtcNow.AddHours(-1)
            };

            var fakeService = new FakeInstanceService(new List<ServerInstance> { unpinnedOlder, unpinnedNewer, pinned });
            var listVm = new DashboardInstanceListViewModel(fakeService);

            Assert.True(listVm.IsLoading);

            await listVm.LoadInstancesAsync();

            Assert.False(listVm.IsLoading);
            Assert.Equal(3, listVm.Instances.Count);
            Assert.Equal("Pinned Server", listVm.Instances[0].Name);
            Assert.Equal("Unpinned Newer", listVm.Instances[1].Name);
            Assert.Equal("Unpinned Older", listVm.Instances[2].Name);
        }

        [Fact]
        public async Task DashboardActionsViewModel_StartAndStopServer_UpdatesCardStateCorrectly()
        {
            var instance = new ServerInstance
            {
                Id = Guid.NewGuid(),
                Name = "Action Server",
                Slug = "action-server",
                Path = "/tmp/action-server",
                EngineType = EngineType.Paper,
                EngineVersion = "1.20.4"
            };

            var fakeRunner = new FakeProcessRunner();
            var fakeService = new FakeInstanceService(new List<ServerInstance> { instance });
            var actionsVm = new DashboardActionsViewModel(fakeRunner, fakeService);
            var card = new InstanceCardViewModel(instance);

            await actionsVm.StartServerAsync(card);
            Assert.Equal("Online", card.StatusText);
            Assert.True(card.IsRunning);
            Assert.True(fakeRunner.IsRunning);

            await actionsVm.StopServerAsync(card);
            Assert.Equal("Stopped", card.StatusText);
            Assert.False(card.IsRunning);
            Assert.False(fakeRunner.IsRunning);
        }

        private class FakeProcessRunner : IProcessRunner
        {
            public bool IsRunning { get; private set; }
            public event Action<string, string>? StateChanged;

            public Task StartAsync(ServerInstance instance, bool isAutoRestart = false)
            {
                IsRunning = true;
                StateChanged?.Invoke(instance.Slug, "Online");
                return Task.CompletedTask;
            }

            public Task StopAsync(ServerInstance instance)
            {
                IsRunning = false;
                StateChanged?.Invoke(instance.Slug, "Stopped");
                return Task.CompletedTask;
            }

            public Task StopAllAsync()
            {
                IsRunning = false;
                return Task.CompletedTask;
            }

            public System.Collections.Generic.IReadOnlyList<string> GetRunningSlugs() => IsRunning ? new[] { "action-server" } : Array.Empty<string>();
            public Task SendCommandAsync(ServerInstance instance, string command) => Task.CompletedTask;
            public bool TryGetRunningInfo(string slug, out int pgid, out string state)
            {
                if (IsRunning)
                {
                    pgid = 1234;
                    state = "Online";
                    return true;
                }
                pgid = 0;
                state = "Stopped";
                return false;
            }
            public DateTime? GetSessionStartTime(string slug) => IsRunning ? DateTime.UtcNow.AddMinutes(-5) : null;
        }

        private class FakeInstanceService : IInstanceService
        {
            private readonly List<ServerInstance> _instances;

            public FakeInstanceService(List<ServerInstance> instances)
            {
                _instances = instances;
            }

            public Task<ServerInstance> CreateInstanceAsync(string name, EngineType engineType, string version) => throw new NotImplementedException();
            public Task DeleteInstanceAsync(string slug) => throw new NotImplementedException();
            public Task<ServerInstance> RenameInstanceAsync(string slug, string newName) => throw new NotImplementedException();
            public Task<ServerInstance> CloneInstanceAsync(string slug, string newName) => throw new NotImplementedException();
            public Task SaveInstanceAsync(ServerInstance instance) => Task.CompletedTask;
            public Task ExportInstanceAsync(string slug, string targetZipPath) => throw new NotImplementedException();
            public Task<ServerInstance?> GetInstanceAsync(string slug) => Task.FromResult(_instances.Find(i => i.Slug == slug));
            public Task<ServerInstance> ImportInstanceAsync(string sourceZipPath, string name) => throw new NotImplementedException();
            public Task<List<ServerInstance>> ListInstancesAsync() => Task.FromResult(new List<ServerInstance>(_instances));
        }
    }
}
