using System;
using System.Threading.Tasks;

namespace PocketMC.Core.Services
{
    public interface IPHPService
    {
        Task<string> GetPHPExecutablePathAsync(string version);
        Task<bool> ValidatePHPRuntimeAsync(string executablePath, string expectedVersion);
        Task ProvisionPHPRuntimeAsync(string version, IProgress<double>? progress = null);
    }
}
