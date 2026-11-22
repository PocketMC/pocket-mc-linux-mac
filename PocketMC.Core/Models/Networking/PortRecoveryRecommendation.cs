using System;

namespace PocketMC.Core.Models.Networking;

public sealed class PortRecoveryRecommendation
{
    public PortRecoveryRecommendation(
        PortFailureCode failureCode,
        string title,
        string description,
        int? suggestedPort = null,
        string? suggestedBindAddress = null,
        PortProtocol? suggestedProtocol = null,
        PortIpMode? suggestedIpMode = null,
        bool canAutoApply = false,
        bool requiresUserAction = true,
        PortRecoveryAction action = PortRecoveryAction.Abort,
        bool isTransient = false,
        TimeSpan? retryDelay = null,
        int attemptNumber = 0,
        int maxAttempts = 0)
    {
        FailureCode = failureCode;
        Title = title;
        Description = description;
        SuggestedPort = suggestedPort;
        SuggestedBindAddress = suggestedBindAddress;
        SuggestedProtocol = suggestedProtocol;
        SuggestedIpMode = suggestedIpMode;
        CanAutoApply = canAutoApply;
        RequiresUserAction = requiresUserAction;
        Action = action;
        IsTransient = isTransient;
        RetryDelay = retryDelay;
        AttemptNumber = attemptNumber;
        MaxAttempts = maxAttempts;
    }

    public PortFailureCode FailureCode { get; }
    public string Title { get; }
    public string Description { get; }
    public int? SuggestedPort { get; }
    public string? SuggestedBindAddress { get; }
    public PortProtocol? SuggestedProtocol { get; }
    public PortIpMode? SuggestedIpMode { get; }
    public bool CanAutoApply { get; }
    public bool RequiresUserAction { get; }
    public PortRecoveryAction Action { get; }
    public bool IsTransient { get; }
    public TimeSpan? RetryDelay { get; }
    public int AttemptNumber { get; }
    public int MaxAttempts { get; }
}
