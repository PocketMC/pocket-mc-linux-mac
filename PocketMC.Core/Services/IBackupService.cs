using System;
using System.Threading.Tasks;
using PocketMC.Core.Models;

namespace PocketMC.Core.Services
{
    public interface IBackupService
    {
        Task<BackupMetadataEntry> CreateBackupAsync(
            ServerInstance instance,
            BackupTrigger trigger,
            string? label = null,
            string? notes = null,
            Action<string>? onProgress = null,
            IProgress<double>? progress = null);

        Task RestoreBackupAsync(
            ServerInstance instance,
            string backupZipPath,
            Action<string>? onProgress = null,
            IProgress<double>? progress = null);

        bool? VerifyBackupIntegrity(ServerInstance instance, string fileName, string? zipPath = null);
    }
}
