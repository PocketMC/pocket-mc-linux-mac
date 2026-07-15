using System.Linq;
using System.Net.NetworkInformation;

namespace PocketMC.Infrastructure.Services
{
    public static class PortProber
    {
        public static bool IsPortListening(int port, bool isUdp)
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();

            if (isUdp)
            {
                // Bedrock Dedicated Server / PocketMine (UDP)
                var listeners = properties.GetActiveUdpListeners();
                return listeners.Any(endpoint => endpoint.Port == port);
            }
            else
            {
                // Minecraft Java Edition (TCP)
                var listeners = properties.GetActiveTcpListeners();
                return listeners.Any(endpoint => endpoint.Port == port);
            }
        }
    }
}
