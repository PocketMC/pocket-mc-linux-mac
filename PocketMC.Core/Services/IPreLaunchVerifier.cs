using System.Threading.Tasks;

namespace PocketMC.Core.Services
{
    public interface IPreLaunchVerifier
    {
        Task<bool> VerifyJavaAsync(string version);
        Task<bool> VerifyPHPAsync(string version);
    }
}
