using System;
using System.Net.Sockets;
using PocketMC.Core.Models.Networking;

namespace PocketMC.Infrastructure.Networking;

public static class PortProbeFailureClassifier
{
    public static PortFailureCode Classify(SocketException exception, PortProtocol protocol, AddressFamily addressFamily)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.SocketErrorCode switch
        {
            SocketError.AccessDenied => PortFailureCode.AccessDenied,
            SocketError.AddressAlreadyInUse => protocol == PortProtocol.Udp
                ? PortFailureCode.UdpConflict
                : PortFailureCode.TcpConflict,
            SocketError.AddressFamilyNotSupported => PortFailureCode.UnsupportedProtocolOrAddressFamily,
            SocketError.ProtocolFamilyNotSupported => PortFailureCode.UnsupportedProtocolOrAddressFamily,
            SocketError.ProtocolNotSupported => PortFailureCode.UnsupportedProtocolOrAddressFamily,
            SocketError.OperationNotSupported => PortFailureCode.UnsupportedProtocolOrAddressFamily,
            SocketError.AddressNotAvailable => MapBindFailure(addressFamily),
            SocketError.InvalidArgument => MapBindFailure(addressFamily),
            SocketError.Fault => MapBindFailure(addressFamily),
            _ => PortFailureCode.UnknownSocketFailure
        };
    }

    public static PortFailureCode Classify(Exception exception, AddressFamily addressFamily)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            PlatformNotSupportedException => PortFailureCode.UnsupportedProtocolOrAddressFamily,
            NotSupportedException => PortFailureCode.UnsupportedProtocolOrAddressFamily,
            ObjectDisposedException => PortFailureCode.UnknownSocketFailure,
            _ => MapBindFailure(addressFamily)
        };
    }

    private static PortFailureCode MapBindFailure(AddressFamily addressFamily)
    {
        return addressFamily == AddressFamily.InterNetworkV6
            ? PortFailureCode.IPv6BindFailure
            : PortFailureCode.IPv4BindFailure;
    }
}
