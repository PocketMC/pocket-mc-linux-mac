using System.Collections.Generic;
using System.Threading.Tasks;
using PocketMC.Core.Models;

namespace PocketMC.Core.Services
{
    public interface IInstanceService
    {
        Task<ServerInstance> CreateInstanceAsync(string name, EngineType engineType, string version);
        Task DeleteInstanceAsync(string slug);
        Task<ServerInstance> RenameInstanceAsync(string slug, string newName);
        Task<ServerInstance> CloneInstanceAsync(string slug, string newName);
        Task SaveInstanceAsync(ServerInstance instance);
        Task ExportInstanceAsync(string slug, string targetZipPath);
        Task<ServerInstance> ImportInstanceAsync(string sourceZipPath, string name);
        Task<List<ServerInstance>> ListInstancesAsync();
    }
}
