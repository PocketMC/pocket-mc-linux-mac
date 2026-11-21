using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.Infrastructure.Services
{
    public class InstanceUpdateService : IInstanceUpdateService
    {
        private readonly InstanceVersionTargetService _targetService;
        private readonly InstanceUpdatePlanner _planner;
        private readonly InstanceArtifactStager _stager;
        private readonly InstanceUpdateApplier _applier;

        public InstanceUpdateService(
            InstanceVersionTargetService targetService,
            InstanceUpdatePlanner planner,
            InstanceArtifactStager stager,
            InstanceUpdateApplier applier)
        {
            _targetService = targetService;
            _planner = planner;
            _stager = stager;
            _applier = applier;
        }

        public Task<IReadOnlyList<UpdateTargetVersionInfo>> GetAvailableTargetVersionsAsync(
            ServerInstance instance,
            CancellationToken ct = default)
        {
            return _targetService.GetAvailableTargetVersionsAsync(instance, false, ct);
        }

        public Task<InstanceUpdatePlan> BuildPlanAsync(
            ServerInstance instance,
            string targetVersion,
            CancellationToken ct = default)
        {
            return _planner.BuildPlanAsync(instance, targetVersion, ct);
        }

        public Task<InstanceUpdateStagedArtifacts> StageAsync(
            InstanceUpdatePlan plan,
            IProgress<double>? progress = null,
            CancellationToken ct = default)
        {
            return _stager.StageAsync(plan, progress, ct);
        }

        public Task<InstanceUpdateApplyResult> ApplyAsync(
            InstanceUpdatePlan plan,
            InstanceUpdateStagedArtifacts staged,
            Action<string>? onStatus = null,
            CancellationToken ct = default)
        {
            return _applier.ApplyAsync(plan, staged, onStatus, ct);
        }

        public async Task<InstanceUpdateApplyResult> UpdateAsync(
            ServerInstance instance,
            string targetVersion,
            IProgress<double>? progress = null,
            Action<string>? onStatus = null,
            CancellationToken ct = default)
        {
            onStatus?.Invoke($"Planning update to version {targetVersion}...");
            var plan = await BuildPlanAsync(instance, targetVersion, ct);

            onStatus?.Invoke("Downloading and staging verified server artifacts...");
            var staged = await StageAsync(plan, progress, ct);

            return await ApplyAsync(plan, staged, onStatus, ct);
        }
    }
}
