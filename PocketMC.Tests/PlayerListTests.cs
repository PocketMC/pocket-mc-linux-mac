using System;
using System.IO;
using System.Threading.Tasks;
using PocketMC.Core.Models;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests
{
    public class PlayerListTests : IDisposable
    {
        private readonly string _testTempDir;
        private readonly SettingsService _settingsService;
        private readonly ConsoleLogService _logService;
        private readonly PlayerService _playerService;

        public PlayerListTests()
        {
            _testTempDir = Path.Combine(Path.GetTempPath(), "PlayerListTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testTempDir);

            _settingsService = new SettingsService(_testTempDir);
            _settingsService.Settings.CustomDataRoot = _testTempDir;
            _settingsService.Load();

            _logService = new ConsoleLogService(_settingsService);
            _playerService = new PlayerService(_settingsService, _logService);
        }

        public void Dispose()
        {
            _playerService.Dispose();
            try
            {
                if (Directory.Exists(_testTempDir))
                {
                    Directory.Delete(_testTempDir, true);
                }
            }
            catch { }
        }

        [Fact]
        [Trait("Category", "Unit")]
        public async Task JavaPlayerSync_SavesOpsAndWhitelistJson()
        {
            var instance = new ServerInstance
            {
                Id = Guid.NewGuid(),
                Name = "Java Server",
                Slug = "java-server",
                Path = Path.Combine(_settingsService.GetInstancesDirectory(), "java-server"),
                EngineType = EngineType.VanillaJava,
                EngineVersion = "1.20"
            };
            Directory.CreateDirectory(instance.Path);

            // Add Op
            await _playerService.AddOpAsync(instance, "Sahaj");
            var ops = await _playerService.GetOpsAsync(instance);
            Assert.Single(ops);
            Assert.Equal("Sahaj", ops[0]);

            // Add Whitelist
            await _playerService.AddWhitelistAsync(instance, "Sahaj");
            var whitelisted = await _playerService.GetWhitelistAsync(instance);
            Assert.Single(whitelisted);
            Assert.Equal("Sahaj", whitelisted[0]);

            // Remove Op
            await _playerService.RemoveOpAsync(instance, "Sahaj");
            ops = await _playerService.GetOpsAsync(instance);
            Assert.Empty(ops);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public async Task PocketMinePlayerSync_SavesOpsAndWhitelistTxt()
        {
            var instance = new ServerInstance
            {
                Id = Guid.NewGuid(),
                Name = "PHP Server",
                Slug = "php-server",
                Path = Path.Combine(_settingsService.GetInstancesDirectory(), "php-server"),
                EngineType = EngineType.PocketMine,
                EngineVersion = "5.0"
            };
            Directory.CreateDirectory(instance.Path);

            // Add Op
            await _playerService.AddOpAsync(instance, "GamerPHP");
            var ops = await _playerService.GetOpsAsync(instance);
            Assert.Single(ops);
            Assert.Equal("GamerPHP", ops[0]);

            // Add Whitelist
            await _playerService.AddWhitelistAsync(instance, "GamerPHP");
            var whitelisted = await _playerService.GetWhitelistAsync(instance);
            Assert.Single(whitelisted);
            Assert.Equal("GamerPHP", whitelisted[0]);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public async Task ActivePlayerTracking_DetectsJoinAndLeaveFromLogs()
        {
            var instance = new ServerInstance
            {
                Id = Guid.NewGuid(),
                Name = "Track Server",
                Slug = "track-server",
                Path = Path.Combine(_settingsService.GetInstancesDirectory(), "track-server"),
                EngineType = EngineType.VanillaJava,
                EngineVersion = "1.20"
            };
            Directory.CreateDirectory(instance.Path);

            // Simulate log lines
            _logService.WriteLog(instance.Slug, "[23:59:59] [Server thread/INFO]: PlayerOne joined the game");
            _logService.WriteLog(instance.Slug, "[23:59:59] [Server thread/INFO]: PlayerTwo joined the game");

            var online = await _playerService.GetOnlinePlayersAsync(instance);
            Assert.Equal(2, online.Count);
            Assert.Contains("PlayerOne", online);
            Assert.Contains("PlayerTwo", online);

            // Simulate leave
            _logService.WriteLog(instance.Slug, "[23:59:59] [Server thread/INFO]: PlayerOne left the game");
            online = await _playerService.GetOnlinePlayersAsync(instance);
            Assert.Single(online);
            Assert.Equal("PlayerTwo", online[0]);
        }
    }
}
