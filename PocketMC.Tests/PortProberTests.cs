using System.Net;
using System.Net.Sockets;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests
{
    public class PortProberTests
    {
        [Fact]
        public void PortProber_DetectsActiveTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            try
            {
                Assert.True(PortProber.IsPortListening(port, false), "TCP port should be reported as listening");
            }
            finally
            {
                listener.Stop();
            }

            Assert.False(PortProber.IsPortListening(port, false), "TCP port should not be listening after stop");
        }

        [Fact]
        public void PortProber_DetectsActiveUdpPort()
        {
            var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            int port = ((IPEndPoint)client.Client.LocalEndPoint!).Port;

            try
            {
                Assert.True(PortProber.IsPortListening(port, true), "UDP port should be reported as listening");
            }
            finally
            {
                client.Close();
            }

            Assert.False(PortProber.IsPortListening(port, true), "UDP port should not be listening after close");
        }
    }
}
