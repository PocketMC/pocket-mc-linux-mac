using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using PocketMC.Core.Models;

namespace PocketMC.Infrastructure.Services;

/// <summary>
/// Maps PocketMC shared server properties to Pumpkin's native pumpkin.toml while preserving unknown settings.
/// </summary>
public static class PumpkinConfiguration
{
    public const string FileName = "pumpkin.toml";

    private static readonly (string Key, string Path, object Default)[] Settings =
    {
        ("motd", "networking.java.motd", "A Pumpkin Server"),
        ("max-players", "networking.java.max_players", 20L),
        ("online-mode", "networking.java.online_mode", true),
        ("view-distance", "networking.java.view_distance", 10L),
        ("simulation-distance", "networking.java.simulation_distance", 10L),
        ("gamemode", "default_gamemode", "survival"),
        ("difficulty", "default_difficulty", "normal"),
        ("level-name", "default_level_name", "world"),
        ("level-seed", "seed", ""),
        ("white-list", "white_list", false),
        ("pvp", "pvp.enabled", true),
        ("allow-nether", "allow_nether", true),
        ("spawn-protection", "spawn_protection", 16L),
        ("pumpkin-java-enabled", "networking.java.enabled", true),
        ("pumpkin-bedrock-enabled", "networking.bedrock.enabled", true),
        ("pumpkin-nethernet-enabled", "networking.bedrock.nethernet.enabled", true)
    };

    public static Dictionary<string, string> Load(string serverDir)
    {
        var table = ReadTable(serverDir);
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, path, fallback) in Settings)
        {
            string value = FormatScalar(Get(table, path) ?? fallback);
            properties[key] = key is "gamemode" or "difficulty" ? value.ToLowerInvariant() : value;
        }

        string javaAddress = FormatScalar(Get(table, "networking.java.address") ?? "0.0.0.0:25565");
        string bedrockAddress = FormatScalar(Get(table, "networking.bedrock.nethernet.address") ?? "0.0.0.0:19132");
        var java = ParseAddress(javaAddress, 25565);
        var bedrock = ParseAddress(bedrockAddress, 19132);

        properties["server-port"] = java.Port.ToString(CultureInfo.InvariantCulture);
        properties["server-ip"] = java.Address;
        properties["pumpkin-bedrock-port"] = bedrock.Port.ToString(CultureInfo.InvariantCulture);
        properties["pumpkin-bedrock-ip"] = bedrock.Address;
        return properties;
    }

    public static void Save(string serverDir, IReadOnlyDictionary<string, string> properties)
    {
        var table = ReadTable(serverDir);
        Apply(table, properties);
        Write(serverDir, table);
    }

    public static string ReadLevelName(string serverDir) => Load(serverDir)["level-name"];

    public static int ReadBedrockPort(string serverDir, int fallback = 19132) =>
        ParseAddress(ReadBedrockAddress(serverDir), fallback).Port;

    public static string ReadJavaAddress(string serverDir) =>
        FormatScalar(Get(ReadTable(serverDir), "networking.java.address") ?? "0.0.0.0:25565");

    public static string ReadBedrockAddress(string serverDir) =>
        FormatScalar(Get(ReadTable(serverDir), "networking.bedrock.nethernet.address") ?? "0.0.0.0:19132");

    public static bool IsJavaEnabled(string serverDir) =>
        Get(ReadTable(serverDir), "networking.java.enabled") is not false;

    public static bool IsBedrockEnabled(string serverDir)
    {
        var table = ReadTable(serverDir);
        return Get(table, "networking.bedrock.enabled") is not false &&
               Get(table, "networking.bedrock.nethernet.enabled") is not false;
    }

    public static void PrepareForLaunch(string serverDir)
    {
        var table = ReadTable(serverDir);
        Set(table, "commands.use_console", true);
        Set(table, "commands.use_tty", false);
        Set(table, "logging.color", false);

        if (File.Exists(Path.Combine(serverDir, "server-icon.png")))
        {
            Set(table, "use_favicon", true);
            Set(table, "favicon_path", "server-icon.png");
        }

        Write(serverDir, table);
    }

    public static string CreateSafeExport(string serverDir)
    {
        var table = NewTable();
        Apply(table, Load(serverDir));
        var source = ReadTable(serverDir);

        foreach (string path in new[]
        {
            "world.chunk.type", "world.chunk.compression.algorithm", "world.chunk.compression.level",
            "world.lighting", "world.autosave_ticks", "world.entity_islands.unaudited_reach",
            "world.entity_islands.knockback_reach", "allow_end", "hardcore", "force_gamemode",
            "networking.bedrock.motd", "networking.bedrock.max_players", "networking.bedrock.view_distance",
            "networking.bedrock.simulation_distance", "networking.bedrock.online_mode", "networking.bedrock.authentication.enabled",
            "use_favicon", "favicon_path"
        })
        {
            object? value = Get(source, path);
            if (value == null) continue;
            if (value is not (string or bool or long or double))
            {
                throw new FormatException($"Pumpkin setting '{path}' must be a scalar value.");
            }
            Set(table, path, value);
        }

        return SerializeToml(table);
    }

    private static Dictionary<string, object> ReadTable(string serverDir)
    {
        string path = Path.Combine(serverDir, FileName);
        var table = File.Exists(path)
            ? ParseToml(File.ReadAllText(path))
            : NewTable();
        ValidateTable(table);
        return table;
    }

    private static void Apply(Dictionary<string, object> table, IReadOnlyDictionary<string, string> properties)
    {
        foreach (var (key, path, fallback) in Settings)
        {
            if (!properties.TryGetValue(key, out string? value)) continue;

            object converted = fallback switch
            {
                bool => bool.TryParse(value, out bool parsedBool)
                    ? parsedBool
                    : throw new FormatException($"{key} must be true or false."),
                long => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsedLong) &&
                        parsedLong >= (key == "spawn-protection" ? 0 : 1)
                    ? parsedLong
                    : throw new FormatException($"{key} must be a positive integer."),
                _ => value
            };

            if (key is "view-distance" or "simulation-distance" && converted is long dist && (dist < 2 || dist > 64))
            {
                throw new FormatException($"{key} must be between 2 and 64.");
            }

            if (key is "gamemode" or "difficulty")
            {
                string[] allowed = key == "gamemode"
                    ? new[] { "survival", "creative", "adventure", "spectator" }
                    : new[] { "peaceful", "easy", "normal", "hard" };
                string lower = value.ToLowerInvariant();
                if (!allowed.Contains(lower))
                {
                    throw new FormatException($"Invalid {key}: {value}.");
                }
                converted = char.ToUpperInvariant(lower[0]) + lower[1..];
            }

            Set(table, path, converted);

            if (key is "motd" or "max-players" or "view-distance" or "simulation-distance" or "online-mode")
            {
                Set(table, path.Replace("networking.java.", "networking.bedrock.", StringComparison.Ordinal), converted);
            }
        }

        if (properties.TryGetValue("server-port", out string? port))
        {
            string ip = properties.TryGetValue("server-ip", out string? sIp) ? sIp : "0.0.0.0";
            Set(table, "networking.java.address", BuildAddress(ip, port, bedrock: false));
        }

        if (properties.TryGetValue("pumpkin-bedrock-port", out string? bedrockPort))
        {
            string ip = properties.TryGetValue("pumpkin-bedrock-ip", out string? bIp) ? bIp : "0.0.0.0";
            Set(table, "networking.bedrock.nethernet.address", BuildAddress(ip, bedrockPort, bedrock: true));
        }

        Set(table, "commands.use_console", true);
        Set(table, "commands.use_tty", false);
        Set(table, "logging.color", false);
        if (Get(table, "logging.file") == null)
        {
            Set(table, "logging.file", "latest.log");
        }
    }

    private static string BuildAddress(string address, string portText, bool bedrock)
    {
        int upper = bedrock ? 65534 : 65535;
        if (!int.TryParse(portText, out int port) || port < 1 || port > upper)
        {
            throw new FormatException($"{(bedrock ? "Pumpkin Bedrock" : "Java")} port must be between 1 and {upper}.");
        }

        if (string.IsNullOrWhiteSpace(address)) address = "0.0.0.0";
        if (!IPAddress.TryParse(address.Trim('[', ']'), out IPAddress? ip))
        {
            throw new FormatException("Pumpkin bind address must be an IPv4 or IPv6 address.");
        }

        return new IPEndPoint(ip, port).ToString();
    }

    private static (string Address, int Port) ParseAddress(string value, int fallback)
    {
        if (!IPEndPoint.TryParse(value, out IPEndPoint? endpoint))
        {
            throw new FormatException($"Invalid Pumpkin listener address '{value}'. Expected an IP address and port.");
        }

        if (endpoint.Port == 0)
        {
            throw new FormatException("Pumpkin listener ports must be between 1 and 65535.");
        }

        return (endpoint.Address.ToString(), endpoint.Port);
    }

    private static void ValidateTable(Dictionary<string, object> table)
    {
        foreach (var (key, path, fallback) in Settings)
        {
            object? value = Get(table, path);
            if (value == null) continue;

            if (fallback is bool && value is not bool ||
                fallback is long && value is not long ||
                fallback is string && value is not string)
            {
                throw new FormatException($"Pumpkin setting '{path}' has an invalid TOML value type.");
            }

            if (key is "view-distance" or "simulation-distance" && value is long dist && (dist < 2 || dist > 64))
            {
                throw new FormatException($"Pumpkin setting '{path}' must be between 2 and 64.");
            }
        }

        bool javaEnabled = Get(table, "networking.java.enabled") is not false;
        bool bedrockEnabled = Get(table, "networking.bedrock.enabled") is not false &&
                              Get(table, "networking.bedrock.nethernet.enabled") is not false;
        if (!javaEnabled && !bedrockEnabled)
        {
            throw new FormatException("Enable at least one Pumpkin client listener (Java or Bedrock).");
        }

        string javaAddr = Get(table, "networking.java.address") as string ?? "0.0.0.0:25565";
        string bedrockAddr = Get(table, "networking.bedrock.nethernet.address") as string ?? "0.0.0.0:19132";
        var javaEp = ParseAddress(javaAddr, 25565);
        var bedrockEp = ParseAddress(bedrockAddr, 19132);

        if (bedrockEp.Port >= 65535)
        {
            throw new FormatException("Pumpkin Bedrock port must leave room for the following UDP port.");
        }

        if (javaEnabled && bedrockEnabled && javaEp.Port == bedrockEp.Port)
        {
            throw new FormatException("Pumpkin Java and Bedrock listeners must use different ports.");
        }
    }

    private static Dictionary<string, object> NewTable() => new(StringComparer.Ordinal);

    private static object? Get(Dictionary<string, object> root, string path)
    {
        object? current = root;
        foreach (string part in path.Split('.'))
        {
            if (current is not Dictionary<string, object> table)
            {
                throw new FormatException($"Pumpkin setting '{path}' requires nested TOML tables.");
            }
            if (!table.TryGetValue(part, out current))
            {
                return null;
            }
        }
        return current;
    }

    private static void Set(Dictionary<string, object> root, string path, object value)
    {
        string[] parts = path.Split('.');
        var current = root;
        foreach (string part in parts[..^1])
        {
            if (!current.TryGetValue(part, out object? child))
            {
                child = NewTable();
                current[part] = child;
            }
            current = child as Dictionary<string, object>
                ?? throw new FormatException($"Pumpkin setting '{part}' must be a TOML table.");
        }
        current[parts[^1]] = value;
    }

    private static void Write(string serverDir, Dictionary<string, object> table)
    {
        ValidateTable(table);
        Directory.CreateDirectory(serverDir);
        string target = Path.Combine(serverDir, FileName);
        string tmp = target + ".tmp";
        File.WriteAllText(tmp, SerializeToml(table), new UTF8Encoding(false));
        File.Move(tmp, target, overwrite: true);
    }

    private static Dictionary<string, object> ParseToml(string content)
    {
        var root = NewTable();
        string currentSection = string.Empty;

        foreach (string rawLine in content.Split('\n'))
        {
            string line = StripTomlComment(rawLine).Trim();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']') && !line.StartsWith("[[", StringComparison.Ordinal))
            {
                currentSection = line[1..^1].Trim();
                EnsureTablePath(root, currentSection);
                continue;
            }

            int eq = line.IndexOf('=');
            if (eq <= 0) continue;

            string key = line[..eq].Trim().Trim('"');
            string valRaw = line[(eq + 1)..].Trim();
            object val = ParseTomlValue(valRaw);

            string fullPath = string.IsNullOrEmpty(currentSection) ? key : $"{currentSection}.{key}";
            Set(root, fullPath, val);
        }

        return root;
    }

    private static void EnsureTablePath(Dictionary<string, object> root, string sectionPath)
    {
        var current = root;
        foreach (string part in sectionPath.Split('.'))
        {
            string cleanPart = part.Trim().Trim('"');
            if (!current.TryGetValue(cleanPart, out object? child))
            {
                child = NewTable();
                current[cleanPart] = child;
            }
            current = child as Dictionary<string, object>
                ?? throw new FormatException($"Duplicate or conflicting TOML section '{sectionPath}'.");
        }
    }

    private static object ParseTomlValue(string raw)
    {
        if (string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase)) return false;

        if (raw.Length >= 2 && raw.StartsWith('"') && raw.EndsWith('"'))
        {
            return raw[1..^1].Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        if (raw.Length >= 2 && raw.StartsWith('\'') && raw.EndsWith('\''))
        {
            return raw[1..^1];
        }

        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
        {
            return l;
        }

        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
        {
            return d;
        }

        return raw;
    }

    private static string StripTomlComment(string line)
    {
        bool inDouble = false;
        bool inSingle = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"' && !inSingle && (i == 0 || line[i - 1] != '\\')) inDouble = !inDouble;
            else if (c == '\'' && !inDouble) inSingle = !inSingle;
            else if (c == '#' && !inDouble && !inSingle) return line[..i];
        }
        return line;
    }

    private static string SerializeToml(Dictionary<string, object> table)
    {
        var sb = new StringBuilder();
        WriteTable(sb, string.Empty, table);
        return sb.ToString();
    }

    private static void WriteTable(StringBuilder sb, string prefix, Dictionary<string, object> table)
    {
        bool hasScalars = table.Values.Any(v => v is not Dictionary<string, object>);
        if (!string.IsNullOrEmpty(prefix) && hasScalars)
        {
            sb.Append('[').Append(prefix).Append(']').AppendLine();
        }

        foreach (var pair in table)
        {
            if (pair.Value is not Dictionary<string, object>)
            {
                sb.Append(pair.Key).Append(" = ").Append(SerializeScalar(pair.Value)).AppendLine();
            }
        }

        if (!string.IsNullOrEmpty(prefix) && hasScalars)
        {
            sb.AppendLine();
        }

        foreach (var pair in table)
        {
            if (pair.Value is Dictionary<string, object> child)
            {
                string nextPrefix = string.IsNullOrEmpty(prefix) ? pair.Key : $"{prefix}.{pair.Key}";
                WriteTable(sb, nextPrefix, child);
            }
        }
    }

    private static string SerializeScalar(object value) => value switch
    {
        bool b => b ? "true" : "false",
        long l => l.ToString(CultureInfo.InvariantCulture),
        double d => d.ToString(CultureInfo.InvariantCulture),
        _ => "\"" + (value?.ToString() ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""
    };

    private static string FormatScalar(object value) =>
        value is bool b ? (b ? "true" : "false") : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
}
