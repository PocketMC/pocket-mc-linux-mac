using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PocketMC.Infrastructure.Services;

public static partial class SummaryEmojiFormatter
{
    private static readonly IReadOnlyDictionary<string, string> HeadingEmoji = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Minecraft Server Session Summary"] = "🎮",
        ["Minecraft Server Analysis Summary"] = "🎮",
        ["Important Events"] = "🔑",
        ["Key Events"] = "🔑",
        ["Player Activity"] = "👥",
        ["Crashes, Warnings, and Lag Spikes"] = "⚠️",
        ["Warnings & Issues"] = "⚠️",
        ["Warnings and Issues"] = "⚠️",
        ["Server Stability Issues"] = "📈",
        ["Performance Metrics"] = "📈",
        ["Plugin/mod issues"] = "🧩",
        ["Plugin Issues"] = "🧩",
        ["Mod Issues"] = "🧩",
        ["Configuration Problems"] = "⚙️",
        ["Recommendations"] = "✅",
        ["Recommendations for Improvement"] = "✅"
    };

    public static string Apply(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return markdown;
        }

        string[] lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = ApplyToLine(lines[i]);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string ApplyToLine(string line)
    {
        Match heading = MarkdownHeadingRegex().Match(line);
        if (heading.Success)
        {
            string body = heading.Groups["body"].Value.TrimEnd();
            return TryDecorate(body, out string decorated)
                ? $"{heading.Groups["indent"].Value}{heading.Groups["prefix"].Value}{decorated}"
                : line;
        }

        Match boldLine = BoldLineRegex().Match(line);
        if (boldLine.Success)
        {
            string body = boldLine.Groups["body"].Value.Trim();
            return TryDecorate(body, out string decorated)
                ? $"{boldLine.Groups["indent"].Value}**{decorated}**{boldLine.Groups["trailing"].Value}"
                : line;
        }

        string trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return line;
        }

        return TryDecorate(trimmed, out string plainDecorated)
            ? line.Replace(trimmed, plainDecorated, StringComparison.Ordinal)
            : line;
    }

    private static bool TryDecorate(string heading, out string decorated)
    {
        decorated = heading;

        foreach (string emoji in HeadingEmoji.Values)
        {
            if (heading.StartsWith(emoji, StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (!HeadingEmoji.TryGetValue(heading, out string? prefix))
        {
            return false;
        }

        decorated = $"{prefix} {heading}";
        return true;
    }

    [GeneratedRegex(@"^(?<indent>\s*)(?<prefix>#{1,6}\s+)(?<body>.+?)\s*$")]
    private static partial Regex MarkdownHeadingRegex();

    [GeneratedRegex(@"^(?<indent>\s*)\*\*(?<body>.+?)\*\*(?<trailing>\s*)$")]
    private static partial Regex BoldLineRegex();
}
