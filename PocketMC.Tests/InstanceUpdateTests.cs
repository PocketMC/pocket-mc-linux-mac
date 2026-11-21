using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using PocketMC.Core.Models;
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
