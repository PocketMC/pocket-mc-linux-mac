namespace PocketMC.Core.Models;

public sealed record SimpleVoiceChatSettings(
    string ConfigPath,
    int Port,
    string BindAddress,
    string? VoiceHost);

public enum SimpleVoiceChatDetectionSource
{
    None = 0,
    ConfigFile,
    ModJar,
    PluginJar,
    Log
}

public sealed record SimpleVoiceChatDetection(
    bool IsDetected,
    SimpleVoiceChatDetectionSource Source,
    int Port,
    string BindAddress,
    string? VoiceHost,
    string? ConfigPath,
    bool IsConfigPending);
