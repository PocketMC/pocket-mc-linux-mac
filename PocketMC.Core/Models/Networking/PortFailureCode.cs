namespace PocketMC.Core.Models.Networking;

public enum PortFailureCode
{
    None = 0,
    InvalidRange,
    ReservedOrPrivilegedPort,
    InUseByPocketMcInstance,
    InUseByExternalProcess,
    AccessDenied,
    IPv4BindFailure,
    IPv6BindFailure,
    TcpConflict,
    UdpConflict,
    UnsupportedProtocolOrAddressFamily,
    TunnelLimitReached,
    PlayitAgentOffline,
    PlayitTokenInvalid,
    PlayitClaimRequired,
    PublicReachabilityFailure,
    UnknownSocketFailure,
    UnknownPortFailure
}
