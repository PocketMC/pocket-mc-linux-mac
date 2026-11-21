using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;
using PocketMC.RemoteControl.Hosting;
using PocketMC.RemoteControl.Models;
using PocketMC.RemoteControl.Services;
using PocketMC.RemoteControl.Tunnels;
using Xunit;

namespace PocketMC.Tests;

public class RemoteDashboardHostTests
{
    private static int GetFreePort()
    {
        using var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    [Fact]
    public async Task RemoteDashboardHost_StartsAndServesRestEndpoints()
    {
        int port = GetFreePort();
        string tempDir = Path.Combine(Path.GetTempPath(), "pocketmc_remote_test_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            var settingsService = new SettingsService(tempDir);
            settingsService.Settings.RemoteControl.Enabled = true;
            settingsService.Settings.RemoteControl.Port = port;
            settingsService.Settings.RemoteControl.RequireAuthentication = false; // Disable auth for testing public API contracts

            var fakeInstService = new FakeInstanceService();
            var instDir = Path.Combine(tempDir, "instances", "survival-1");
            Directory.CreateDirectory(instDir);
            File.WriteAllText(Path.Combine(instDir, "server.properties"), "motd=Test Server\nmax-players=25\n");
            File.WriteAllText(Path.Combine(instDir, "eula.txt"), "eula=true\n");

            var inst = new ServerInstance
            {
                Id = Guid.NewGuid(),
                Name = "Survival Server",
                Slug = "survival-1",
                Path = instDir,
                EngineType = EngineType.Paper,
                EngineVersion = "1.20.4"
            };
            fakeInstService.Instances.Add(inst);

            var fakeProcessRunner = new FakeProcessRunner();
            var fakePlayerService = new FakePlayerService();
            var metricsTracker = new ProcessMetricsTracker();
            var localNet = new LocalNetworkAddressService();
            var authService = new RemoteAuthenticationService();
            var auditLog = new RemoteAuditLogService(Path.Combine(tempDir, "logs"));
            var limiter = new RemoteRequestLimiter();
            var fakeLogService = new FakeConsoleLogService();
            var wsHandler = new RemoteConsoleWebSocketHandler(fakeLogService);
            var tunnelManager = new RemoteTunnelManager(Array.Empty<IRemoteTunnelProvider>());

            var statusService = new RemoteStatusService(fakeInstService, fakeProcessRunner, fakePlayerService, metricsTracker, localNet);
            var controlService = new RemoteInstanceControlService(fakeInstService, fakeProcessRunner);
            var playerActionService = new RemotePlayerActionService(fakeInstService, fakeProcessRunner, auditLog);

            var host = new RemoteDashboardHost(
                settingsService,
                fakeInstService,
                fakeProcessRunner,
                authService,
                auditLog,
                limiter,
                wsHandler,
                tunnelManager,
                localNet,
                statusService,
                controlService,
                playerActionService,
                fakeLogService,
                null);

            await host.StartAsync(CancellationToken.None);
            Assert.True(host.IsRunning);

            using var httpClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

            // 1. /health
            var healthRes = await httpClient.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, healthRes.StatusCode);

            // 2. /api/status
            var statusRes = await httpClient.GetAsync("/api/status");
            Assert.Equal(HttpStatusCode.OK, statusRes.StatusCode);
            var statusJson = await statusRes.Content.ReadAsStringAsync();
            var dashStatus = JsonSerializer.Deserialize<RemoteDashboardStatus>(statusJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(dashStatus);
            Assert.True(dashStatus.Enabled);
            Assert.True(dashStatus.HostRunning);

            // 3. /api/instances
            var instsRes = await httpClient.GetAsync("/api/instances");
            Assert.Equal(HttpStatusCode.OK, instsRes.StatusCode);
            var instsJson = await instsRes.Content.ReadAsStringAsync();
            var insts = JsonSerializer.Deserialize<RemoteInstanceDto[]>(instsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(insts);
            Assert.Single(insts);
            Assert.Equal("Survival Server", insts[0].Name);

            // 4. /api/instances/{id}/status
            var singleStatusRes = await httpClient.GetAsync($"/api/instances/{inst.Id}/status");
            Assert.Equal(HttpStatusCode.OK, singleStatusRes.StatusCode);
            var singleStatusJson = await singleStatusRes.Content.ReadAsStringAsync();
            var singleStatus = JsonSerializer.Deserialize<RemoteInstanceStatusDto>(singleStatusJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(singleStatus);
            Assert.Equal("Survival Server", singleStatus.Name);

            // 5. /api/instances/{id}/properties (GET & PUT)
            var propsRes = await httpClient.GetAsync($"/api/instances/{inst.Id}/properties");
            Assert.Equal(HttpStatusCode.OK, propsRes.StatusCode);
            var propsJson = await propsRes.Content.ReadAsStringAsync();
            var propsDto = JsonSerializer.Deserialize<RemoteServerPropertiesDto>(propsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(propsDto);
            Assert.Equal("Test Server", propsDto.Motd);
            Assert.Equal(25, propsDto.MaxPlayers);

            propsDto.Motd = "Updated Remotely";
            var putPropsRes = await httpClient.PutAsync($"/api/instances/{inst.Id}/properties",
                new StringContent(JsonSerializer.Serialize(propsDto), Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.OK, putPropsRes.StatusCode);

            // Verify properties persisted
            var reloadedProps = ServerPropertiesHelper.Load(inst.Path);
            Assert.Equal("Updated Remotely", reloadedProps.Motd);

            // 6. /api/instances/{id}/files
            var filesRes = await httpClient.GetAsync($"/api/instances/{inst.Id}/files");
            Assert.Equal(HttpStatusCode.OK, filesRes.StatusCode);
            var filesJson = await filesRes.Content.ReadAsStringAsync();
            var files = JsonSerializer.Deserialize<RemoteFileItemDto[]>(filesJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(files);
            Assert.Contains(files, f => f.Name == "eula.txt");

            // 7. /api/instances/{id}/files/content
            var fileContentRes = await httpClient.GetAsync($"/api/instances/{inst.Id}/files/content?path=eula.txt");
            Assert.Equal(HttpStatusCode.OK, fileContentRes.StatusCode);
            var fileContentJson = await fileContentRes.Content.ReadAsStringAsync();
            var fileContent = JsonSerializer.Deserialize<RemoteFileContentDto>(fileContentJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(fileContent);
            Assert.Equal("eula=true\n", fileContent.Content);

            // 8. Put file content
            var saveFileReq = new SaveRemoteFileContentRequest
            {
                RelativePath = "eula.txt",
                Content = "eula=false\n"
            };
            var putFileRes = await httpClient.PutAsync($"/api/instances/{inst.Id}/files/content",
                new StringContent(JsonSerializer.Serialize(saveFileReq), Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.OK, putFileRes.StatusCode);
            Assert.Equal("eula=false\n", File.ReadAllText(Path.Combine(inst.Path, "eula.txt")));

            // Stop host
            await host.StopAsync(CancellationToken.None);
            Assert.False(host.IsRunning);
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
