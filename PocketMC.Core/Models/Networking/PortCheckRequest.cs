using System;

namespace PocketMC.Core.Models.Networking;

public sealed class PortCheckRequest
{
    public PortCheckRequest(
        int port,
        PortProtocol protocol = PortProtocol.Tcp,
        PortIpMode ipMode = PortIpMode.IPv4,
        string? bindAddress = null,
        Guid? instanceId = null,
        string? instanceName = null,
        string? instancePath = null,
        bool checkTunnelAvailability = false,
        bool checkPublicReachability = false,
        PortBindingRole bindingRole = PortBindingRole.PrimaryServer,
        PortEngine engine = PortEngine.Unknown,
        string? displayName = null)
    {
        Port = port;
        Protocol = protocol;
        IpMode = ipMode;
        BindAddress = bindAddress;
        InstanceId = instanceId;
        InstanceName = instanceName;
        InstancePath = instancePath;
        CheckTunnelAvailability = checkTunnelAvailability;
        CheckPublicReachability = checkPublicReachability;
        BindingRole = bindingRole;
        Engine = engine;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? BuildDefaultDisplayName(bindingRole, engine) : displayName.Trim();
    }

    public int Port { get; }
    public PortProtocol Protocol { get; }
    public PortIpMode IpMode { get; }
    public string? BindAddress { get; }
    public Guid? InstanceId { get; }
    public string? InstanceName { get; }
    public string? InstancePath { get; }
    public bool CheckTunnelAvailability { get; }
    public bool CheckPublicReachability { get; }
    public PortBindingRole BindingRole { get; }
    public PortEngine Engine { get; }
    public string DisplayName { get; }

    public static string BuildDefaultDisplayName(PortBindingRole bindingRole, PortEngine engine)
    {
        return bindingRole switch
        {
            PortBindingRole.PrimaryServer => engine switch
            {
                PortEngine.BedrockDedicated => "Bedrock Server",
                PortEngine.PocketMine => "PocketMine-MP Server",
                PortEngine.PumpkinBedrock => "Pumpkin Bedrock Server",
                _ => "Primary Minecraft Server"
            },
            PortBindingRole.JavaServer => "Java Edition Server",
            PortBindingRole.BedrockServer => "Bedrock Dedicated Server",
            PortBindingRole.PocketMineServer => "PocketMine-MP Server",
            PortBindingRole.GeyserBedrock => "Geyser Bedrock Listener",
            PortBindingRole.SimpleVoiceChat => "Simple Voice Chat",
            PortBindingRole.PumpkinBedrock => "Pumpkin NetherNet Listener",
            _ => "Server Port"
        };
    }
}
