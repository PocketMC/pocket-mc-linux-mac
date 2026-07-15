using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PocketMC.RemoteControl.Services;

public class LocalNetworkAddressService
{
    public IEnumerable<string> GetLocalIPv4Addresses()
    {
        var addresses = new List<string>();

        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus == OperationalStatus.Up &&
                networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            {
                var properties = networkInterface.GetIPProperties();
                foreach (var address in properties.UnicastAddresses)
                {
                    if (address.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !System.Net.IPAddress.IsLoopback(address.Address))
                    {
                        addresses.Add(address.Address.ToString());
                    }
                }
            }
        }

        return addresses.Distinct();
    }
}
