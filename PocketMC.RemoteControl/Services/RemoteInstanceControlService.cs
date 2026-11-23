using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.RemoteControl.Models;

namespace PocketMC.RemoteControl.Services;

public sealed class RemoteInstanceControlService
{
    private readonly IInstanceService _instanceService;
    private readonly IProcessRunner _processRunner;

    public RemoteInstanceControlService(
        IInstanceService instanceService,
        IProcessRunner processRunner)
    {
        _instanceService = instanceService;
        _processRunner = processRunner;
    }

    public async Task<RemoteControlActionResult> StartAsync(Guid instanceId)
    {
        var allInstances = await _instanceService.ListInstancesAsync();
        var instance = allInstances.FirstOrDefault(i => i.Id == instanceId);
        if (instance == null)
        {
            return RemoteControlActionResult.Failed(RemoteControlActionFailure.NotFound, "Instance was not found.");
        }

        try
        {
            await _processRunner.StartAsync(instance);
            return RemoteControlActionResult.Successful();
        }
        catch (Exception ex)
        {
            return RemoteControlActionResult.Failed(RemoteControlActionFailure.Failed, ex.Message);
        }
    }

    public async Task<RemoteControlActionResult> StopAsync(Guid instanceId)
    {
        var allInstances = await _instanceService.ListInstancesAsync();
        var instance = allInstances.FirstOrDefault(i => i.Id == instanceId);
        if (instance == null)
        {
            return RemoteControlActionResult.Failed(RemoteControlActionFailure.NotFound, "Instance was not found.");
        }

        try
        {
            await _processRunner.StopAsync(instance);
            return RemoteControlActionResult.Successful();
        }
        catch (Exception ex)
        {
            return RemoteControlActionResult.Failed(RemoteControlActionFailure.Failed, ex.Message);
        }
    }

    public async Task<RemoteControlActionResult> RestartAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        var allInstances = await _instanceService.ListInstancesAsync();
        var instance = allInstances.FirstOrDefault(i => i.Id == instanceId);
        if (instance == null)
        {
            return RemoteControlActionResult.Failed(RemoteControlActionFailure.NotFound, "Instance was not found.");
        }

        try
        {
            await _processRunner.StopAsync(instance);
            await Task.Delay(1000, cancellationToken);
            await _processRunner.StartAsync(instance);
            return RemoteControlActionResult.Successful();
        }
        catch (Exception ex)
        {
            return RemoteControlActionResult.Failed(RemoteControlActionFailure.Failed, ex.Message);
        }
    }

    public async Task<IReadOnlyList<RemoteBatchItemResult>> ExecuteBatchAsync(
        IReadOnlyCollection<Guid> instanceIds,
        string action,
        Func<Guid, bool> hasAccess,
        Action<RemoteBatchItemResult>? audit = null,
        CancellationToken cancellationToken = default)
    {
        action = (action ?? string.Empty).Trim().ToLowerInvariant();
        if (action is not ("start" or "stop" or "restart")) throw new ArgumentException("Choose start, stop, or restart.");
        if (instanceIds.Count is < 1 or > 100) throw new ArgumentException("Select between 1 and 100 instances.");

        var allInstances = await _instanceService.ListInstancesAsync();
        var instanceMap = allInstances.ToDictionary(i => i.Id);

        var results = new List<RemoteBatchItemResult>();
        foreach (Guid id in instanceIds.Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            RemoteBatchItemResult item;

            if (!hasAccess(id))
            {
                item = new(id, "forbidden", "Instance access is not permitted.");
            }
            else if (!instanceMap.TryGetValue(id, out var instance))
            {
                item = new(id, "notFound", "Instance was not found.");
            }
            else
            {
                bool running = _processRunner.TryGetRunningInfo(instance.Slug, out _, out string state) && state == "Running";
                bool skip = action == "start" ? running : action == "restart" ? !running : !running;

                if (skip)
                {
                    item = new(id, "skipped", "The instance is already in the requested state.");
                }
                else
                {
                    var result = action switch
                    {
                        "start" => await StartAsync(id),
                        "stop" => await StopAsync(id),
                        _ => await RestartAsync(id, cancellationToken)
                    };
                    item = new(id, result.Success ? "success" : "failed", result.Message);
                }
            }

            results.Add(item);
            audit?.Invoke(item);
        }

        return results;
    }
}
