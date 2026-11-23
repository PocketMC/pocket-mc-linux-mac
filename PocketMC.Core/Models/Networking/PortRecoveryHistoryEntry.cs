using System;

namespace PocketMC.Core.Models.Networking;

public sealed class PortRecoveryHistoryEntry
{
    public PortRecoveryHistoryEntry(
        DateTimeOffset timestampUtc,
        int port,
        PortProtocol protocol,
        PortFailureCode failureCode,
        PortRecoveryAction recommendedAction,
        int? suggestedPort,
        bool isTransient,
        int attemptNumber)
    {
        TimestampUtc = timestampUtc;
        Port = port;
        Protocol = protocol;
        FailureCode = failureCode;
        RecommendedAction = recommendedAction;
        SuggestedPort = suggestedPort;
        IsTransient = isTransient;
        AttemptNumber = attemptNumber;
    }

    public DateTimeOffset TimestampUtc { get; }
    public int Port { get; }
    public PortProtocol Protocol { get; }
    public PortFailureCode FailureCode { get; }
    public PortRecoveryAction RecommendedAction { get; }
    public int? SuggestedPort { get; }
    public bool IsTransient { get; }
    public int AttemptNumber { get; }
}
