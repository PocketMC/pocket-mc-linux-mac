using System;

namespace PocketMC.Core.Models
{
    public enum EngineType
    {
        VanillaJava,
        Paper,
        Fabric,
        Forge,
        NeoForge,
        PocketMine,
        Bedrock
    }

    public class ServerInstance
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public EngineType EngineType { get; set; }
        public string EngineVersion { get; set; } = string.Empty;
        public string JvmArgs { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastPlayedAt { get; set; }
        public DateTime? PinnedAt { get; set; }
        public int? ServerPort { get; set; } = 25565;
        public int MaxPlayers { get; set; } = 20;
        public int MaxRamMb { get; set; } = 2048;
        public int MinRamMb { get; set; } = 1024;

        // Backups
        public DateTime? LastBackupTime { get; set; }
        public int MaxBackupsToKeep { get; set; } = 5;
        public string? CustomBackupDirectory { get; set; }

        // Scheduled Maintenance
        public RebootScheduleSettings? RebootSchedule { get; set; }
        public DateTime? LastScheduledRebootTime { get; set; }
    }
}
