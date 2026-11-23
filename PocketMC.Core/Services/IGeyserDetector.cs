namespace PocketMC.Core.Services;

public interface IGeyserDetector
{
    bool IsGeyserInstalled(string? instancePath);
}
