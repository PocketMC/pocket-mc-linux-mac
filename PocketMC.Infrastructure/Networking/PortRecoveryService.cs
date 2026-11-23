using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using PocketMC.Core.Models.Networking;
using PocketMC.Core.Services;

namespace PocketMC.Infrastructure.Networking;

public sealed class PortRecoveryService
{
    private const int MinValidPort = 1;
    private const int MaxValidPort = 65535;
    private const int MaxPrivilegedPort = 1023;
    private const int DefaultJavaPort = 25565;
    private const int DefaultBedrockPort = 19132;
    private const int DefaultPortSearchLimit = 256;
    private const int DefaultMaxRetryAttempts = 3;
    private const int MaxHistoryEntries = 100;
    private static readonly TimeSpan BaseBackoffDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxBackoffDelay = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan RecentLeaseWindow = TimeSpan.FromSeconds(30);

    private readonly PortProbeService _portProbeService;
    private readonly PortLeaseRegistry _portLeaseRegistry;
    private readonly ILogger<PortRecoveryService> _logger;
    private readonly ConcurrentQueue<PortRecoveryHistoryEntry> _history = new();

    public PortRecoveryService(
        PortProbeService portProbeService,
        PortLeaseRegistry portLeaseRegistry,
        ILogger<PortRecoveryService> logger)
    {
        _portProbeService = portProbeService;
        _portLeaseRegistry = portLeaseRegistry;
        _logger = logger;
    }

    public PortRecoveryRecommendation Recommend(
        PortCheckResult result,
        int attemptNumber = 0,
        bool allowAutoPortSwitch = false,
        int maxRetryAttempts = DefaultMaxRetryAttempts)
    {
        ArgumentNullException.ThrowIfNull(result);
        ValidateRetryArguments(attemptNumber, maxRetryAttempts);

        if (result.IsSuccessful || result.FailureCode == PortFailureCode.None)
        {
            PortRecoveryRecommendation recommendation = new(
                PortFailureCode.None,
                "No port recovery needed",
                "The port check completed successfully.",
                suggestedPort: result.Request.Port,
                suggestedProtocol: result.Request.Protocol,
                suggestedIpMode: result.Request.IpMode,
                canAutoApply: false,
                requiresUserAction: false,
                action: PortRecoveryAction.None,
                isTransient: false,
                retryDelay: null,
                attemptNumber: attemptNumber,
                maxAttempts: maxRetryAttempts);

            RecordHistory(result, recommendation);
            return recommendation;
        }

        if (ShouldRetrySamePort(result, attemptNumber, maxRetryAttempts))
        {
            PortRecoveryRecommendation recommendation = CreateBackoffRecommendation(result, attemptNumber, maxRetryAttempts);
            RecordHistory(result, recommendation);
            return recommendation;
        }

        PortRecoveryRecommendation finalRecommendation = result.FailureCode switch
        {
            PortFailureCode.InvalidRange => CreatePortChangeRecommendation(
                result,
                "Use a valid server port",
                $"{result.Request.DisplayName} port {result.Request.Port} is outside the valid range {MinValidPort}-{MaxValidPort}.",
                allowAutoPortSwitch,
                attemptNumber,
                maxRetryAttempts),

            PortFailureCode.ReservedOrPrivilegedPort => CreatePortChangeRecommendation(
                result,
                "Use a non-privileged server port",
                $"{result.Request.DisplayName} port {result.Request.Port} is in the privileged/reserved range. Ports above {MaxPrivilegedPort} do not require elevated privileges.",
                allowAutoPortSwitch,
                attemptNumber,
                maxRetryAttempts),

            PortFailureCode.TcpConflict or
            PortFailureCode.UdpConflict or
            PortFailureCode.InUseByExternalProcess => CreatePortChangeRecommendation(
                result,
                "Choose an available server port",
                $"{result.Request.DisplayName} port {result.Request.Port} is still unavailable.",
                allowAutoPortSwitch,
                attemptNumber,
                maxRetryAttempts),

            PortFailureCode.InUseByPocketMcInstance => CreatePocketMcConflictRecommendation(
                result,
                allowAutoPortSwitch,
                attemptNumber,
                maxRetryAttempts),

            PortFailureCode.IPv4BindFailure or
            PortFailureCode.IPv6BindFailure => CreateAbortRecommendation(
                result,
                "Fix the configured bind address",
                BuildBindFailureDescription(result),
                attemptNumber,
                maxRetryAttempts),

            PortFailureCode.AccessDenied => CreateAbortRecommendation(
                result,
                "Fix port access permissions",
                $"Operating system denied access to port {result.Request.Port}. Choose a higher non-privileged port (>1024).",
                attemptNumber,
                maxRetryAttempts),

            _ => CreateAbortRecommendation(
                result,
                "Review the port failure",
                result.FailureMessage ?? "PocketMC could not recover from this port failure automatically.",
                attemptNumber,
                maxRetryAttempts)
        };

        RecordHistory(result, finalRecommendation);
        return finalRecommendation;
    }

    public int? FindNextFreePort(PortCheckRequest request, int searchLimit = DefaultPortSearchLimit)
    {
        ArgumentNullException.ThrowIfNull(request);

        foreach (int candidate in EnumerateCandidatePorts(request, searchLimit))
        {
            if (_portLeaseRegistry.IsPortLeased(candidate, request.Protocol, request.IpMode, request.BindAddress))
            {
                continue;
            }

            var probeResult = _portProbeService.Probe(new PortCheckRequest(
                candidate,
                request.Protocol,
                request.IpMode,
                request.BindAddress,
                request.InstanceId,
                request.InstanceName,
                request.InstancePath,
                checkTunnelAvailability: false,
                checkPublicReachability: false,
                request.BindingRole,
                request.Engine,
                request.DisplayName));

            if (probeResult.CanBindLocally && probeResult.IsSuccessful)
            {
                return candidate;
            }
        }

        return null;
    }

    public bool IsLikelyTransient(PortCheckResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.FailureCode switch
        {
            PortFailureCode.TcpConflict or PortFailureCode.UdpConflict => !HasStableExternalConflict(result),
            PortFailureCode.InUseByPocketMcInstance => HasRecentPocketMcLeaseConflict(result),
            PortFailureCode.IPv4BindFailure or PortFailureCode.IPv6BindFailure => true,
            PortFailureCode.PlayitAgentOffline => true,
            PortFailureCode.PublicReachabilityFailure => true,
            PortFailureCode.UnknownSocketFailure => true,
            PortFailureCode.UnknownPortFailure => true,
            _ => false
        };
    }

    public TimeSpan ComputeBackoffDelay(int attemptNumber)
    {
        if (attemptNumber <= 0)
        {
            return BaseBackoffDelay;
        }

        double multiplier = Math.Pow(2, Math.Min(attemptNumber, 4));
        TimeSpan delay = TimeSpan.FromMilliseconds(BaseBackoffDelay.TotalMilliseconds * multiplier);
        return delay > MaxBackoffDelay ? MaxBackoffDelay : delay;
    }

    private bool ShouldRetrySamePort(PortCheckResult result, int attemptNumber, int maxRetryAttempts)
    {
        return attemptNumber < maxRetryAttempts && IsLikelyTransient(result);
    }

    private PortRecoveryRecommendation CreateBackoffRecommendation(PortCheckResult result, int attemptNumber, int maxRetryAttempts)
    {
        TimeSpan delay = ComputeBackoffDelay(attemptNumber);
        return new PortRecoveryRecommendation(
            result.FailureCode,
            "Wait and retry the same port",
            $"{result.Request.DisplayName} port {result.Request.Port} encountered a transient issue. Retrying in {delay.TotalSeconds:0.#}s.",
            suggestedPort: result.Request.Port,
            suggestedBindAddress: result.Request.BindAddress,
            suggestedProtocol: result.Request.Protocol,
            suggestedIpMode: result.Request.IpMode,
            canAutoApply: true,
            requiresUserAction: false,
            action: PortRecoveryAction.WaitWithBackoff,
            isTransient: true,
            retryDelay: delay,
            attemptNumber: attemptNumber,
            maxAttempts: maxRetryAttempts);
    }

    private PortRecoveryRecommendation CreatePortChangeRecommendation(
        PortCheckResult result,
        string title,
        string description,
        bool allowAutoPortSwitch,
        int attemptNumber,
        int maxRetryAttempts)
    {
        int? suggestedPort = FindNextFreePort(result.Request);
        PortRecoveryAction action = allowAutoPortSwitch && suggestedPort.HasValue
            ? PortRecoveryAction.AutoSwitchToNextFreePort
            : suggestedPort.HasValue
                ? PortRecoveryAction.SuggestNextFreePort
                : PortRecoveryAction.Abort;

        string actionText = suggestedPort.HasValue
            ? $"Port {suggestedPort.Value} is verified available."
            : "No available alternate port could be found.";

        return new PortRecoveryRecommendation(
            result.FailureCode,
            title,
            $"{description} {actionText}",
            suggestedPort: suggestedPort,
            suggestedBindAddress: result.Request.BindAddress,
            suggestedProtocol: result.Request.Protocol,
            suggestedIpMode: result.Request.IpMode,
            canAutoApply: allowAutoPortSwitch && suggestedPort.HasValue,
            requiresUserAction: !(allowAutoPortSwitch && suggestedPort.HasValue),
            action: action,
            isTransient: false,
            retryDelay: null,
            attemptNumber: attemptNumber,
            maxAttempts: maxRetryAttempts);
    }

    private PortRecoveryRecommendation CreatePocketMcConflictRecommendation(
        PortCheckResult result,
        bool allowAutoPortSwitch,
        int attemptNumber,
        int maxRetryAttempts)
    {
        PortConflictInfo? conflict = result.Conflicts.FirstOrDefault(x => x.ExistingLease != null);
        string owner = conflict?.ExistingLease?.InstanceName ?? "another PocketMC instance";

        return CreatePortChangeRecommendation(
            result,
            "Resolve the PocketMC port conflict",
            $"{result.Request.DisplayName} port {result.Request.Port} is already assigned to {owner}. Stop the other instance or use a different port.",
            allowAutoPortSwitch,
            attemptNumber,
            maxRetryAttempts);
    }

    private static PortRecoveryRecommendation CreateAbortRecommendation(
        PortCheckResult result,
        string title,
        string description,
        int attemptNumber,
        int maxRetryAttempts)
    {
        return new PortRecoveryRecommendation(
            result.FailureCode,
            title,
            description,
            suggestedPort: null,
            suggestedBindAddress: result.Request.BindAddress,
            suggestedProtocol: result.Request.Protocol,
            suggestedIpMode: result.Request.IpMode,
            canAutoApply: false,
            requiresUserAction: true,
            action: PortRecoveryAction.Abort,
            isTransient: false,
            retryDelay: null,
            attemptNumber: attemptNumber,
            maxAttempts: maxRetryAttempts);
    }

    private static string BuildBindFailureDescription(PortCheckResult result)
    {
        string ipMode = result.FailureCode == PortFailureCode.IPv6BindFailure ? "IPv6" : "IPv4";
        if (!string.IsNullOrWhiteSpace(result.Request.BindAddress))
        {
            return $"PocketMC could not bind {ipMode} on '{result.Request.BindAddress}:{result.Request.Port}'. Check the configured server IP or clear the bind address.";
        }

        return $"PocketMC could not bind {ipMode} on port {result.Request.Port}. Check whether the local network stack supports this binding.";
    }

    private static void ValidateRetryArguments(int attemptNumber, int maxRetryAttempts)
    {
        if (attemptNumber < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptNumber), "Attempt number cannot be negative.");
        }

        if (maxRetryAttempts < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetryAttempts), "Maximum retry attempts cannot be negative.");
        }
    }

    private static bool HasStableExternalConflict(PortCheckResult result)
    {
        return result.Conflicts.Any(conflict =>
            conflict.FailureCode == PortFailureCode.InUseByExternalProcess ||
            conflict.IsExternalProcessConflict);
    }

    private static bool HasRecentPocketMcLeaseConflict(PortCheckResult result)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return result.Conflicts.Any(conflict =>
            conflict.ExistingLease != null &&
            conflict.ExistingLease.AcquiredAtUtc != DateTimeOffset.MinValue &&
            now - conflict.ExistingLease.AcquiredAtUtc <= RecentLeaseWindow);
    }

    private static IEnumerable<int> EnumerateCandidatePorts(PortCheckRequest request, int searchLimit)
    {
        int defaultPort = request.Protocol == PortProtocol.Udp ? DefaultBedrockPort : DefaultJavaPort;
        int startPort = IsValidPort(request.Port)
            ? Math.Min(request.Port + 1, MaxValidPort)
            : defaultPort;

        var yielded = new HashSet<int>();
        foreach (int candidate in EnumerateCandidateRange(startPort, searchLimit, yielded))
        {
            yield return candidate;
        }

        foreach (int candidate in EnumerateCandidateRange(defaultPort, searchLimit - yielded.Count, yielded))
        {
            yield return candidate;
        }
    }

    private static IEnumerable<int> EnumerateCandidateRange(int startPort, int remainingLimit, HashSet<int> yielded)
    {
        if (remainingLimit <= 0)
        {
            yield break;
        }

        for (int p = startPort; p <= MaxValidPort && yielded.Count < remainingLimit; p++)
        {
            if (p <= MaxPrivilegedPort)
            {
                continue;
            }

            if (yielded.Add(p))
            {
                yield return p;
            }
        }
    }

    private static bool IsValidPort(int port) => port >= MinValidPort && port <= MaxValidPort;

    private void RecordHistory(PortCheckResult result, PortRecoveryRecommendation recommendation)
    {
        _history.Enqueue(new PortRecoveryHistoryEntry(
            DateTimeOffset.UtcNow,
            result.Request.Port,
            result.Request.Protocol,
            result.FailureCode,
            recommendation.Action,
            recommendation.SuggestedPort,
            recommendation.IsTransient,
            recommendation.AttemptNumber));

        while (_history.Count > MaxHistoryEntries && _history.TryDequeue(out _)) { }
    }
}
