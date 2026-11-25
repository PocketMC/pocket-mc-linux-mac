using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PocketMC.Core.Models;
using PocketMC.Core.Models.Networking;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Networking;

namespace PocketMC.Infrastructure.Services;

/// <summary>
/// Manages server instance lifecycle orchestration including port preflight checks,
/// lease reservation/release, Geyser UDP synchronization, restart tracking, and crash recovery.
/// </summary>
public sealed class ServerLifecycleService : IServerLifecycleService, IDisposable
{
    private readonly IProcessRunner _processRunner;
    private readonly IInstanceService _instanceService;
    private readonly PortProbeService _portProbeService;
    private readonly PortLeaseRegistry _portLeaseRegistry;
    private readonly PortRecoveryService _portRecoveryService;
    private readonly GeyserProvisioningService _geyserProvisioningService;
    private readonly IGeyserDetector _geyserDetector;
    private readonly INotificationService? _notificationService;
    private readonly SessionSummarizationService? _summarizationService;
    private readonly ISettingsService? _settingsService;
    private readonly ILogger<ServerLifecycleService> _logger;

    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _startLocks = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _restartCancellations = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _sessionStartTimes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Guid> _slugToInstanceId = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ServerInstance> _knownInstances = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _consecutiveRestarts = new(StringComparer.OrdinalIgnoreCase);

    public event Action<string, string>? InstanceStateChanged;
    public event Action<string, int>? RestartCountdownTick;

    public ServerLifecycleService(
        IProcessRunner processRunner,
        IInstanceService instanceService,
        PortProbeService portProbeService,
        PortLeaseRegistry portLeaseRegistry,
        PortRecoveryService portRecoveryService,
        GeyserProvisioningService geyserProvisioningService,
        IGeyserDetector geyserDetector,
        ILogger<ServerLifecycleService>? logger = null,
        INotificationService? notificationService = null,
        SessionSummarizationService? summarizationService = null,
        ISettingsService? settingsService = null)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _instanceService = instanceService ?? throw new ArgumentNullException(nameof(instanceService));
        _portProbeService = portProbeService ?? throw new ArgumentNullException(nameof(portProbeService));
        _portLeaseRegistry = portLeaseRegistry ?? throw new ArgumentNullException(nameof(portLeaseRegistry));
        _portRecoveryService = portRecoveryService ?? throw new ArgumentNullException(nameof(portRecoveryService));
        _geyserProvisioningService = geyserProvisioningService ?? throw new ArgumentNullException(nameof(geyserProvisioningService));
        _geyserDetector = geyserDetector ?? throw new ArgumentNullException(nameof(geyserDetector));
        _logger = logger ?? NullLogger<ServerLifecycleService>.Instance;
        _notificationService = notificationService;
        _summarizationService = summarizationService;
        _settingsService = settingsService;

        _processRunner.StateChanged += HandleProcessStateChanged;
    }

    public async Task StartAsync(ServerInstance instance, bool isAutoRestart = false)
    {
        ArgumentNullException.ThrowIfNull(instance);

        SemaphoreSlim startLock = _startLocks.GetOrAdd(instance.Id, static _ => new SemaphoreSlim(1, 1));
        bool leasesAcquired = false;
        int recoveryAttempt = 0;

        await startLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_processRunner.TryGetRunningInfo(instance.Slug, out _, out string state) &&
                (string.Equals(state, "Running", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(state, "Online", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(state, "Starting", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Server '{instance.Name}' is already running.");
            }

            if (string.IsNullOrWhiteSpace(instance.Path) || !Directory.Exists(instance.Path))
            {
                throw new DirectoryNotFoundException($"Could not locate directory for instance '{instance.Name}'.");
            }

            _slugToInstanceId[instance.Slug] = instance.Id;
            _knownInstances[instance.Slug] = instance;
            if (!isAutoRestart)
            {
                _consecutiveRestarts[instance.Slug] = 0;
            }

            if (_geyserDetector.IsGeyserInstalled(instance.Path))
            {
                _geyserProvisioningService.PatchGeyserConfigPort(instance.Path, instance.GeyserBedrockPort ?? 19132);
            }

            while (true)
            {
                try
                {
                    IReadOnlyList<PortCheckRequest> requests = BuildPortCheckRequests(instance);
                    PortCheckResult[] failedProbeResults = _portProbeService.ProbeMany(requests)
                        .Where(r => !r.IsSuccessful)
                        .ToArray();

                    if (failedProbeResults.Length > 0)
                    {
                        throw new PortReliabilityException(failedProbeResults[0]);
                    }

                    leasesAcquired = true;
                    PortCheckResult? leaseFailure = TryReservePortLeases(instance, requests);
                    if (leaseFailure != null)
                    {
                        throw new PortReliabilityException(leaseFailure);
                    }

                    break;
                }
                catch (PortReliabilityException ex) when (ex.Result != null && TryGetRecoveryDelay(ex.Result, recoveryAttempt, out TimeSpan retryDelay))
                {
                    if (leasesAcquired)
                    {
                        _portLeaseRegistry.ReleaseAll(instance.Id);
                        leasesAcquired = false;
                    }

                    _logger.LogWarning(
                        "Transient port issue on {Port} for '{Name}' (attempt {Attempt}), retrying in {DelayMs}ms.",
                        ex.Result.Request.Port, instance.Name, recoveryAttempt + 1, retryDelay.TotalMilliseconds);
                    recoveryAttempt++;
                    await Task.Delay(retryDelay).ConfigureAwait(false);
                }
            }

            _sessionStartTimes[instance.Slug] = DateTime.UtcNow;
            await _processRunner.StartAsync(instance, isAutoRestart).ConfigureAwait(false);
        }
        catch
        {
            if (leasesAcquired)
            {
                _portLeaseRegistry.ReleaseAll(instance.Id);
            }
            _sessionStartTimes.TryRemove(instance.Slug, out _);
            throw;
        }
        finally
        {
            startLock.Release();
        }
    }

    public async Task StopAsync(ServerInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        AbortRestartDelay(instance.Slug);
        await _processRunner.StopAsync(instance).ConfigureAwait(false);
        _portLeaseRegistry.ReleaseAll(instance.Id);
        _sessionStartTimes.TryRemove(instance.Slug, out _);
    }

    public async Task RestartAsync(ServerInstance instance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (!_restartCancellations.TryAdd(instance.Slug, linkedCts))
        {
            throw new InvalidOperationException($"A restart is already pending for '{instance.Name}'.");
        }

        try
        {
            await _processRunner.StopAsync(instance).ConfigureAwait(false);
            _portLeaseRegistry.ReleaseAll(instance.Id);
            linkedCts.Token.ThrowIfCancellationRequested();
            await StartAsync(instance, isAutoRestart: false).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // User aborted pending restart
        }
        finally
        {
            _restartCancellations.TryRemove(new KeyValuePair<string, CancellationTokenSource>(instance.Slug, linkedCts));
        }
    }

    public void Kill(ServerInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        AbortRestartDelay(instance.Slug);
        _ = _processRunner.StopAsync(instance);
        _portLeaseRegistry.ReleaseAll(instance.Id);
        _sessionStartTimes.TryRemove(instance.Slug, out _);
    }

    public void AbortRestartDelay(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return;
        }

        if (_restartCancellations.TryRemove(slug, out var cts))
        {
            try
            {
                cts.Cancel();
                cts.Dispose();
            }
            catch
            {
                // Ignore disposed cancellation token
            }

            InstanceStateChanged?.Invoke(slug, "Stopped");
        }
    }

    public bool IsRunning(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return false;
        }

        return _processRunner.TryGetRunningInfo(slug, out _, out _);
    }

    public bool IsWaitingToRestart(string slug) =>
        !string.IsNullOrWhiteSpace(slug) && _restartCancellations.ContainsKey(slug);

    public DateTime? GetSessionStartTime(string slug) =>
        _processRunner.GetSessionStartTime(slug) ??
        (_sessionStartTimes.TryGetValue(slug, out var time) ? time : null);

    public IReadOnlyList<PortCheckRequest> BuildPortCheckRequests(ServerInstance instance)
    {
        var requests = new List<PortCheckRequest>();
        bool isBedrock = instance.EngineType == EngineType.Bedrock || instance.EngineType == EngineType.PocketMine;
        int primaryPort = instance.ServerPort ?? (isBedrock ? 19132 : 25565);

        PortProtocol primaryProto = instance.EngineType switch
        {
            EngineType.Bedrock or EngineType.PocketMine => PortProtocol.Udp,
            EngineType.Pumpkin => PortProtocol.TcpAndUdp,
            _ => PortProtocol.Tcp
        };

        PortEngine portEngine = instance.EngineType switch
        {
            EngineType.Bedrock => PortEngine.BedrockDedicated,
            EngineType.PocketMine => PortEngine.PocketMine,
            EngineType.Pumpkin => PortEngine.PumpkinBedrock,
            _ => PortEngine.Java
        };

        PortBindingRole primaryRole = instance.EngineType switch
        {
            EngineType.Bedrock => PortBindingRole.BedrockServer,
            EngineType.PocketMine => PortBindingRole.PocketMineServer,
            _ => PortBindingRole.JavaServer
        };

        requests.Add(new PortCheckRequest(
            primaryPort,
            primaryProto,
            PortIpMode.DualStack,
            instanceId: instance.Id,
            instanceName: instance.Name,
            instancePath: instance.Path,
            bindingRole: primaryRole,
            engine: portEngine));

        if (!isBedrock && !string.IsNullOrWhiteSpace(instance.Path) && _geyserDetector.IsGeyserInstalled(instance.Path))
        {
            int geyserPort = instance.GeyserBedrockPort ?? 19132;
            requests.Add(new PortCheckRequest(
                geyserPort,
                PortProtocol.Udp,
                PortIpMode.DualStack,
                instanceId: instance.Id,
                instanceName: instance.Name,
                instancePath: instance.Path,
                bindingRole: PortBindingRole.GeyserBedrock,
                engine: PortEngine.Geyser));
        }

        if (instance.EngineType == EngineType.Pumpkin && instance.PumpkinBedrockPort is > 0)
        {
            requests.Add(new PortCheckRequest(
                instance.PumpkinBedrockPort.Value,
                PortProtocol.Udp,
                PortIpMode.DualStack,
                instanceId: instance.Id,
                instanceName: instance.Name,
                instancePath: instance.Path,
                bindingRole: PortBindingRole.PumpkinBedrock,
                engine: PortEngine.PumpkinBedrock));
        }

        if (instance.SimpleVoiceChatPort is > 0)
        {
            requests.Add(new PortCheckRequest(
                instance.SimpleVoiceChatPort.Value,
                PortProtocol.Udp,
                PortIpMode.DualStack,
                instanceId: instance.Id,
                instanceName: instance.Name,
                instancePath: instance.Path,
                bindingRole: PortBindingRole.SimpleVoiceChat,
                engine: PortEngine.SimpleVoiceChat));
        }

        return requests;
    }

    private PortCheckResult? TryReservePortLeases(ServerInstance instance, IReadOnlyList<PortCheckRequest> requests)
    {
        foreach (PortCheckRequest request in requests)
        {
            var lease = new PortLease(
                request.Port,
                request.Protocol,
                request.IpMode,
                instance.Id,
                instance.Name,
                instance.Path,
                request.BindAddress);

            if (!_portLeaseRegistry.TryReserve(lease, out PortLease? conflict))
            {
                string ownerName = conflict?.InstanceName ?? "another instance";
                return new PortCheckResult(
                    request,
                    isSuccessful: false,
                    canBindLocally: false,
                    failureCode: PortFailureCode.InUseByPocketMcInstance,
                    failureMessage: $"Port {request.Port} ({request.Protocol}) is already leased by '{ownerName}'.",
                    lease: conflict);
            }
        }

        return null;
    }

    private bool TryGetRecoveryDelay(PortCheckResult failedResult, int attemptNumber, out TimeSpan delay)
    {
        PortRecoveryRecommendation recommendation = _portRecoveryService.Recommend(failedResult, attemptNumber);
        if (recommendation.IsTransient && recommendation.RetryDelay.HasValue && recommendation.RetryDelay.Value > TimeSpan.Zero)
        {
            delay = recommendation.RetryDelay.Value;
            return true;
        }

        delay = TimeSpan.Zero;
        return false;
    }

    private void HandleProcessStateChanged(string slug, string state)
    {
        bool isStopped = string.Equals(state, "Stopped", StringComparison.OrdinalIgnoreCase);
        bool isCrashed = string.Equals(state, "Crashed", StringComparison.OrdinalIgnoreCase);

        if ((isStopped || isCrashed) &&
            _slugToInstanceId.TryGetValue(slug, out Guid instanceId))
        {
            _portLeaseRegistry.ReleaseAll(instanceId);
            DateTime sessionStart = _sessionStartTimes.TryRemove(slug, out DateTime started)
                ? started
                : DateTime.UtcNow.AddMinutes(-5);

            if (_knownInstances.TryGetValue(slug, out var instance))
            {
                TryTriggerAutoSummarize(instance, sessionStart, isCrashed);

                if (isCrashed && instance.EnableAutoRestart)
                {
                    _ = ScheduleAutoRestartAsync(instance);
                }
            }
        }

        InstanceStateChanged?.Invoke(slug, state);
    }

    private void TryTriggerAutoSummarize(ServerInstance instance, DateTime sessionStart, bool isCrashed)
    {
        if (_summarizationService == null || _settingsService == null)
        {
            return;
        }

        Settings settings = _settingsService.Settings;
        if (!settings.EnableAiSummarization || !settings.IsAiConfigured())
        {
            return;
        }

        if ((isCrashed && !settings.AutoSummarizeOnCrash) || (!isCrashed && !settings.AutoSummarizeOnStop))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(instance.Path) || !Directory.Exists(instance.Path))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                SummarizationResult result = await _summarizationService.SummarizeAsync(
                    instance.Path,
                    instance.Name,
                    settings,
                    sessionStart,
                    DateTime.UtcNow).ConfigureAwait(false);

                if (result.Success && settings.EnableAiSummaryNotifications)
                {
                    _notificationService?.ShowInformation(
                        "AI Session Summary Ready",
                        $"Session summary generated for {instance.Name}.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Background auto-summarization failed for server '{ServerName}'.", instance.Name);
            }
        });
    }

    private async Task ScheduleAutoRestartAsync(ServerInstance instance)
    {
        int attempts = _consecutiveRestarts.GetOrAdd(instance.Slug, 0);
        if (attempts >= instance.MaxAutoRestarts)
        {
            _notificationService?.ShowInformation("Restart Limit Reached",
                $"Server '{instance.Name}' stopped after {attempts} failed restarts.");
            return;
        }

        var cts = new CancellationTokenSource();
        if (!_restartCancellations.TryAdd(instance.Slug, cts))
        {
            cts.Dispose();
            return;
        }

        int delay = (int)Math.Min(Math.Max(1, instance.AutoRestartDelaySeconds) * Math.Pow(2, attempts), 300);

        try
        {
            for (int remaining = delay; remaining > 0; remaining--)
            {
                if (cts.Token.IsCancellationRequested)
                {
                    return;
                }

                RestartCountdownTick?.Invoke(instance.Slug, remaining);
                await Task.Delay(1000, cts.Token).ConfigureAwait(false);
            }

            _consecutiveRestarts[instance.Slug] = attempts + 1;
            await StartAsync(instance, isAutoRestart: true).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancelled by explicit Stop / Kill / AbortRestartDelay
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-restart failed for server '{ServerName}'.", instance.Name);
        }
        finally
        {
            _restartCancellations.TryRemove(new KeyValuePair<string, CancellationTokenSource>(instance.Slug, cts));
            cts.Dispose();
        }
    }

    public void Dispose()
    {
        _processRunner.StateChanged -= HandleProcessStateChanged;

        foreach (var cts in _restartCancellations.Values)
        {
            try
            {
                cts.Cancel();
                cts.Dispose();
            }
            catch { }
        }
        _restartCancellations.Clear();

        foreach (var startLock in _startLocks.Values)
        {
            startLock.Dispose();
        }
        _startLocks.Clear();
    }
}
