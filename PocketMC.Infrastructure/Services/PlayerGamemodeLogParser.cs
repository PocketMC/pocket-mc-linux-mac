using System;
using System.Text.RegularExpressions;

namespace PocketMC.Infrastructure.Services;

public sealed class PlayerGamemodeChangeEvent
{
    public PlayerGamemodeChangeEvent(string playerName, string gamemode, string rawLine)
    {
        PlayerName = playerName;
        Gamemode = gamemode;
        RawLine = rawLine;
    }

    public string PlayerName { get; }
    public string Gamemode { get; }
    public string RawLine { get; }
}

public static class PlayerGamemodeLogParser
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    private static readonly Regex SetTargetGamemodeRegex = new(
        @"^(?:\[.*?\])?(?:\s*\[.*?\])*:?\s*(?:\[[^:\]]+:\s*)?Set\s+(?<player>.+?)'s\s+game\s*mode\s+to\s+(?<mode>Survival|Creative|Adventure|Spectator|\w+)(?:\s+Mode)?(?:\s*\])?\.?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        RegexTimeout);

    private static readonly Regex SetOwnGamemodeRegex = new(
        @"^(?:\[.*?\])?(?:\s*\[.*?\])*:?\s*(?:\[(?<player>[^:\]]+):\s*Set\s+own\s+game\s*mode\s+to\s+(?<mode>Survival|Creative|Adventure|Spectator|\w+)(?:\s+Mode)?\]|(?<player>.+?)\s+set\s+own\s+game\s*mode\s+to\s+(?<mode>Survival|Creative|Adventure|Spectator|\w+)(?:\s+Mode)?)\.?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        RegexTimeout);

    private static readonly Regex GamemodeUpdatedRegex = new(
        @"^(?:\[.*?\])?(?:\s*\[.*?\])*:?\s*(?:Player\s+)?(?<player>.+?)'s\s+game\s*mode\s+(?:has\s+been\s+updated\s+to|was\s+changed\s+to)\s+(?<mode>Survival|Creative|Adventure|Spectator|\w+)(?:\s+Mode)?\.?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        RegexTimeout);

    public static bool TryParse(string line, out PlayerGamemodeChangeEvent? changeEvent)
    {
        changeEvent = null;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var match = SetTargetGamemodeRegex.Match(line);
        if (match.Success)
        {
            changeEvent = new PlayerGamemodeChangeEvent(
                match.Groups["player"].Value.Trim(),
                NormalizeGamemode(match.Groups["mode"].Value),
                line);
            return true;
        }

        match = SetOwnGamemodeRegex.Match(line);
        if (match.Success)
        {
            changeEvent = new PlayerGamemodeChangeEvent(
                match.Groups["player"].Value.Trim(),
                NormalizeGamemode(match.Groups["mode"].Value),
                line);
            return true;
        }

        match = GamemodeUpdatedRegex.Match(line);
        if (match.Success)
        {
            changeEvent = new PlayerGamemodeChangeEvent(
                match.Groups["player"].Value.Trim(),
                NormalizeGamemode(match.Groups["mode"].Value),
                line);
            return true;
        }

        return false;
    }

    private static string NormalizeGamemode(string mode)
    {
        return mode.Trim().ToLowerInvariant() switch
        {
            "0" or "s" or "survival" => "survival",
            "1" or "c" or "creative" => "creative",
            "2" or "a" or "adventure" => "adventure",
            "3" or "sp" or "spectator" => "spectator",
            var other => other
        };
    }
}
