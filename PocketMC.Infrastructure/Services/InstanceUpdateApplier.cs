using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.Infrastructure.Services
{
    public class InstanceUpdateApplier
    {
        private static readonly string[] BedrockPreservedEntries =
        {
            "worlds",
            "behavior_packs",
            "resource_packs",
            "config",
            "server.properties",
            "eula.txt",
            "permissions.json",
            "allowlist.json"
        };

        private readonly IProcessRunner _processRunner;
        private readonly IInstanceService _instanceService;
        private readonly IBackupService? _backupService;
        private readonly InstanceOperationCoordinator _coordinator;

        public InstanceUpdateApplier(
            IProcessRunner processRunner,
            IInstanceService instanceService,
            InstanceOperationCoordinator coordinator,
            IBackupService? backupService = null)
        {
            _processRunner = processRunner;
            _instanceService = instanceService;
            _coordinator = coordinator;
            _backupService = backupService;
        }

        public async Task<InstanceUpdateApplyResult> ApplyAsync(
            InstanceUpdatePlan plan,
            InstanceUpdateStagedArtifacts stagedArtifacts,
            Action<string>? onStatus = null,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(stagedArtifacts);

            var instances = await _instanceService.ListInstancesAsync();
            var instance = instances.FirstOrDefault(i => i.Id == plan.InstanceId || i.Slug.Equals(plan.InstanceSlug, StringComparison.OrdinalIgnoreCase));
            if (instance == null)
            {
                return new InstanceUpdateApplyResult
                {
                    OperationId = plan.OperationId,
                    Success = false,
                    ErrorMessage = $"Instance '{plan.InstanceSlug}' not found."
                };
            }

            // 1. Stop server if running
            if (_processRunner.TryGetRunningInfo(instance.Slug, out _, out var state) && state == "Running")
            {
                onStatus?.Invoke("Stopping server gracefully before update...");
                await _processRunner.StopAsync(instance);
            }

            // 2. Acquire operational lock for this instance directory
            using var lockHandle = await _coordinator.AcquireAsync(instance.Path, requiresStopped: true, ct);

            // 3. Create pre-update backup snapshot if possible
            if (_backupService != null)
            {
                try
                {
                    onStatus?.Invoke("Creating pre-update backup snapshot...");
                    await _backupService.CreateBackupAsync(instance, BackupTrigger.Manual, $"PreUpdate-{plan.TargetVersion}");
                }
                catch (Exception ex)
                {
                    onStatus?.Invoke($"Warning: Pre-update backup failed ({ex.Message}), continuing with transaction journal...");
                }
            }

            // 4. Recover any interrupted transactions before committing new one
            if (InstanceFileTransaction.Recover(instance.Path))
            {
                onStatus?.Invoke("Recovered interrupted prior transaction state.");
            }

            // 5. Prepare transactional replacements
            var replacements = new List<InstanceFileTransaction.Replacement>();

            try
            {
                if (plan.TargetEngineType == EngineType.Bedrock)
                {
                    string extractedDir = Path.Combine(stagedArtifacts.StagingDirectory, $"extracted-bds-{Guid.NewGuid():N}");
                    Directory.CreateDirectory(extractedDir);
                    ZipFile.ExtractToDirectory(stagedArtifacts.ServerArtifactPath, extractedDir);

                    foreach (var entry in Directory.EnumerateFileSystemEntries(extractedDir))
                    {
                        string name = Path.GetFileName(entry);
                        if (!BedrockPreservedEntries.Contains(name, StringComparer.OrdinalIgnoreCase))
                        {
                            replacements.Add(new InstanceFileTransaction.Replacement(entry, name));
                        }
                    }
                }
                else
                {
                    // Java / PocketMine replacement
                    replacements.Add(new InstanceFileTransaction.Replacement(stagedArtifacts.ServerArtifactPath, plan.ServerArtifactFileName));
                }

                // Stage updated instance.json metadata
                var updatedInstance = new ServerInstance
                {
                    Id = instance.Id,
                    Name = instance.Name,
                    Slug = instance.Slug,
                    Path = instance.Path,
                    EngineType = plan.TargetEngineType,
                    EngineVersion = plan.TargetVersion,
                    JvmArgs = instance.JvmArgs,
                    CreatedAt = instance.CreatedAt,
                    LastBackupTime = instance.LastBackupTime,
                    MaxBackupsToKeep = instance.MaxBackupsToKeep,
                    CustomBackupDirectory = instance.CustomBackupDirectory
                };

                string stagedMetaPath = Path.Combine(stagedArtifacts.StagingDirectory, "instance.json");
                string metaJson = JsonSerializer.Serialize(updatedInstance, new JsonSerializerOptions { WriteIndented = true });
                FileUtils.AtomicWriteAllText(stagedMetaPath, metaJson);
                replacements.Add(new InstanceFileTransaction.Replacement(stagedMetaPath, "instance.json"));

                // 6. Commit transaction atomically
                onStatus?.Invoke("Applying verified update artifacts transactionally...");
                InstanceFileTransaction.Commit(instance.Path, replacements, ct);

                // 7. Update in-memory and registry metadata
                await _instanceService.SaveInstanceAsync(updatedInstance);

                // 8. Clean up staging directory
                try
                {
                    if (Directory.Exists(stagedArtifacts.StagingDirectory))
                    {
                        Directory.Delete(stagedArtifacts.StagingDirectory, true);
                    }
                }
                catch { }

                onStatus?.Invoke($"Successfully updated to {plan.TargetVersion}.");
                return new InstanceUpdateApplyResult
                {
                    OperationId = plan.OperationId,
                    Success = true,
                    TargetVersionApplied = plan.TargetVersion
                };
            }
            catch (Exception ex)
            {
                onStatus?.Invoke($"Update failed: {ex.Message}. Rolling back files...");
                bool recovered = InstanceFileTransaction.Recover(instance.Path);

                return new InstanceUpdateApplyResult
                {
                    OperationId = plan.OperationId,
                    Success = false,
                    ErrorMessage = ex.Message,
                    RestoredFromRollback = recovered
                };
            }
        }
    }
}
