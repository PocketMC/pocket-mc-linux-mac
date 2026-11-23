using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;
using PocketMC.RemoteControl.Models;

namespace PocketMC.RemoteControl.Services;

public sealed class RemoteStatusService
{
    private readonly IInstanceService _instanceService;
    private readonly IProcessRunner _processRunner;
    private readonly IPlayerService _playerService;
    private readonly ProcessMetricsTracker _metricsTracker;
    private readonly LocalNetworkAddressService _localNetworkAddressService;
    private readonly IGeyserDetector? _geyserDetector;

    public RemoteStatusService(
        IInstanceService instanceService,
        IProcessRunner processRunner,
        IPlayerService playerService,
        ProcessMetricsTracker metricsTracker,
        LocalNetworkAddressService localNetworkAddressService,
        IGeyserDetector? geyserDetector = null)
    {
        _instanceService = instanceService;
        _processRunner = processRunner;
        _playerService = playerService;
        _metricsTracker = metricsTracker;
        _localNetworkAddressService = localNetworkAddressService;
        _geyserDetector = geyserDetector;
    }

    public async Task<IReadOnlyList<RemoteInstanceDto>> GetInstancesAsync()
    {
        var allInstances = await _instanceService.ListInstancesAsync();
        var dtos = new List<RemoteInstanceDto>();

        foreach (var inst in allInstances)
        {
            bool isRunning = _processRunner.TryGetRunningInfo(inst.Slug, out _, out string state);
            int playerCount = 0;
            if (isRunning)
            {
                try
                {
                    var onlinePlayers = await _playerService.GetOnlinePlayersAsync(inst);
                    playerCount = onlinePlayers?.Count ?? 0;
                }
                catch { }
            }

            dtos.Add(new RemoteInstanceDto
            {
                Id = inst.Id,
                Name = inst.Name,
                ServerType = inst.EngineType.ToString(),
                IsRunning = isRunning && state == "Running",
                State = state,
                MinecraftVersion = inst.EngineVersion,
                PlayerCount = playerCount,
                MaxPlayers = 20,
                IsPinned = false
            });
        }

        return dtos;
    }

    public async Task<RemoteInstanceStatusDto?> GetInstanceStatusAsync(Guid instanceId)
    {
        var allInstances = await _instanceService.ListInstancesAsync();
        var inst = allInstances.FirstOrDefault(i => i.Id == instanceId);
        if (inst == null) return null;

        bool isRunning = _processRunner.TryGetRunningInfo(inst.Slug, out int pgid, out string state);

        var onlinePlayerNames = new List<string>();
        var ops = new List<string>();
        var bans = new List<string>();

        if (isRunning)
        {
            try
            {
                onlinePlayerNames = (await _playerService.GetOnlinePlayersAsync(inst))?.ToList() ?? new();
                ops = (await _playerService.GetOpsAsync(inst))?.ToList() ?? new();
            }
            catch { }
        }

        double cpuUsage = 0;
        double ramUsageMb = 0;
        if (isRunning && pgid > 0)
        {
            try
            {
                var metrics = _metricsTracker.GetGroupMetrics(pgid);
                cpuUsage = Math.Round(metrics.CpuPercentage, 1);
                ramUsageMb = Math.Round(metrics.MemoryBytes / (1024.0 * 1024.0), 1);
            }
            catch { }
        }

        var onlinePlayers = onlinePlayerNames.Select(name => new RemotePlayerDto
        {
            Name = name,
            IsOp = ops.Contains(name, StringComparer.OrdinalIgnoreCase)
        }).ToList();

        var serverIps = new List<ServerIpDto>();
        int port = 25565;
        var localIps = _localNetworkAddressService.GetLocalIPv4Addresses();
        foreach (var ip in localIps)
        {
            serverIps.Add(new ServerIpDto { Label = "Local LAN", Address = $"{ip}:{port}" });
        }

        return new RemoteInstanceStatusDto
        {
            InstanceId = inst.Id,
            Name = inst.Name,
            ServerType = inst.EngineType.ToString(),
            State = state,
            MinecraftVersion = inst.EngineVersion,
            IsRunning = isRunning && state == "Running",
            UptimeSeconds = 0,
            PlayerCount = onlinePlayers.Count,
            MaxPlayers = 20,
            OnlinePlayers = onlinePlayers,
            OppedPlayers = ops,
            BannedPlayers = bans,
            CpuUsage = cpuUsage,
            RamUsageMb = ramUsageMb,
            MaxRamMb = 4096,
            IsPinned = false,
            ServerIps = serverIps
        };
    }
}
