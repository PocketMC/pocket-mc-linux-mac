using System;
using System.Collections.Generic;

namespace PocketMC.Core.Models
{
    public sealed class InstanceUpdatePlan
    {
        public Guid OperationId { get; set; } = Guid.NewGuid();
        public Guid InstanceId { get; set; }
        public string InstanceSlug { get; set; } = string.Empty;
        public string InstancePath { get; set; } = string.Empty;
        public EngineType CurrentEngineType { get; set; }
        public string CurrentVersion { get; set; } = string.Empty;
        public EngineType TargetEngineType { get; set; }
        public string TargetVersion { get; set; } = string.Empty;
        public int CurrentRequiredJavaVersion { get; set; }
        public int TargetRequiredJavaVersion { get; set; }
        public string RuntimeChangeText { get; set; } = string.Empty;
        public string ServerArtifactFileName { get; set; } = "server.jar";
    }

    public sealed class InstanceUpdateStagedArtifacts
    {
        public string StagingDirectory { get; set; } = string.Empty;
        public string ServerArtifactPath { get; set; } = string.Empty;
    }

    public sealed class InstanceUpdateApplyResult
    {
        public Guid OperationId { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public bool RestoredFromRollback { get; set; }
        public string TargetVersionApplied { get; set; } = string.Empty;
    }

    public sealed class UpdateTargetVersionInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = "release";
        public DateTime? ReleaseDate { get; set; }
        public bool IsCurrent { get; set; }
        public bool IsUpgrade { get; set; }
    }
}
