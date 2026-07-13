using System.Threading.Tasks;
using PocketMC.Core.Services;

namespace PocketMC.Infrastructure.Services
{
    public class PreLaunchVerifier : IPreLaunchVerifier
    {
        private readonly IJavaService _javaService;
        private readonly IPHPService _phpService;

        public PreLaunchVerifier(IJavaService javaService, IPHPService phpService)
        {
            _javaService = javaService;
            _phpService = phpService;
        }

        public async Task<bool> VerifyJavaAsync(string version)
        {
            try
            {
                var path = await _javaService.GetJavaExecutablePathAsync(version);
                return await _javaService.ValidateJavaRuntimeAsync(path, version);
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> VerifyPHPAsync(string version)
        {
            try
            {
                var path = await _phpService.GetPHPExecutablePathAsync(version);
                return await _phpService.ValidatePHPRuntimeAsync(path, version);
            }
            catch
            {
                return false;
            }
        }
    }
}
