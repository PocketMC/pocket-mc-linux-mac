using PocketMC.Core.Models;

namespace PocketMC.Core.Services
{
    public interface ISettingsService
    {
        Settings Settings { get; }
        void Load();
        void Save();
        string GetSettingsDirectory();
        string GetDataRoot();
        string GetDefaultDataRoot();
        void SetDataRoot(string path);
        string GetInstancesDirectory();
        string GetBackupsDirectory();
        string GetDownloadsDirectory();
        string GetCacheDirectory();
        string GetLogsDirectory();
    }
}
