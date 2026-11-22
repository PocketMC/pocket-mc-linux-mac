using System;

namespace PocketMC.Core.Models.Networking;

public sealed class PortConflictInfo
{
    public PortConflictInfo(
        PortFailureCode failureCode,
        int port,
        PortProtocol protocol = PortProtocol.Tcp,
        PortIpMode ipMode = PortIpMode.IPv4,
        string? bindAddress = null,
        PortLease? existingLease = null,
        int? processId = null,
        string? processName = null,
        string? details = null)
    {
        FailureCode = failureCode;
        Port = port;
        Protocol = protocol;
        IpMode = ipMode;
        BindAddress = bindAddress;
        ExistingLease = existingLease;
        ProcessId = processId;
        ProcessName = processName;
        Details = details;
    }

    public PortFailureCode FailureCode { get; }
    public int Port { get; }
    public PortProtocol Protocol { get; }
    public PortIpMode IpMode { get; }
    public string? BindAddress { get; }
    public PortLease? ExistingLease { get; }
    public int? ProcessId { get; }
    public string? ProcessName { get; }
    public string? Details { get; }
    public bool IsPocketMcConflict => ExistingLease != null;
    public bool IsExternalProcessConflict => ProcessId.HasValue || !string.IsNullOrWhiteSpace(ProcessName);
}
