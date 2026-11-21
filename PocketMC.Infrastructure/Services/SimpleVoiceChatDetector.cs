using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.Infrastructure.Services;

public class SimpleVoiceChatDetector : ISimpleVoiceChatDetector
{
    private readonly AddonManifestService _manifestService;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    private static readonly Regex VoiceChatStartedRegex = new(
        @"\[voicechat\]\s+Voice chat server started at port\s+(?<port>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    public SimpleVoiceChatDetector(AddonManifestService manifestService)
    {
        _manifestService = manifestService;
    }

    public SimpleVoiceChatDetection Detect(string? serverDir)
    {
        if (string.IsNullOrWhiteSpace(serverDir) || !Directory.Exists(serverDir))
        {
            return NotDetected();
        }

        SimpleVoiceChatDetectionSource source = SimpleVoiceChatDetectionSource.None;
        bool isDetected = false;

        // 1. Try manifest first
        var manifest = _manifestService.LoadManifest(serverDir);
        var entry = manifest.Entries.FirstOrDefault(e =>
            e.ProjectId == "9eGKb6K1" ||
            (!string.IsNullOrWhiteSpace(e.ProjectTitle) && e.ProjectTitle.Contains("Simple Voice Chat", StringComparison.OrdinalIgnoreCase)));

        if (entry != null)
        {
            isDetected = true;
            bool isPlugin = IsPluginServer(serverDir) ||
                            (entry.Loader != null && (entry.Loader.Contains("bukkit", StringComparison.OrdinalIgnoreCase) ||
                                                     entry.Loader.Contains("spigot", StringComparison.OrdinalIgnoreCase) ||
                                                     entry.Loader.Contains("paper", StringComparison.OrdinalIgnoreCase) ||
                                                     entry.Loader.Contains("purpur", StringComparison.OrdinalIgnoreCase) ||
                                                     entry.Loader.Contains("folia", StringComparison.OrdinalIgnoreCase)));
            source = isPlugin ? SimpleVoiceChatDetectionSource.PluginJar : SimpleVoiceChatDetectionSource.ModJar;
        }

        // Safety check: if detected as mod, but file is in plugins/, mark PluginJar
        if (isDetected && source == SimpleVoiceChatDetectionSource.ModJar)
        {
            if (TryFindVoiceChatJar(Path.Combine(serverDir, "plugins"), out _))
            {
                source = SimpleVoiceChatDetectionSource.PluginJar;
            }
        }

        // 2. Fallback to jar scan
        if (!isDetected)
        {
            bool hasModJar = TryFindVoiceChatJar(Path.Combine(serverDir, "mods"), out _);
            bool hasPluginJar = TryFindVoiceChatJar(Path.Combine(serverDir, "plugins"), out _);

            if (hasModJar || hasPluginJar)
            {
                isDetected = true;
                source = hasPluginJar ? SimpleVoiceChatDetectionSource.PluginJar : SimpleVoiceChatDetectionSource.ModJar;
            }
        }

        if (!isDetected)
        {
            return NotDetected();
        }

        if (SimpleVoiceChatConfigService.TryLoad(serverDir, out SimpleVoiceChatSettings settings))
        {
            return new SimpleVoiceChatDetection(
                true,
                source,
                settings.Port,
                settings.BindAddress,
                settings.VoiceHost,
                settings.ConfigPath,
                IsConfigPending: false);
        }

        if (TryFindVoiceChatLogPort(serverDir, out int logPort))
        {
            return new SimpleVoiceChatDetection(
                true,
                SimpleVoiceChatDetectionSource.Log,
                logPort,
                SimpleVoiceChatConfigService.DefaultBindAddress,
                VoiceHost: null,
                ConfigPath: null,
                IsConfigPending: true);
        }

        return Pending(source);
    }

    private static SimpleVoiceChatDetection Pending(SimpleVoiceChatDetectionSource source)
    {
        return new SimpleVoiceChatDetection(
            true,
            source,
            SimpleVoiceChatConfigService.DefaultPort,
            SimpleVoiceChatConfigService.DefaultBindAddress,
            VoiceHost: null,
            ConfigPath: null,
            IsConfigPending: true);
    }

    private static SimpleVoiceChatDetection NotDetected()
    {
        return new SimpleVoiceChatDetection(
            false,
            SimpleVoiceChatDetectionSource.None,
            SimpleVoiceChatConfigService.DefaultPort,
            SimpleVoiceChatConfigService.DefaultBindAddress,
            VoiceHost: null,
            ConfigPath: null,
            IsConfigPending: false);
    }

    private static bool TryFindVoiceChatJar(string directory, out string? path)
    {
        path = null;
        if (!Directory.Exists(directory))
        {
            return false;
        }

        foreach (string jarPath in Directory.EnumerateFiles(directory, "*.jar", SearchOption.TopDirectoryOnly))
        {
            string name = Path.GetFileName(jarPath);
            if (IsBaseSimpleVoiceChatJar(name))
            {
                path = jarPath;
                return true;
            }
        }

        return false;
    }

    private static bool IsBaseSimpleVoiceChatJar(string name)
    {
        return name.StartsWith("voicechat-", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("simplevoicechat-", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("simple-voice-chat-", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPluginServer(string serverDir)
    {
        return Directory.Exists(Path.Combine(serverDir, "plugins"));
    }

    private static bool TryFindVoiceChatLogPort(string serverDir, out int port)
    {
        port = 0;
        string logPath = Path.Combine(serverDir, "logs", "latest.log");
        if (!File.Exists(logPath))
        {
            return false;
        }

        try
        {
            using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var match = VoiceChatStartedRegex.Match(line);
                if (match.Success && int.TryParse(match.Groups["port"].Value, out int foundPort))
                {
                    port = foundPort;
                    return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }
}
