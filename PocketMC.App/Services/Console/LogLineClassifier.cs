using System;
using System.Text.RegularExpressions;

namespace PocketMC.App.Services.Console;

public static class LogLineClassifier
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    private static readonly Regex ChatOrPlayerEventRegex = new(
        @"^\[.*?\](?:\s*\[.*?\])*:\s*(?:<[^>]+>\s.*|[^\s:]+\s(?:joined|left) the game)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex LogPrefixRegex = new(
        @"^\[.*?\](?:\s*\[.*?\])*:\s*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    public static ConsoleLogLevel Classify(string? text, ConsoleLogLevel previousLevel = ConsoleLogLevel.Info)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ConsoleLogLevel.Info;
        }

        string trimmed = text.TrimStart();
        if (trimmed.StartsWith("at ", StringComparison.Ordinal) ||
            trimmed.StartsWith("...", StringComparison.Ordinal) ||
            text.Contains("Caused by:", StringComparison.Ordinal))
        {
            return previousLevel;
        }

        if (text.StartsWith("> ", StringComparison.Ordinal))
        {
            return ConsoleLogLevel.System;
        }

        if (IsChatOrPlayerEvent(text))
        {
            return ConsoleLogLevel.Chat;
        }

        string searchTarget = text.ToUpperInvariant();
        try
        {
            Match prefixMatch = LogPrefixRegex.Match(text);
            if (prefixMatch.Success)
            {
                searchTarget = prefixMatch.Value.ToUpperInvariant();
            }
        }
        catch (RegexMatchTimeoutException)
        {
        }

        if (ContainsAny(searchTarget, "ERROR", "FATAL", "SEVERE", "CRITICAL", "EXCEPTION"))
        {
            return ConsoleLogLevel.Error;
        }

        if (ContainsAny(searchTarget, "WARN", "****", "***"))
        {
            return ConsoleLogLevel.Warn;
        }

        if (ContainsAny(searchTarget, "DEBUG"))
        {
            return ConsoleLogLevel.Debug;
        }

        if (ContainsAny(searchTarget, "TRACE"))
        {
            return ConsoleLogLevel.Trace;
        }

        return ConsoleLogLevel.Info;
    }

    public static string GetHexColor(ConsoleLogLevel level)
    {
        return level switch
        {
            ConsoleLogLevel.Error => "#EF4444",   // Red
            ConsoleLogLevel.Warn => "#F59E0B",    // Yellow/Amber
            ConsoleLogLevel.Chat => "#10B981",    // Green
            ConsoleLogLevel.System => "#8B5CF6",  // Purple/Indigo
            ConsoleLogLevel.Debug => "#60A5FA",   // Blue
            ConsoleLogLevel.Trace => "#9CA3AF",   // Dim Gray
            _ => "#E4E4E7"                         // Off-white default
        };
    }

    private static bool IsChatOrPlayerEvent(string text)
    {
        try
        {
            return ChatOrPlayerEventRegex.IsMatch(text);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        foreach (string needle in needles)
        {
            if (text.Contains(needle, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }
}
