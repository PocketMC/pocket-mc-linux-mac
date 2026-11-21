using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PocketMC.Core.Models;

namespace PocketMC.Core.Services
{
    public interface IInstanceUpdateService
    {
        Task<IReadOnlyList<UpdateTargetVersionInfo>> GetAvailableTargetVersionsAsync(ServerInstance instance, CancellationToken ct = default);
        Task<InstanceUpdatePlan> BuildPlanAsync(ServerInstance instance, string targetVersion, CancellationToken ct = default);
        Task<InstanceUpdateStagedArtifacts> StageAsync(InstanceUpdatePlan plan, IProgress<double>? progress = null, CancellationToken ct = default);
        Task<InstanceUpdateApplyResult> ApplyAsync(InstanceUpdatePlan plan, InstanceUpdateStagedArtifacts staged, Action<string>? onStatus = null, CancellationToken ct = default);
        Task<InstanceUpdateApplyResult> UpdateAsync(ServerInstance instance, string targetVersion, IProgress<double>? progress = null, Action<string>? onStatus = null, CancellationToken ct = default);
    }
}
