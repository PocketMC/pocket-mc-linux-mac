using System;
using System.Threading.Tasks;

namespace PocketMC.Core.Services
{
    public interface IJavaService
    {
        Task<string> GetJavaExecutablePathAsync(string version);
        Task<bool> ValidateJavaRuntimeAsync(string executablePath, string expectedVersion);
        Task ProvisionJavaRuntimeAsync(string version, IProgress<double>? progress = null);
    }
}
