using System.Threading;
using System.Threading.Tasks;
using PocketMC.Core.Models.Networking;

namespace PocketMC.Core.Services;

public interface IPosixPortOwnerResolver
{
    Task<(int? ProcessId, string? ProcessName)> ResolveOwnerAsync(int port, PortProtocol protocol, CancellationToken cancellationToken = default);
}
