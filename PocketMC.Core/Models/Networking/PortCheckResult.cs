using System;
using System.Collections.Generic;
using System.Linq;

namespace PocketMC.Core.Models.Networking;

public sealed class PortCheckResult
{
    public PortCheckResult(
        PortCheckRequest request,
        bool isSuccessful,
        bool canBindLocally,
        PortFailureCode failureCode = PortFailureCode.None,
        string? failureMessage = null,
        PortLease? lease = null,
        IEnumerable<PortConflictInfo>? conflicts = null,
        IEnumerable<PortRecoveryRecommendation>? recommendations = null,
        DateTimeOffset? checkedAtUtc = null)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        IsSuccessful = isSuccessful;
        CanBindLocally = canBindLocally;
        FailureCode = failureCode;
        FailureMessage = failureMessage;
        Lease = lease;
        Conflicts = conflicts?.ToArray() ?? Array.Empty<PortConflictInfo>();
        Recommendations = recommendations?.ToArray() ?? Array.Empty<PortRecoveryRecommendation>();
        CheckedAtUtc = checkedAtUtc ?? DateTimeOffset.UtcNow;
    }

    public PortCheckRequest Request { get; }
    public bool IsSuccessful { get; }
    public bool CanBindLocally { get; }
    public PortFailureCode FailureCode { get; }
    public string? FailureMessage { get; }
    public PortLease? Lease { get; }
    public IReadOnlyList<PortConflictInfo> Conflicts { get; }
    public IReadOnlyList<PortRecoveryRecommendation> Recommendations { get; }
    public DateTimeOffset CheckedAtUtc { get; }
    public bool HasConflicts => Conflicts.Count > 0;
}
