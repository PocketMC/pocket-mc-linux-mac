using System.Collections.Generic;
using System.Threading.Tasks;
using PocketMC.Core.Models;

namespace PocketMC.Core.Services
{
    public interface IAddonProvider
    {
        string Name { get; }
        Task<MarketplaceVersion?> GetLatestVersionAsync(string projectId, string mcVersion, string loader);
        Task<MarketplaceVersion?> GetLatestVersionAsync(string projectId, string mcVersion, IReadOnlyList<string> loaderCandidates);
        Task<MarketplaceVersion?> GetVersionByIdAsync(string versionId);
        Task<MarketplaceProjectInfo?> GetProjectInfoAsync(string projectId);
    }
}
