using System;
using System.Text.RegularExpressions;

namespace PocketMC.App.Services.Console;

public static class ConsoleLogFilter
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    public static bool MatchesSearch(string line, string? query, bool useRegex = false)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        if (useRegex)
        {
            try
            {
                return Regex.IsMatch(
                    line,
                    query,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                    RegexTimeout);
            }
            catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
            {
                return false;
            }
        }

        string[] keywords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (string keyword in keywords)
        {
            if (keyword.StartsWith('-') && keyword.Length > 1)
            {
                if (line.Contains(keyword[1..], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            else if (!line.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
