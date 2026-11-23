using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using PocketMC.App.ViewModels;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.RemoteControl.Services;
using PocketMC.RemoteControl.Tunnels;
using Xunit;

namespace PocketMC.Tests
{
    public class PortsMapViewModelTests
    {
        [Fact]
        public async Task PortsMapViewModel_RefreshMapAsync_DiscoversServerPortsAndMatchesTunnels()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pocketmc_portsmap_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var serverPropsPath = Path.Combine(tempDir, "server.properties");
            await File.WriteAllTextAsync(serverPropsPath, "server-port=25565\nenable-rcon=true\nrcon.port=25575\nenable-query=true\nquery.port=25565\n");

            try
            {
                var instanceId = Guid.NewGuid();
                var instance = new ServerInstance
                {
                    Id = instanceId,
                    Name = "Survival World",
                    Slug = "survival-world",
                    Path = tempDir,
                    EngineType = EngineType.Paper,
                    EngineVersion = "1.21.4"
                };

                var instService = new FakeInstanceService();
                instService.Instances.Add(instance);

                var procRunner = new FakeProcessRunner();
                procRunner.RunningInfo["survival-world"] = (1234, "running");

                var settingsService = new FakeSettingsService();
                var playitClient = new PlayitApiClient(settingsService);

                var vm = new PortsMapViewModel(instService, procRunner, playitClient, settingsService);
                await vm.RefreshMapAsync();

                Assert.Single(vm.ServerGroups);
                var group = vm.ServerGroups[0];
                Assert.Equal("Survival World", group.ServerName);
                Assert.True(group.IsOnline);
                Assert.True(group.IsExpanded);

                // Expect 3 routes: Game port (25565 TCP), RCON (25575 TCP), Query (25565 UDP)
                Assert.True(group.Routes.Count >= 3);
                Assert.Contains(group.Routes, r => r.Port == 25565 && r.Protocol == "TCP" && r.PortType == "GamePort");
                Assert.Contains(group.Routes, r => r.Port == 25575 && r.Protocol == "TCP" && r.PortType == "RconPort");
                Assert.Contains(group.Routes, r => r.Port == 25565 && r.Protocol == "UDP" && r.PortType == "QueryPort");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [Fact]
        public async Task PortsMapViewModel_SavePortAsync_UpdatesServerPropertiesWhenOffline()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pocketmc_portsmap_save_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var serverPropsPath = Path.Combine(tempDir, "server.properties");
            await File.WriteAllTextAsync(serverPropsPath, "server-port=25565\nenable-rcon=false\n");

            try
            {
                var instanceId = Guid.NewGuid();
                var instance = new ServerInstance
                {
                    Id = instanceId,
                    Name = "Offline Server",
                    Slug = "offline-server",
                    Path = tempDir,
                    EngineType = EngineType.VanillaJava,
                    EngineVersion = "1.21.4"
                };

                var instService = new FakeInstanceService();
                instService.Instances.Add(instance);

                var procRunner = new FakeProcessRunner(); // offline
                var settingsService = new FakeSettingsService();
                var playitClient = new PlayitApiClient(settingsService);

                var vm = new PortsMapViewModel(instService, procRunner, playitClient, settingsService);
                await vm.RefreshMapAsync();

                var route = vm.ServerGroups[0].Routes[0];
                Assert.Equal(25565, route.Port);
                Assert.False(route.IsOnline);

                // Edit port to 25570
                vm.EditPort(route);
                Assert.True(route.IsEditingPort);
                route.EditingPortText = "25570";

                await vm.SavePortAsync(route);

                Assert.False(route.IsEditingPort);
                Assert.Equal(25570, route.Port);
                Assert.Equal("25570 / TCP", route.LocalPortLabel);

                var updatedProps = ServerPropertiesHelper.ReadProperties(serverPropsPath);
                Assert.Equal("25570", updatedProps["server-port"]);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }
    }
}
