using System;
using System.Net.Sockets;
using PocketMC.Core.Models.Networking;
using PocketMC.Infrastructure.Networking;
using PocketMC.Platform.Networking;
using Xunit;

namespace PocketMC.Tests;

public class PortReliabilityEngineTests
{
    [Fact]
    public void PortProbeFailureClassifier_AddressAlreadyInUse_ClassifiesAsTcpConflict()
    {
        var socketException = new SocketException((int)SocketError.AddressAlreadyInUse);
        var failureCode = PortProbeFailureClassifier.Classify(socketException, PortProtocol.Tcp, AddressFamily.InterNetwork);

        Assert.Equal(PortFailureCode.TcpConflict, failureCode);
    }

    [Fact]
    public void PortLeaseRegistry_ReservingOverlappingPortForDifferentInstance_FailsAndReturnsConflictingLease()
    {
        var registry = new PortLeaseRegistry();
        var instanceA = Guid.NewGuid();
        var instanceB = Guid.NewGuid();

        var leaseA = new PortLease(25565, PortProtocol.Tcp, PortIpMode.IPv4, instanceA, "Instance A");
        var leaseB = new PortLease(25565, PortProtocol.Tcp, PortIpMode.IPv4, instanceB, "Instance B");

        bool reservedA = registry.TryReserve(leaseA, out var conflictA);
        Assert.True(reservedA);
        Assert.Null(conflictA);

        bool reservedB = registry.TryReserve(leaseB, out var conflictB);
        Assert.False(reservedB);
        Assert.NotNull(conflictB);
        Assert.Equal(instanceA, conflictB.InstanceId);
    }

    [Fact]
    public void PosixPortOwnerResolver_ParsesSsAndLsofOutputs()
    {
        string ssOutput = "LISTEN 0 128 0.0.0.0:25565 0.0.0.0:* users:((\"java\",pid=12345,fd=45))";
        bool parsedSs = PosixPortOwnerResolver.TryParseSsOutput(ssOutput, 25565, out int pidSs, out string nameSs);
        Assert.True(parsedSs);
        Assert.Equal(12345, pidSs);
        Assert.Equal("java", nameSs);

        string lsofOutput = "COMMAND   PID USER   FD   TYPE DEVICE SIZE/OFF NODE NAME\njava    54321 user   45u  IPv4 0x1234      0t0  TCP *:25565 (LISTEN)";
        bool parsedLsof = PosixPortOwnerResolver.TryParseLsofOutput(lsofOutput, 25565, out int pidLsof, out string nameLsof);
        Assert.True(parsedLsof);
        Assert.Equal(54321, pidLsof);
        Assert.Equal("java", nameLsof);
    }

    [Fact]
    public void PortProbeService_ProbingInvalidPort_ReturnsInvalidRange()
    {
        var probeService = new PortProbeService(Microsoft.Extensions.Logging.Abstractions.NullLogger<PortProbeService>.Instance);
        var request = new PortCheckRequest(70000, PortProtocol.Tcp);

        var result = probeService.Probe(request);

        Assert.False(result.IsSuccessful);
        Assert.False(result.CanBindLocally);
        Assert.Equal(PortFailureCode.InvalidRange, result.FailureCode);
    }

    [Fact]
    public void PortRecoveryService_Recommend_OnInvalidRange_SuggestsValidPort()
    {
        var probeService = new PortProbeService(Microsoft.Extensions.Logging.Abstractions.NullLogger<PortProbeService>.Instance);
        var leaseRegistry = new PortLeaseRegistry();
        var recoveryService = new PortRecoveryService(probeService, leaseRegistry, Microsoft.Extensions.Logging.Abstractions.NullLogger<PortRecoveryService>.Instance);

        var request = new PortCheckRequest(70000, PortProtocol.Tcp);
        var failedResult = probeService.Probe(request);

        var recommendation = recoveryService.Recommend(failedResult);

        Assert.Equal(PortFailureCode.InvalidRange, recommendation.FailureCode);
        Assert.NotNull(recommendation.SuggestedPort);
        Assert.True(recommendation.SuggestedPort >= 1024 && recommendation.SuggestedPort <= 65535);
    }
}
