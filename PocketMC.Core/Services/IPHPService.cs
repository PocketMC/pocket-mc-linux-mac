using System;
using System.Threading.Tasks;

namespace PocketMC.Core.Services
{
    public interface IPHPService
    {
        Task<string> GetPHPExecutablePathAsync(string version);
        Task<bool> ValidatePHPRuntimeAsync(string executablePath, string expectedVersion);
        Task ProvisionPHPRuntimeAsync(string version, IProgress<double>? progress = null);
        Task<bool> IsPHPRuntimeInstalledAsync(string version) => Task.FromResult(false);
        Task DeletePHPRuntimeAsync(string version) => Task.CompletedTask;
        string? GetPHPRuntimeDirectory(string version) => null;
    }
}
