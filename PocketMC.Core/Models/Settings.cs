using System.Collections.Generic;

namespace PocketMC.Core.Models
{
    public class Settings
    {
        public int Version { get; set; } = 1;
        public string? CustomDataRoot { get; set; }
        public Dictionary<string, Dictionary<string, string>> DownloadedRuntimes { get; set; } = new Dictionary<string, Dictionary<string, string>>();
    }
}
