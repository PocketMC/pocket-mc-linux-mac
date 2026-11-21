using PocketMC.Core.Models;

namespace PocketMC.Core.Services;

public interface ISimpleVoiceChatDetector
{
    SimpleVoiceChatDetection Detect(string? serverDir);
}
