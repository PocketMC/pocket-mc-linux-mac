using System;

namespace PocketMC.Core.Models
{
    public sealed record RebootScheduleSettings
    {
        public bool Enabled { get; init; }
        public string Mode { get; init; } = "Daily";
        public string Time { get; init; } = "04:00";
        public int IntervalHours { get; init; } = 24;
        public bool WarningEnabled { get; init; } = true;
        public int WarningSeconds { get; init; } = 60;
    }
}
