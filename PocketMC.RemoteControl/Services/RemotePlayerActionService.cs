using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;
using PocketMC.RemoteControl.Models;

namespace PocketMC.RemoteControl.Services;

public sealed class RemotePlayerActionService
{
    private readonly IInstanceService _instanceService;
    private readonly IProcessRunner _processRunner;
    private readonly RemoteAuditLogService _auditLogService;

    public RemotePlayerActionService(
        IInstanceService instanceService,
        IProcessRunner processRunner,
        RemoteAuditLogService auditLogService)
    {
        _instanceService = instanceService;
        _processRunner = processRunner;
        _auditLogService = auditLogService;
    }

    public async Task<RemoteControlActionResult> ExecuteAsync(
        Guid instanceId,
        string playerName,
        string action,
        RemotePlayerActionRequest? request,
        string? deviceId)
    {
        var allInstances = await _instanceService.ListInstancesAsync();
        var instance = allInstances.FirstOrDefault(i => i.Id == instanceId);
        if (instance == null)
        {
            return RemoteControlActionResult.Failed(RemoteControlActionFailure.NotFound, "Instance was not found.");
        }

        string serverType = instance.EngineType.ToString();
        if (!CommandFormatter.TryFormatPlayerName(playerName, serverType, out string formattedName))
        {
            return RemoteControlActionResult.Failed(RemoteControlActionFailure.Failed, "Invalid player name.");
        }

        if (request?.Reason != null && (request.Reason.Length > 255 || request.Reason.Any(c => char.IsControl(c) || c == '\r' || c == '\n')))
        {
            return RemoteControlActionResult.Failed(RemoteControlActionFailure.Failed, "Invalid reason format.");
        }

        bool isRunning = _processRunner.TryGetRunningInfo(instance.Slug, out _, out string state) && state == "Running";
        if (!isRunning)
        {
            return RemoteControlActionResult.Failed(RemoteControlActionFailure.NotRunning, "Instance is not fully online.");
        }

        IReadOnlyList<string> commands = BuildCommands(action, formattedName, serverType, request?.Reason);
        if (commands.Count == 0)
        {
            return RemoteControlActionResult.Failed(RemoteControlActionFailure.Failed, "This action is not supported for this server type.");
        }

        try
        {
            foreach (string command in commands)
            {
                await _processRunner.SendCommandAsync(instance, command);
            }

            _auditLogService.Log(deviceId, $"player.{action}", instanceId, playerName);
            return RemoteControlActionResult.Successful();
        }
        catch (Exception ex)
        {
            _auditLogService.Log(deviceId, $"player.{action}", instanceId, playerName, success: false, message: ex.Message);
            return RemoteControlActionResult.Failed(RemoteControlActionFailure.Failed, ex.Message);
        }
    }

    private static IReadOnlyList<string> BuildCommands(string action, string formattedName, string? serverType, string? reason) =>
        action.ToLowerInvariant() switch
        {
            "kick" => PlayerActionCommandBuilder.BuildSubmitCommands("Kick", formattedName, serverType, reason),
            "ban" => PlayerActionCommandBuilder.BuildSubmitCommands("Ban", formattedName, serverType, reason),
            "pardon" => PlayerActionCommandBuilder.BuildPardonCommands(formattedName, serverType),
            "op" => new[] { $"op {formattedName}" },
            "deop" => new[] { $"deop {formattedName}" },
            _ => Array.Empty<string>()
        };
}
