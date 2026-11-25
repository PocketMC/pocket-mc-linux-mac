using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using PocketMC.Core.Models;
using PocketMC.Core.Models.Networking;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Networking;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests;

public sealed class ServerLifecycleServiceTests
{
    private static int GetAvailableTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    [Fact]
    public async Task StartAsync_ReservesPortLeaseAndReleasesWhenRunnerThrows()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "pmc-lifecycle-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var probe = new PortProbeService(NullLogger<PortProbeService>.Instance);
            var leases = new PortLeaseRegistry();
            var recovery = new PortRecoveryService(probe, leases, NullLogger<PortRecoveryService>.Instance);
            var runner = new ThrowingProcessRunner();
            var instanceService = new FakeInstanceService();
            var httpClient = new System.Net.Http.HttpClient();
            var geyserProvisioning = new GeyserProvisioningService(new DownloaderService(httpClient), new ModrinthService(httpClient));
            var geyserDetector = new GeyserDetector(new AddonManifestService());

            var lifecycle = new ServerLifecycleService(
                runner,
                instanceService,
                probe,
                leases,
                recovery,
                geyserProvisioning,
                geyserDetector,
                NullLogger<ServerLifecycleService>.Instance);

            int port = GetAvailableTcpPort();
            var server = new ServerInstance
            {
                Id = Guid.NewGuid(),
                Name = "Test Paper Server",
                Slug = "test-paper-server",
                Path = tempDir,
                EngineType = EngineType.Paper,
                EngineVersion = "1.20.4",
                ServerPort = port
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() => lifecycle.StartAsync(server));

            Assert.Empty(leases.GetLeasesForInstance(server.Id));
            Assert.False(lifecycle.IsRunning(server.Slug));
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private sealed class ThrowingProcessRunner : IProcessRunner
    {
        public event Action<string, string>? StateChanged
        {
            add { }
            remove { }
        }

        public Task StartAsync(ServerInstance instance, bool isAutoRestart = false)
            => throw new InvalidOperationException("Simulated launch failure");

        public Task StopAsync(ServerInstance instance) => Task.CompletedTask;
        public Task StopAllAsync() => Task.CompletedTask;
        public System.Collections.Generic.IReadOnlyList<string> GetRunningSlugs() => Array.Empty<string>();
        public Task SendCommandAsync(ServerInstance instance, string command) => Task.CompletedTask;
        public bool TryGetRunningInfo(string slug, out int pgid, out string state)
        {
            pgid = 0;
            state = "Stopped";
            return false;
        }
        public DateTime? GetSessionStartTime(string slug) => null;
    }
}
