using System;
using System.IO;
using System.Threading.Tasks;
using PocketMC.App.ViewModels;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using Xunit;

namespace PocketMC.Tests
{
    public class FakePlayerService2 : IPlayerService
    {
        public List<string> Ops { get; } = new();
        public List<string> Whitelist { get; } = new();
        public List<string> Online { get; } = new();

        public Task<List<string>> GetOpsAsync(ServerInstance instance) => Task.FromResult(Ops);
        public Task AddOpAsync(ServerInstance instance, string username)
        {
            Ops.Add(username);
            return Task.CompletedTask;
        }
        public Task RemoveOpAsync(ServerInstance instance, string username)
        {
            Ops.Remove(username);
            return Task.CompletedTask;
        }

        public Task<List<string>> GetWhitelistAsync(ServerInstance instance) => Task.FromResult(Whitelist);
        public Task AddWhitelistAsync(ServerInstance instance, string username)
        {
            Whitelist.Add(username);
            return Task.CompletedTask;
        }
        public Task RemoveWhitelistAsync(ServerInstance instance, string username)
        {
            Whitelist.Remove(username);
            return Task.CompletedTask;
        }

        public Task<List<string>> GetOnlinePlayersAsync(ServerInstance instance) => Task.FromResult(Online);
    }

    public class PlayerManagementViewModelTests
    {
        [Fact]
        public async Task PlayerManagementViewModel_KickBanOpWhitelistCommands()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                var instance = new ServerInstance
                {
                    Name = "Player Test",
                    Slug = "player-test",
                    Path = tempDir
                };

                var instService = new FakeInstanceService();
                var playerService = new FakePlayerService2();
                var processRunner = new FakeProcessRunner();

                playerService.Online.Add("Player1");
                playerService.Online.Add("Player2");

                using var vm = new PlayerManagementViewModel(instService, playerService, processRunner);
                vm.Initialize(instance);

                // Verify initial online players load
                Assert.Contains("Player1", vm.OnlinePlayers);
                Assert.Contains("Player2", vm.OnlinePlayers);

                // Add to Whitelist
                vm.NewWhitelistPlayer = "WhitelistPlayer";
                await vm.AddWhitelistPlayerCommand.ExecuteAsync(null);
                Assert.Contains("WhitelistPlayer", vm.Whitelist);

                // Remove from Whitelist
                await vm.RemoveWhitelistPlayerCommand.ExecuteAsync("WhitelistPlayer");
                Assert.DoesNotContain("WhitelistPlayer", vm.Whitelist);

                // Add Op
                vm.NewOpPlayer = "OpPlayer";
                await vm.AddOpPlayerCommand.ExecuteAsync(null);
                Assert.Contains("OpPlayer", vm.Ops);

                // Remove Op
                await vm.RemoveOpPlayerCommand.ExecuteAsync("OpPlayer");
                Assert.DoesNotContain("OpPlayer", vm.Ops);

                // Ban Player
                vm.NewBanPlayer = "BannedPlayer";
                await vm.BanPlayerCommand.ExecuteAsync("BannedPlayer");
                Assert.Contains("BannedPlayer", vm.BannedPlayers);

                // Unban Player
                await vm.UnbanPlayerCommand.ExecuteAsync("BannedPlayer");
                Assert.DoesNotContain("BannedPlayer", vm.BannedPlayers);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public async Task PlayerManagementViewModel_WatcherReloadsLists()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                var instance = new ServerInstance
                {
                    Name = "Player Watcher Test",
                    Slug = "player-watcher-test",
                    Path = tempDir
                };

                var instService = new FakeInstanceService();
                var playerService = new FakePlayerService2();
                var processRunner = new FakeProcessRunner();

                using var vm = new PlayerManagementViewModel(instService, playerService, processRunner);
                vm.Initialize(instance);

                // Confirm initially empty whitelist
                Assert.Empty(vm.Whitelist);

                // Externally write banned-players.txt
                var banPath = Path.Combine(tempDir, "banned-players.txt");
                File.WriteAllText(banPath, "ExternalBannedPlayer\n");

                // Call LoadLists manually to simulate what watcher does on background thread
                vm.LoadLists();

                Assert.Contains("ExternalBannedPlayer", vm.BannedPlayers);
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
