using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PocketMC.Infrastructure.Services;

public sealed class PlayerListParseResult
{
    public PlayerListParseResult(
        int onlinePlayerCount,
        int? maxPlayers,
        IReadOnlyList<string> onlinePlayerNames,
        bool isComplete)
    {
        OnlinePlayerCount = onlinePlayerCount;
        MaxPlayers = maxPlayers;
        OnlinePlayerNames = onlinePlayerNames;
        IsComplete = isComplete;
    }

    public int OnlinePlayerCount { get; }
    public int? MaxPlayers { get; }
    public IReadOnlyList<string> OnlinePlayerNames { get; }
    public bool IsComplete { get; }
}

public static class PlayerListParser
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    private static readonly Regex JavaInlineRegex = new(
        @"There\s+are\s+(?<count>\d+)\s+of\s+a\s+max\s+of\s+(?<max>\d+)\s+players\s+online:\s*(?<players>.*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex BedrockInlineRegex = new(
        @"Players\s+connected\s+\((?<count>\d+)\s*/\s*(?<max>\d+)\):\s*(?<players>.*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex PocketMineInlineRegex = new(
        @"Online\s+players\s+\((?<count>\d+)\s*/\s*(?<max>\d+)\):\s*(?<players>.*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RegexTimeout);

    public static PlayerListParseResult? TryParse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var match = JavaInlineRegex.Match(line);
        if (match.Success)
        {
            int count = int.Parse(match.Groups["count"].Value);
            int max = int.Parse(match.Groups["max"].Value);
            string rawPlayers = match.Groups["players"].Value;
            var names = ParsePlayerNames(rawPlayers);
            return new PlayerListParseResult(count, max, names, names.Count == count);
        }

        match = BedrockInlineRegex.Match(line);
        if (match.Success)
        {
            int count = int.Parse(match.Groups["count"].Value);
            int max = int.Parse(match.Groups["max"].Value);
            string rawPlayers = match.Groups["players"].Value;
            var names = ParsePlayerNames(rawPlayers);
            return new PlayerListParseResult(count, max, names, names.Count == count);
        }

        match = PocketMineInlineRegex.Match(line);
        if (match.Success)
        {
            int count = int.Parse(match.Groups["count"].Value);
            int max = int.Parse(match.Groups["max"].Value);
            string rawPlayers = match.Groups["players"].Value;
            var names = ParsePlayerNames(rawPlayers);
            return new PlayerListParseResult(count, max, names, names.Count == count);
        }

        return null;
    }

    private static IReadOnlyList<string> ParsePlayerNames(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        var parts = raw.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var list = new List<string>();
        foreach (var p in parts)
        {
            string trimmed = p.Trim();
            if (!string.IsNullOrEmpty(trimmed) && !list.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(trimmed);
            }
        }
        return list;
    }
}
