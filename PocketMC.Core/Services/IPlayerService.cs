using System.Collections.Generic;
using System.Threading.Tasks;
using PocketMC.Core.Models;

namespace PocketMC.Core.Services
{
    public interface IPlayerService
    {
        Task<List<string>> GetOpsAsync(ServerInstance instance);
        Task AddOpAsync(ServerInstance instance, string username);
        Task RemoveOpAsync(ServerInstance instance, string username);
        Task<List<string>> GetWhitelistAsync(ServerInstance instance);
        Task AddWhitelistAsync(ServerInstance instance, string username);
        Task RemoveWhitelistAsync(ServerInstance instance, string username);
        Task<List<string>> GetOnlinePlayersAsync(ServerInstance instance);
    }
}
