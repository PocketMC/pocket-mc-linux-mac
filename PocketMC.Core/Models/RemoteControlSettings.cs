namespace PocketMC.Core.Models;

public class RemoteControlSettings
{
    public bool Enabled { get; set; } = false;
    public int Port { get; set; } = 49152;
    public RemoteAccessMode AccessMode { get; set; } = RemoteAccessMode.LanOnly;
    public bool AllowRemoteConsoleCommands { get; set; } = false;
    public bool AllowRemotePlayerActions { get; set; } = false;
    public string? PasswordHash { get; set; }
    public bool RequireAuthentication { get; set; } = true;
    public string? SecurityStamp { get; set; }
    public string? PlayitTunnelId { get; set; }
    public string? TunnelProviderId { get; set; }
}
