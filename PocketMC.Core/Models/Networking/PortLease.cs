using System;

namespace PocketMC.Core.Models.Networking;

public sealed class PortLease
{
    public PortLease(
        int port,
        PortProtocol protocol = PortProtocol.Tcp,
        PortIpMode ipMode = PortIpMode.IPv4,
        Guid? instanceId = null,
        string? instanceName = null,
        string? instancePath = null,
        string? bindAddress = null,
        DateTimeOffset? acquiredAtUtc = null)
    {
        Port = port;
        Protocol = protocol;
        IpMode = ipMode;
        InstanceId = instanceId;
        InstanceName = instanceName;
        InstancePath = instancePath;
        BindAddress = bindAddress;
        AcquiredAtUtc = acquiredAtUtc ?? DateTimeOffset.UtcNow;
    }

    public int Port { get; }
    public PortProtocol Protocol { get; }
    public PortIpMode IpMode { get; }
    public Guid? InstanceId { get; }
    public string? InstanceName { get; }
    public string? InstancePath { get; }
    public string? BindAddress { get; }
    public DateTimeOffset AcquiredAtUtc { get; }
}
