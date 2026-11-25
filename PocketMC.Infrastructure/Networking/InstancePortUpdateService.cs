using System;
using System.IO;
using System.Threading.Tasks;
using PocketMC.Core.Models;
using PocketMC.Core.Models.Networking;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;

namespace PocketMC.Infrastructure.Networking;

/// <summary>
/// Consolidates atomic port updates for Primary Server, Geyser Bedrock listener,
/// Pumpkin Bedrock listener, and Simple Voice Chat across config files and instance metadata.
/// </summary>
public sealed class InstancePortUpdateService
{
    private readonly IInstanceService _instanceService;
    private readonly GeyserProvisioningService? _geyserProvisioningService;

    public InstancePortUpdateService(
        IInstanceService instanceService,
        GeyserProvisioningService? geyserProvisioningService = null)
    {
        _instanceService = instanceService ?? throw new ArgumentNullException(nameof(instanceService));
        _geyserProvisioningService = geyserProvisioningService;
    }

    public async Task UpdatePortAsync(ServerInstance server, PortBindingRole role, int newPort)
    {
        ArgumentNullException.ThrowIfNull(server);

        if (role == PortBindingRole.PumpkinBedrock)
        {
            if (newPort < 1 || newPort > 65534)
            {
                throw new ArgumentOutOfRangeException(nameof(newPort), "Pumpkin Bedrock also uses the next UDP port.");
            }

            server.PumpkinBedrockPort = newPort;
            await _instanceService.SaveInstanceAsync(server).ConfigureAwait(false);
        }
        else if (role == PortBindingRole.GeyserBedrock)
        {
            if (newPort < 1 || newPort > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(newPort));
            }

            server.GeyserBedrockPort = newPort;
            if (!string.IsNullOrWhiteSpace(server.Path) && Directory.Exists(server.Path))
            {
                _geyserProvisioningService?.PatchGeyserConfigPort(server.Path, newPort);
            }
            await _instanceService.SaveInstanceAsync(server).ConfigureAwait(false);
        }
        else if (role == PortBindingRole.SimpleVoiceChat)
        {
            if (newPort < 1 || newPort > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(newPort));
            }

            server.SimpleVoiceChatPort = newPort;
            await _instanceService.SaveInstanceAsync(server).ConfigureAwait(false);
        }
        else
        {
            if (newPort < 1 || newPort > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(newPort));
            }

            UpdateServerPropertiesPort(server.Path, newPort);
            server.ServerPort = newPort;
            await _instanceService.SaveInstanceAsync(server).ConfigureAwait(false);
        }
    }

    public async Task UpdatePortFromMapTypeAsync(ServerInstance server, string portType, int newPort)
    {
        PortBindingRole role = portType switch
        {
            "Geyser" => PortBindingRole.GeyserBedrock,
            "Voice" => PortBindingRole.SimpleVoiceChat,
            "PumpkinBedrock" => PortBindingRole.PumpkinBedrock,
            _ => PortBindingRole.PrimaryServer
        };

        await UpdatePortAsync(server, role, newPort).ConfigureAwait(false);
    }

    private static void UpdateServerPropertiesPort(string instanceDir, int newPort)
    {
        if (string.IsNullOrWhiteSpace(instanceDir) || !Directory.Exists(instanceDir))
        {
            return;
        }

        string propsPath = Path.Combine(instanceDir, "server.properties");
        if (!File.Exists(propsPath))
        {
            File.WriteAllText(propsPath, $"server-port={newPort}\n");
            return;
        }

        string[] lines = File.ReadAllLines(propsPath);
        bool updated = false;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("server-port=", StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = $"server-port={newPort}";
                updated = true;
                break;
            }
        }

        if (!updated)
        {
            var list = new System.Collections.Generic.List<string>(lines) { $"server-port={newPort}" };
            lines = list.ToArray();
        }

        File.WriteAllLines(propsPath, lines);
    }
}
