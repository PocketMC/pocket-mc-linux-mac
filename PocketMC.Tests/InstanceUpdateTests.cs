using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests
{
    public class InstanceUpdateTests
    {
        [Fact]
        public async Task InstanceUpdatePlanner_PlansTargetVersionCorrectly()
        {
            var planner = new InstanceUpdatePlanner();
            var instance = new ServerInstance
            {
                Id = Guid.NewGuid(),
                Name = "TestServer",
                Slug = "test-server",
                Path = "/tmp/test-server",
                EngineType = EngineType.Paper,
                EngineVersion = "1.20.4"
            };

            var plan = await planner.BuildPlanAsync(instance, "1.21");

            Assert.Equal("1.20.4", plan.CurrentVersion);
            Assert.Equal("1.21", plan.TargetVersion);
            Assert.Equal(17, plan.CurrentRequiredJavaVersion);
            Assert.Equal(21, plan.TargetRequiredJavaVersion);
            Assert.Contains("Java 17 -> Java 21", plan.RuntimeChangeText);
            Assert.Equal("server.jar", plan.ServerArtifactFileName);
        }

        [Fact]
        public async Task InstanceUpdatePlanner_HandlesPocketMineAndBedrock()
        {
            var planner = new InstanceUpdatePlanner();
            var pmInstance = new ServerInstance
            {
                Id = Guid.NewGuid(),
                Name = "PMServer",
                Slug = "pm-server",
                Path = "/tmp/pm-server",
                EngineType = EngineType.PocketMine,
                EngineVersion = "5.10.0"
            };

            var plan = await planner.BuildPlanAsync(pmInstance, "5.11.0");
            Assert.Equal("PocketMine-MP.phar", plan.ServerArtifactFileName);
            Assert.Contains("PHP", plan.RuntimeChangeText);

            var bdsInstance = new ServerInstance
            {
                Id = Guid.NewGuid(),
                Name = "BdsServer",
                Slug = "bds-server",
                Path = "/tmp/bds-server",
                EngineType = EngineType.Bedrock,
                EngineVersion = "1.20.80.01"
            };

            var bdsPlan = await planner.BuildPlanAsync(bdsInstance, "1.21.0.03");
            Assert.Equal("bedrock-server.zip", bdsPlan.ServerArtifactFileName);
        }

        [Fact]
        public async Task InstanceUpdatePlanner_PlansForgeAndNeoForgeAsInstallerJar()
        {
            var planner = new InstanceUpdatePlanner();
            var forgeInstance = new ServerInstance
            {
                Id = Guid.NewGuid(),
                Name = "ForgeServer",
                Slug = "forge-server",
                Path = "/tmp/forge-server",
                EngineType = EngineType.Forge,
                EngineVersion = "1.20.1"
            };

            var forgePlan = await planner.BuildPlanAsync(forgeInstance, "1.20.2");
            Assert.Equal("installer.jar", forgePlan.ServerArtifactFileName);

            var neoForgeInstance = new ServerInstance
            {
                Id = Guid.NewGuid(),
                Name = "NeoForgeServer",
                Slug = "neoforge-server",
                Path = "/tmp/neoforge-server",
                EngineType = EngineType.NeoForge,
                EngineVersion = "1.20.4"
            };

            var neoForgePlan = await planner.BuildPlanAsync(neoForgeInstance, "1.21");
            Assert.Equal("installer.jar", neoForgePlan.ServerArtifactFileName);
        }

        [Fact]
        public void InstanceFileTransaction_CommitsReplacementsAtomically()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"pocketmc-test-tx-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                string targetFile = Path.Combine(tempDir, "server.jar");
                File.WriteAllText(targetFile, "ORIGINAL_SERVER_JAR_CONTENT");

                string stagedDir = Path.Combine(tempDir, ".staging");
                Directory.CreateDirectory(stagedDir);
                string stagedFile = Path.Combine(stagedDir, "new-server.jar");
                File.WriteAllText(stagedFile, "UPDATED_SERVER_JAR_CONTENT");

                var replacements = new[]
                {
                    new InstanceFileTransaction.Replacement(stagedFile, "server.jar")
                };

                InstanceFileTransaction.Commit(tempDir, replacements);

                Assert.True(File.Exists(targetFile));
                Assert.Equal("UPDATED_SERVER_JAR_CONTENT", File.ReadAllText(targetFile));
                Assert.False(File.Exists(Path.Combine(tempDir, ".pocketmc-file-transaction.json")));
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
        public void InstanceFileTransaction_RecoversInterruptedTransaction()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"pocketmc-test-recover-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                string targetFile = Path.Combine(tempDir, "server.jar");
                File.WriteAllText(targetFile, "BROKEN_NEW_CONTENT");

                string rollbackDir = Path.Combine(tempDir, ".pocketmc-rollback-test");
                Directory.CreateDirectory(rollbackDir);
                string backupFile = Path.Combine(rollbackDir, "0");
                File.WriteAllText(backupFile, "PRESERVED_ORIGINAL_CONTENT");

                // Create uncommitted journal simulating crash mid-operation
                var journal = new InstanceFileTransaction.Journal(new System.Collections.Generic.List<InstanceFileTransaction.Entry>
                {
                    new InstanceFileTransaction.Entry("server.jar", ".pocketmc-rollback-test/0", true)
                }, Committed: false);

                string journalPath = Path.Combine(tempDir, ".pocketmc-file-transaction.json");
                File.WriteAllText(journalPath, JsonSerializer.Serialize(journal));

                // Execute recovery
                bool recovered = InstanceFileTransaction.Recover(tempDir);

                Assert.True(recovered);
                Assert.Equal("PRESERVED_ORIGINAL_CONTENT", File.ReadAllText(targetFile));
                Assert.False(File.Exists(journalPath));
                Assert.False(Directory.Exists(rollbackDir));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        private class FailingRegistryInstanceService : IInstanceService
        {
            private readonly ServerInstance _instance;
            public FailingRegistryInstanceService(ServerInstance instance) => _instance = instance;

            public Task<System.Collections.Generic.List<ServerInstance>> ListInstancesAsync() => Task.FromResult(new System.Collections.Generic.List<ServerInstance> { _instance });
            public Task<ServerInstance> CreateInstanceAsync(string name, EngineType engineType, string version) => throw new NotImplementedException();
            public Task DeleteInstanceAsync(string slug) => throw new NotImplementedException();
            public Task<ServerInstance> RenameInstanceAsync(string slug, string newName) => throw new NotImplementedException();
            public Task<ServerInstance> CloneInstanceAsync(string slug, string newName) => throw new NotImplementedException();
            public Task ExportInstanceAsync(string slug, string targetZipPath) => throw new NotImplementedException();
            public Task<ServerInstance> ImportInstanceAsync(string sourceZipPath, string? newName = null) => throw new NotImplementedException();
            public Task SaveInstanceAsync(ServerInstance instance) => throw new IOException("Registry disk full / persistence failure simulated.");
        }

        private class FakeRunner : Core.Services.IProcessRunner
        {
            public event Action<string, string>? StateChanged;
            public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);
            public Task StartAsync(ServerInstance instance, bool isAutoRestart = false) => Task.CompletedTask;
            public Task StopAsync(ServerInstance instance) => Task.CompletedTask;
            public Task StopAllAsync() => Task.CompletedTask;
            public Task SendCommandAsync(ServerInstance instance, string command) => Task.CompletedTask;
            public bool TryGetRunningInfo(string slug, out int pid, out string state)
            {
                pid = 0;
                state = "Stopped";
                return false;
            }
            public DateTime? GetSessionStartTime(string slug) => null;
            public System.Collections.Generic.IReadOnlyList<string> GetRunningSlugs() => Array.Empty<string>();
        }

        [Fact]
        public async Task InstanceUpdateApplier_WhenRegistryPersistenceFails_RollsBackArtifactReplacement()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"pocketmc-rollback-reg-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                string targetJar = Path.Combine(tempDir, "server.jar");
                File.WriteAllText(targetJar, "ORIGINAL_SERVER_JAR");

                string stagedDir = Path.Combine(tempDir, ".staging");
                Directory.CreateDirectory(stagedDir);
                string stagedJar = Path.Combine(stagedDir, "new-server.jar");
                File.WriteAllText(stagedJar, "NEW_SERVER_JAR");

                var instance = new ServerInstance
                {
                    Id = Guid.NewGuid(),
                    Name = "TestRollback",
                    Slug = "test-rollback",
                    Path = tempDir,
                    EngineType = EngineType.Paper,
                    EngineVersion = "1.20.4"
                };

                var plan = new InstanceUpdatePlan
                {
                    OperationId = Guid.NewGuid(),
                    InstanceId = instance.Id,
                    InstanceSlug = instance.Slug,
                    InstancePath = tempDir,
                    CurrentEngineType = EngineType.Paper,
                    CurrentVersion = "1.20.4",
                    TargetEngineType = EngineType.Paper,
                    TargetVersion = "1.21",
                    ServerArtifactFileName = "server.jar"
                };

                var stagedArtifacts = new InstanceUpdateStagedArtifacts
                {
                    StagingDirectory = stagedDir,
                    ServerArtifactPath = stagedJar
                };

                var runner = new FakeRunner();
                var failingService = new FailingRegistryInstanceService(instance);
                var coordinator = new InstanceOperationCoordinator();
                var applier = new InstanceUpdateApplier(runner, failingService, coordinator);

                var result = await applier.ApplyAsync(plan, stagedArtifacts);

                Assert.False(result.Success);
                Assert.True(result.RestoredFromRollback);
                Assert.Equal("ORIGINAL_SERVER_JAR", File.ReadAllText(targetJar));
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task InstanceOperationCoordinator_BlocksConcurrentAcquire()
        {
            var coordinator = new InstanceOperationCoordinator();
            string testDir = Path.Combine(Path.GetTempPath(), $"pocketmc-coord-{Guid.NewGuid():N}");
            Directory.CreateDirectory(testDir);

            try
            {
                using var handle1 = await coordinator.AcquireAsync(testDir);

                // Trying to acquire with short timeout should fail or block
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromMilliseconds(100));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                {
                    await coordinator.AcquireAsync(testDir, cancellationToken: cts.Token);
                });
            }
            finally
            {
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, true);
                }
            }
        }

        private class RecordingHandler : System.Net.Http.HttpMessageHandler
        {
            public System.Collections.Generic.List<string> RequestedUrls { get; } = new();

            protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            {
                var url = request.RequestUri!.ToString();
                RequestedUrls.Add(url);

                if (url.Contains("meta.fabricmc.net/v2/versions/loader"))
                {
                    return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new System.Net.Http.StringContent("[{\"version\":\"0.15.11\",\"stable\":true}]")
                    });
                }

                if (url.Contains("meta.fabricmc.net/v2/versions/installer"))
                {
                    return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new System.Net.Http.StringContent("[{\"version\":\"1.0.1\",\"stable\":true}]")
                    });
                }

                if (url.Contains("files.minecraftforge.net") || url.Contains("promotions_slim.json"))
                {
                    return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new System.Net.Http.StringContent("{\"promos\":{\"1.20.1-recommended\":\"47.2.0\"}}")
                    });
                }

                if (url.Contains("meta.prismlauncher.org/v1/net.neoforged/index.json"))
                {
                    return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new System.Net.Http.StringContent("{\"versions\":[{\"requires\":[{\"uid\":\"net.minecraft\",\"equals\":\"1.20.4\"}],\"version\":\"20.4.80-beta\"}]}")
                    });
                }

                // Default fake binary download
                return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new System.Net.Http.ByteArrayContent(new byte[] { 1, 2, 3, 4, 5 })
                });
            }
        }

        [Fact]
        public async Task InstanceArtifactStager_FabricUpdate_NeverDownloadsPaper()
        {
            var handler = new RecordingHandler();
            var httpClient = new System.Net.Http.HttpClient(handler);
            var stager = new InstanceArtifactStager(httpClient);

            string tempDir = Path.Combine(Path.GetTempPath(), $"pocketmc-fabric-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var plan = new InstanceUpdatePlan
                {
                    OperationId = Guid.NewGuid(),
                    InstanceId = Guid.NewGuid(),
                    InstanceSlug = "fabric-instance",
                    InstancePath = tempDir,
                    CurrentEngineType = EngineType.Fabric,
                    CurrentVersion = "1.20.1",
                    TargetEngineType = EngineType.Fabric,
                    TargetVersion = "1.20.2",
                    ServerArtifactFileName = "server.jar"
                };

                var staged = await stager.StageAsync(plan);

                Assert.True(File.Exists(staged.ServerArtifactPath));
                Assert.DoesNotContain(handler.RequestedUrls, u => u.Contains("papermc.io"));
                Assert.Contains(handler.RequestedUrls, u => u.Contains("meta.fabricmc.net"));
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task InstanceArtifactStager_ForgeUpdate_NeverDownloadsPaper()
        {
            var handler = new RecordingHandler();
            var httpClient = new System.Net.Http.HttpClient(handler);
            var stager = new InstanceArtifactStager(httpClient);

            string tempDir = Path.Combine(Path.GetTempPath(), $"pocketmc-forge-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var plan = new InstanceUpdatePlan
                {
                    OperationId = Guid.NewGuid(),
                    InstanceId = Guid.NewGuid(),
                    InstanceSlug = "forge-instance",
                    InstancePath = tempDir,
                    CurrentEngineType = EngineType.Forge,
                    CurrentVersion = "1.20.1",
                    TargetEngineType = EngineType.Forge,
                    TargetVersion = "1.20.1",
                    ServerArtifactFileName = "installer.jar"
                };

                var staged = await stager.StageAsync(plan);

                Assert.True(File.Exists(staged.ServerArtifactPath));
                Assert.DoesNotContain(handler.RequestedUrls, u => u.Contains("papermc.io"));
                Assert.Contains(handler.RequestedUrls, u => u.Contains("minecraftforge.net"));
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task InstanceArtifactStager_NeoForgeUpdate_NeverDownloadsPaper()
        {
            var handler = new RecordingHandler();
            var httpClient = new System.Net.Http.HttpClient(handler);
            var stager = new InstanceArtifactStager(httpClient);

            string tempDir = Path.Combine(Path.GetTempPath(), $"pocketmc-neoforge-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var plan = new InstanceUpdatePlan
                {
                    OperationId = Guid.NewGuid(),
                    InstanceId = Guid.NewGuid(),
                    InstanceSlug = "neoforge-instance",
                    InstancePath = tempDir,
                    CurrentEngineType = EngineType.NeoForge,
                    CurrentVersion = "1.20.4",
                    TargetEngineType = EngineType.NeoForge,
                    TargetVersion = "1.20.4",
                    ServerArtifactFileName = "installer.jar"
                };

                var staged = await stager.StageAsync(plan);

                Assert.True(File.Exists(staged.ServerArtifactPath));
                Assert.DoesNotContain(handler.RequestedUrls, u => u.Contains("papermc.io"));
                Assert.Contains(handler.RequestedUrls, u => u.Contains("neoforged.net") || u.Contains("prismlauncher.org"));
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task InstanceArtifactStager_VanillaChecksumMismatch_ThrowsCryptographicException()
        {
            var handler = new RecordingHandler();
            // Provide a manifest pointing to a version with a bad sha1
            var customHandler = new ChecksumMismatchHandler();
            var httpClient = new System.Net.Http.HttpClient(customHandler);
            var stager = new InstanceArtifactStager(httpClient);

            string tempDir = Path.Combine(Path.GetTempPath(), $"pocketmc-corrupt-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var plan = new InstanceUpdatePlan
                {
                    OperationId = Guid.NewGuid(),
                    InstanceId = Guid.NewGuid(),
                    InstanceSlug = "corrupt-vanilla",
                    InstancePath = tempDir,
                    CurrentEngineType = EngineType.VanillaJava,
                    CurrentVersion = "1.20.4",
                    TargetEngineType = EngineType.VanillaJava,
                    TargetVersion = "1.20.4",
                    ServerArtifactFileName = "server.jar"
                };

                await Assert.ThrowsAsync<System.Security.Cryptography.CryptographicException>(async () =>
                {
                    await stager.StageAsync(plan);
                });
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        private class ChecksumMismatchHandler : System.Net.Http.HttpMessageHandler
        {
            protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            {
                var url = request.RequestUri!.ToString();

                if (url.Contains("version_manifest.json"))
                {
                    return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new System.Net.Http.StringContent("{\"versions\":[{\"id\":\"1.20.4\",\"url\":\"https://example.com/1.20.4.json\"}]}")
                    });
                }

                if (url.Contains("1.20.4.json"))
                {
                    // Expected hash is "deadbeef", but we return bytes {1, 2, 3}
                    return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new System.Net.Http.StringContent("{\"downloads\":{\"server\":{\"url\":\"https://example.com/server.jar\",\"sha1\":\"0123456789abcdef0123456789abcdef01234567\"}}}")
                    });
                }

                return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new System.Net.Http.ByteArrayContent(new byte[] { 1, 2, 3, 4, 5 })
                });
            }
        }

        [Fact]
        public async Task InstanceOperationCoordinator_ThrowsIfRunningWhenRequiresStopped()
        {
            var coordinator = new InstanceOperationCoordinator();
            string testDir = Path.Combine(Path.GetTempPath(), $"pocketmc-coord-run-{Guid.NewGuid():N}");
            Directory.CreateDirectory(testDir);

            try
            {
                coordinator.SetRunning(testDir, true);

                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                {
                    await coordinator.AcquireAsync(testDir, requiresStopped: true);
                });

                coordinator.SetRunning(testDir, false);

                using var handle = await coordinator.AcquireAsync(testDir, requiresStopped: true);
                Assert.NotNull(handle);
            }
            finally
            {
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, true);
                }
            }
        }
    }
}
