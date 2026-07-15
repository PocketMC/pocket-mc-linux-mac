using System.Threading.Tasks;

namespace PocketMC.RemoteControl.Tunnels;

public interface IRemoteTunnelProvider
{
    string ProviderId { get; }
    string DisplayName { get; }
    
    Task<bool> StartAsync();
    Task StopAsync();
    Task<string> GetStatusAsync();
}
