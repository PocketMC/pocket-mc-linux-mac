using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;
using PocketMC.RemoteControl.Models;
using PocketMC.RemoteControl.Services;
using Xunit;

namespace PocketMC.Tests;

public class RemoteControlServicesTests
{
    [Fact]
    public void RemoteControlActionResult_SuccessfulAndFailedFactoriesWork()
    {
        var success = RemoteControlActionResult.Successful("All good");
        Assert.True(success.Success);
        Assert.Equal(RemoteControlActionFailure.None, success.Failure);
        Assert.Equal("All good", success.Message);

        var fail = RemoteControlActionResult.Failed(RemoteControlActionFailure.NotFound, "Not found");
        Assert.False(fail.Success);
        Assert.Equal(RemoteControlActionFailure.NotFound, fail.Failure);
        Assert.Equal("Not found", fail.Message);
    }

    [Fact]
    public async Task RemoteInstanceControlService_BatchExecution_ValidatesAndReportsStatus()
    {
        var fakeInstService = new FakeInstanceService();
        var fakeProcessRunner = new FakeProcessRunner();
        var inst1 = new ServerInstance { Id = Guid.NewGuid(), Name = "Server 1", Slug = "server-1" };
        var inst2 = new ServerInstance { Id = Guid.NewGuid(), Name = "Server 2", Slug = "server-2" };
        fakeInstService.Instances.Add(inst1);
        fakeInstService.Instances.Add(inst2);

        var controlService = new RemoteInstanceControlService(fakeInstService, fakeProcessRunner);

        var batchResult = await controlService.ExecuteBatchAsync(
            new[] { inst1.Id, inst2.Id, Guid.NewGuid() },
            "start",
            hasAccess: id => true);

        Assert.Equal(3, batchResult.Count);
        Assert.Equal("success", batchResult[0].Status);
        Assert.Equal("success", batchResult[1].Status);
        Assert.Equal("notFound", batchResult[2].Status);
    }

    [Fact]
    public async Task RemotePlayerActionService_ExecutesCommandsCorrectly()
    {
        var fakeInstService = new FakeInstanceService();
        var fakeProcessRunner = new FakeProcessRunner();
        var inst = new ServerInstance
        {
            Id = Guid.NewGuid(),
            Name = "Server 1",
            Slug = "server-1",
            EngineType = EngineType.Paper
        };
        fakeInstService.Instances.Add(inst);

        // Put instance in Running state
        fakeProcessRunner.RunningInfo[inst.Slug] = (1234, "Running");

        var auditLog = new RemoteAuditLogService();
        var playerActionService = new RemotePlayerActionService(fakeInstService, fakeProcessRunner, auditLog);

        var kickRes = await playerActionService.ExecuteAsync(inst.Id, "Steve", "kick", new RemotePlayerActionRequest { Reason = "Spamming" }, "device1");
        Assert.True(kickRes.Success);

        var banRes = await playerActionService.ExecuteAsync(inst.Id, "Alex", "ban", null, "device1");
        Assert.True(banRes.Success);

        var invalidNameRes = await playerActionService.ExecuteAsync(inst.Id, "bad name with spaces!", "kick", null, "device1");
        Assert.False(invalidNameRes.Success);
        Assert.Equal("Invalid player name.", invalidNameRes.Message);
    }

    [Fact]
    public async Task RemoteStatusService_GetInstances_ReturnsExpectedDtos()
    {
        var fakeInstService = new FakeInstanceService();
        var fakeProcessRunner = new FakeProcessRunner();
        var fakePlayerService = new FakePlayerService();
        var metricsTracker = new ProcessMetricsTracker();
        var localNet = new LocalNetworkAddressService();

        var inst = new ServerInstance
        {
            Id = Guid.NewGuid(),
            Name = "Survival 1.20",
            Slug = "survival-120",
            EngineType = EngineType.Paper,
            EngineVersion = "1.20.4"
        };
        fakeInstService.Instances.Add(inst);

        var statusService = new RemoteStatusService(
            fakeInstService,
            fakeProcessRunner,
            fakePlayerService,
            metricsTracker,
            localNet);

        var instances = await statusService.GetInstancesAsync();
        Assert.Single(instances);
        Assert.Equal("Survival 1.20", instances[0].Name);
        Assert.Equal("Paper", instances[0].ServerType);
        Assert.Equal("1.20.4", instances[0].MinecraftVersion);
    }

    [Fact]
    public void RemoteServerProperties_LoadAndSave_UpdatesFilePreservingKeys()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "pocketmc_props_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            string propsPath = Path.Combine(tempDir, "server.properties");
            File.WriteAllText(propsPath, "# Minecraft server properties\nmotd=Old MOTD\nmax-players=10\ngamemode=survival\npvp=true\n");

            var dto = PocketMC.RemoteControl.Services.ServerPropertiesHelper.Load(tempDir);
            Assert.Equal("Old MOTD", dto.Motd);
            Assert.Equal(10, dto.MaxPlayers);
            Assert.Equal("survival", dto.Gamemode);
            Assert.True(dto.Pvp);

            dto.Motd = "New Awesome MOTD";
            dto.MaxPlayers = 50;
            dto.Difficulty = "hard";
            dto.AllowFlight = true;

            PocketMC.RemoteControl.Services.ServerPropertiesHelper.Save(tempDir, dto);

            var reloaded = PocketMC.RemoteControl.Services.ServerPropertiesHelper.Load(tempDir);
            Assert.Equal("New Awesome MOTD", reloaded.Motd);
            Assert.Equal(50, reloaded.MaxPlayers);
            Assert.Equal("hard", reloaded.Difficulty);
            Assert.True(reloaded.AllowFlight);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
