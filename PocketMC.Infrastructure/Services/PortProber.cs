using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PocketMC.Infrastructure.Services
{
    public static class PortProber
    {
        public static bool IsPortListening(int port, bool isUdp)
        {
            if (port < 1 || port > 65535) return false;

            try
            {
                var properties = IPGlobalProperties.GetIPGlobalProperties();

                if (isUdp)
                {
                    var listeners = properties.GetActiveUdpListeners();
                    if (listeners.Any(endpoint => endpoint.Port == port))
                    {
                        return true;
                    }

                    try
                    {
                        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                        socket.ExclusiveAddressUse = true;
                        socket.Bind(new IPEndPoint(IPAddress.Any, port));
                        return false;
                    }
                    catch
                    {
                        return true;
                    }
                }
                else
                {
                    var listeners = properties.GetActiveTcpListeners();
                    if (listeners.Any(endpoint => endpoint.Port == port))
                    {
                        return true;
                    }

                    try
                    {
                        using var client = new TcpClient();
                        var ar = client.BeginConnect(IPAddress.Loopback, port, null, null);
                        if (ar.AsyncWaitHandle.WaitOne(150))
                        {
                            client.EndConnect(ar);
                            return true;
                        }
                    }
                    catch { }

                    try
                    {
                        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        socket.ExclusiveAddressUse = true;
                        socket.Bind(new IPEndPoint(IPAddress.Any, port));
                        return false;
                    }
                    catch
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
