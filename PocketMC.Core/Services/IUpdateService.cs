using System.Threading.Tasks;

namespace PocketMC.Core.Services
{
    public class UpdateCheckResult
    {
        public bool IsUpdateAvailable { get; set; }
        public string CurrentVersion { get; set; } = string.Empty;
        public string LatestVersionTag { get; set; } = string.Empty;
        public string ReleaseUrl { get; set; } = string.Empty;
    }

    public interface IUpdateService
    {
        Task<UpdateCheckResult> CheckForUpdatesAsync();
    }
}
