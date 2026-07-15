using System.Threading.Tasks;

namespace PocketMC.Core.Services
{
    public interface ICurseForgeApiKeyDialogService
    {
        Task<string?> PromptForApiKeyAsync();
    }
}
