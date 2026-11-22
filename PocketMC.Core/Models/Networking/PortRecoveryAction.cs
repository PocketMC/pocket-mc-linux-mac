namespace PocketMC.Core.Models.Networking;

public enum PortRecoveryAction
{
    None = 0,
    RetrySamePort,
    WaitWithBackoff,
    SuggestNextFreePort,
    AutoSwitchToNextFreePort,
    Abort
}
