namespace PocketMC.Core.Models;

public enum ServerState
{
    Stopped = 0,
    Starting,
    SettingUp,
    Installing,
    Running,
    Stopping,
    Crashed
}
