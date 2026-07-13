using System.Threading.Tasks;

namespace PocketMC.Core.Services
{
    public interface ISecretStore
    {
        Task<string?> GetAsync(string key);
        Task SetAsync(string key, string value);
        Task DeleteAsync(string key);
    }
}
