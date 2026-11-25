using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PocketMC.Infrastructure.Services;

/// <summary>
/// Preprocesses Minecraft server console logs before AI session summarization.
/// Strips repetitive startup noise, redacts IP addresses, deduplicates consecutive lines,
/// and splits very large logs into safe chunks.
/// </summary>
public static partial class SessionLogPreprocessor
{
    private const int MinMeaningfulLines = 5;
    private const int DefaultMaxChunkChars = 24000;

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    private static readonly Regex IpRegex = new(
        @"\b(?:\d{1,3}\.){3}\d{1,3}\b|(?:[0-9a-fA-F]{0,4}:){2,7}[0-9a-fA-F]{0,4}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex LeadingTimestampRegex = new(
        @"^\[(?:\d{2}:\d{2}:\d{2}|\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}(?:\.\d+)?)\]\s*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex[] NoisePatterns =
    {
        new(@"Preparing spawn area:\s*\d+%", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout),
        new(@"Preparing level\s+""", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout),
        new(@"Time elapsed:\s*\d+\s*ms", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout),
        new(@"Saving chunks for level", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout),
        new(@"Ambiguity between arguments", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout),
        new(@"Found \d+ advancement", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout),
        new(@"Loaded \d+ recipes", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout)
    };

    private static readonly Regex[] ImportantPatterns =
    {
        new(@"Starting minecraft server|Loading properties|Done \(|Server started|Stopping server|Closing Server", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout),
        new(@"joined the game|left the game|logged in|lost connection|Player connected:|Player disconnected:", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout),
        new(@"Exception|Error|FATAL|WARN|Crash|Can't keep up!|Timed out|StackOverflow|OutOfMemory", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout),
        new(@"banned|pardoned|kicked|opped|de-opped|whitelist", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout),
        new(@"INFO\]", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout)
    };

    public static string? Preprocess(string rawLog)
    {
        if (string.IsNullOrWhiteSpace(rawLog))
        {
            return null;
        }

        using var reader = new StringReader(rawLog);
        return Preprocess(reader);
    }

    public static string? Preprocess(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
        return Preprocess(reader);
    }

    private static string? Preprocess(TextReader reader)
    {
        var filtered = new List<string>();
        string? rawLine;

        while ((rawLine = reader.ReadLine()) != null)
        {
            string line = rawLine.TrimEnd('\r').Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            line = IpRegex.Replace(line, "[REDACTED_IP]");

            if (NoisePatterns.Any(p => p.IsMatch(line)))
            {
                continue;
            }

            if (ImportantPatterns.Any(p => p.IsMatch(line)) || !line.StartsWith("[", StringComparison.Ordinal))
            {
                filtered.Add(line);
            }
        }

        if (filtered.Count < MinMeaningfulLines)
        {
            return null;
        }

        List<string> deduplicated = DeduplicateLines(filtered);
        return string.Join(Environment.NewLine, deduplicated);
    }

    public static List<string> ChunkLog(string processedLog, int maxChunkChars = DefaultMaxChunkChars)
    {
        if (string.IsNullOrEmpty(processedLog))
        {
            return new List<string>();
        }

        int limit = Math.Max(512, maxChunkChars);
        if (processedLog.Length <= limit)
        {
            return new List<string> { processedLog };
        }

        var chunks = new List<string>();
        var current = new StringBuilder();
        string[] lines = processedLog.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        foreach (string line in lines)
        {
            if (current.Length > 0 && current.Length + line.Length + Environment.NewLine.Length > limit)
            {
                chunks.Add(current.ToString().TrimEnd());
                current.Clear();
            }

            if (line.Length > limit)
            {
                for (int offset = 0; offset < line.Length; offset += limit)
                {
                    int len = Math.Min(limit, line.Length - offset);
                    chunks.Add(line.Substring(offset, len));
                }
                continue;
            }

            current.AppendLine(line);
        }

        if (current.Length > 0)
        {
            chunks.Add(current.ToString().TrimEnd());
        }

        return chunks;
    }

    private static List<string> DeduplicateLines(IReadOnlyList<string> lines)
    {
        var output = new List<string>(lines.Count);
        string? prevNormalized = null;
        string? prevRaw = null;
        int repeatCount = 0;

        foreach (string line in lines)
        {
            string normalized = LeadingTimestampRegex.Replace(line, string.Empty).Trim();
            if (prevNormalized != null && string.Equals(normalized, prevNormalized, StringComparison.Ordinal))
            {
                repeatCount++;
                continue;
            }

            if (prevRaw != null)
            {
                output.Add(repeatCount > 1 ? $"{prevRaw} [x{repeatCount}]" : prevRaw);
            }

            prevNormalized = normalized;
            prevRaw = line;
            repeatCount = 1;
        }

        if (prevRaw != null)
        {
            output.Add(repeatCount > 1 ? $"{prevRaw} [x{repeatCount}]" : prevRaw);
        }

        return output;
    }
}
