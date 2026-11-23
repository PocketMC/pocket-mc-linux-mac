using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PocketMC.RemoteControl.Models;

namespace PocketMC.RemoteControl.Services;

public static class ServerPropertiesHelper
{
    private const string PropertiesFileName = "server.properties";

    public static RemoteServerPropertiesDto Load(string serverDirectory)
    {
        string path = Path.Combine(serverDirectory, PropertiesFileName);
        var dto = new RemoteServerPropertiesDto();
        if (!File.Exists(path)) return dto;

        var dict = ReadPropertiesDict(path);

        if (dict.TryGetValue("motd", out string? motd)) dto.Motd = motd;
        if (dict.TryGetValue("gamemode", out string? gamemode)) dto.Gamemode = gamemode;
        if (dict.TryGetValue("difficulty", out string? diff)) dto.Difficulty = diff;
        if (dict.TryGetValue("max-players", out string? mp) && int.TryParse(mp, out int maxPlayers)) dto.MaxPlayers = maxPlayers;
        if (dict.TryGetValue("pvp", out string? pvp) && bool.TryParse(pvp, out bool isPvp)) dto.Pvp = isPvp;
        if (dict.TryGetValue("white-list", out string? wl) && bool.TryParse(wl, out bool isWl)) dto.Whitelist = isWl;
        if (dict.TryGetValue("allow-flight", out string? flight) && bool.TryParse(flight, out bool isFlight)) dto.AllowFlight = isFlight;
        if (dict.TryGetValue("enable-command-block", out string? cb) && bool.TryParse(cb, out bool isCb)) dto.AllowCommandBlock = isCb;
        if (dict.TryGetValue("allow-nether", out string? nether) && bool.TryParse(nether, out bool isNether)) dto.AllowNether = isNether;
        if (dict.TryGetValue("view-distance", out string? vd)) dto.ViewDistance = vd;
        if (dict.TryGetValue("level-seed", out string? seed)) dto.Seed = seed;

        return dto;
    }

    public static void Save(string serverDirectory, RemoteServerPropertiesDto dto)
    {
        string path = Path.Combine(serverDirectory, PropertiesFileName);
        var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();

        var updates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["motd"] = dto.Motd,
            ["gamemode"] = dto.Gamemode,
            ["difficulty"] = dto.Difficulty,
            ["max-players"] = dto.MaxPlayers.ToString(),
            ["pvp"] = dto.Pvp.ToString().ToLowerInvariant(),
            ["white-list"] = dto.Whitelist.ToString().ToLowerInvariant(),
            ["allow-flight"] = dto.AllowFlight.ToString().ToLowerInvariant(),
            ["enable-command-block"] = dto.AllowCommandBlock.ToString().ToLowerInvariant(),
            ["allow-nether"] = dto.AllowNether.ToString().ToLowerInvariant(),
            ["view-distance"] = dto.ViewDistance,
            ["level-seed"] = dto.Seed
        };

        var handledKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resultLines = new List<string>();

        foreach (var line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("#") || !trimmed.Contains('='))
            {
                resultLines.Add(line);
                continue;
            }

            int eqIdx = trimmed.IndexOf('=');
            string key = trimmed[..eqIdx].Trim();

            if (updates.TryGetValue(key, out string? updatedVal))
            {
                resultLines.Add($"{key}={updatedVal}");
                handledKeys.Add(key);
            }
            else
            {
                resultLines.Add(line);
            }
        }

        foreach (var kvp in updates)
        {
            if (!handledKeys.Contains(kvp.Key))
            {
                resultLines.Add($"{kvp.Key}={kvp.Value}");
            }
        }

        string tempPath = path + $".tmp.{Guid.NewGuid():N}";
        File.WriteAllLines(tempPath, resultLines, Encoding.UTF8);
        File.Move(tempPath, path, overwrite: true);
    }

    public static Dictionary<string, string> ReadProperties(string path) => ReadPropertiesDict(path);

    public static void UpdateProperty(string serverDirectory, string key, string value)
    {
        string path = Path.Combine(serverDirectory, PropertiesFileName);
        var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
        var resultLines = new List<string>();
        bool handled = false;

        foreach (var line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("#") || !trimmed.Contains('='))
            {
                resultLines.Add(line);
                continue;
            }

            int eqIdx = trimmed.IndexOf('=');
            string currentKey = trimmed[..eqIdx].Trim();

            if (string.Equals(currentKey, key, StringComparison.OrdinalIgnoreCase))
            {
                resultLines.Add($"{key}={value}");
                handled = true;
            }
            else
            {
                resultLines.Add(line);
            }
        }

        if (!handled)
        {
            resultLines.Add($"{key}={value}");
        }

        string tempPath = path + $".tmp.{Guid.NewGuid():N}";
        File.WriteAllLines(tempPath, resultLines, Encoding.UTF8);
        File.Move(tempPath, path, overwrite: true);
    }

    private static Dictionary<string, string> ReadPropertiesDict(string path)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadAllLines(path))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("#") || !trimmed.Contains('=')) continue;
            int eqIdx = trimmed.IndexOf('=');
            string key = trimmed[..eqIdx].Trim();
            string val = trimmed[(eqIdx + 1)..].Trim();
            dict[key] = val;
        }
        return dict;
    }
}
