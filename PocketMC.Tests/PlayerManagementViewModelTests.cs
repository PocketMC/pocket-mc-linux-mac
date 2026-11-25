using System;
using System.IO;
using System.Text.Json;
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

                await vm.LoadListsAsync();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                // Verify initial online players load
                Assert.Contains("Player1", vm.OnlinePlayers);
                Assert.Contains("Player2", vm.OnlinePlayers);

                // Add to Whitelist
                vm.NewWhitelistPlayer = "WhitelistPlayer";
                await vm.AddWhitelistPlayerCommand.ExecuteAsync(null);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Contains("WhitelistPlayer", vm.Whitelist);

                // Remove from Whitelist
                await vm.RemoveWhitelistPlayerCommand.ExecuteAsync("WhitelistPlayer");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.DoesNotContain("WhitelistPlayer", vm.Whitelist);

                // Add Op
                vm.NewOpPlayer = "OpPlayer";
                await vm.AddOpPlayerCommand.ExecuteAsync(null);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Contains("OpPlayer", vm.Ops);

                // Remove Op
                await vm.RemoveOpPlayerCommand.ExecuteAsync("OpPlayer");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.DoesNotContain("OpPlayer", vm.Ops);

                // Ban Player
                vm.NewBanPlayer = "BannedPlayer";
                await vm.BanPlayerCommand.ExecuteAsync("BannedPlayer");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Contains("BannedPlayer", vm.BannedPlayers);

                // Unban Player
                await vm.UnbanPlayerCommand.ExecuteAsync("BannedPlayer");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
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
            PlayerManagementViewModel? vm = null;

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

                vm = new PlayerManagementViewModel(instService, playerService, processRunner);
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
                vm?.Dispose();
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public async Task PlayerManagementViewModel_PopulatesRichCardsBanMetadataAndTogglesWhitelist()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            PlayerManagementViewModel? vm = null;

            try
            {
                await File.WriteAllTextAsync(Path.Combine(tempDir, "server.properties"), "white-list=false\nmax-players=20\n");
                await File.WriteAllTextAsync(Path.Combine(tempDir, "banned-players.json"), JsonSerializer.Serialize(new[]
                {
                    new { name = "Griefer99", reason = "TNT spam", source = "Admin", expires = "forever", created = "2026-01-01" }
                }));

                var instance = new ServerInstance
                {
                    Name = "SMP",
                    Slug = "smp",
                    Path = tempDir,
                    EngineType = EngineType.Paper
                };

                var instService = new FakeInstanceService();
                var playerService = new FakePlayerService2();
                playerService.Online.Add("Steve");
                playerService.Ops.Add("Steve");
                playerService.Whitelist.Add("Steve");
                var processRunner = new FakeProcessRunner();

                vm = new PlayerManagementViewModel(instService, playerService, processRunner);
                vm.Initialize(instance);

                await vm.LoadListsAsync();
                try { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); } catch { }

                Assert.Single(vm.OnlinePlayerCards);
                Assert.Equal("Steve", vm.OnlinePlayerCards[0].Name);
                Assert.True(vm.OnlinePlayerCards[0].IsOp);

                Assert.Single(vm.BanListEntries);
                Assert.Equal("Griefer99", vm.BanListEntries[0].Name);
                Assert.Equal("TNT spam", vm.BanListEntries[0].Reason);
                Assert.Equal("Admin", vm.BanListEntries[0].BannedDisplay);

                Assert.Single(vm.WhitelistEntries);
                Assert.Equal("Steve", vm.WhitelistEntries[0].Name);

                Assert.False(vm.IsWhitelistEnabled);
                await vm.ToggleWhitelistCommand.ExecuteAsync(null);
                Assert.True(vm.IsWhitelistEnabled);
                string props = await File.ReadAllTextAsync(Path.Combine(tempDir, "server.properties"));
                Assert.Contains("white-list=true", props);
            }
            finally
            {
                vm?.Dispose();
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
}
