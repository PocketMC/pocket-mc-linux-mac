using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PocketMC.Core.Models;

namespace PocketMC.Infrastructure.Services;

public record ServerCrashContext(
    string WorkingDirectory,
    string ServerType,
    ServerState StateBeforeExit,
    bool IntentionalStop,
    int ExitCode,
    DateTime? ProcessStartTime,
    IReadOnlyList<string> OutputBufferLines);

public static class ServerCrashDetector
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(500);

    public static CrashAnalysisResult Analyze(ServerCrashContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.IntentionalStop)
        {
            return CrashAnalysisResult.CleanExit(context.ExitCode);
        }

        var lines = context.OutputBufferLines ?? Array.Empty<string>();

        // Layer 1: Disk Crash Reports
        var diskReport = CheckDiskCrashReports(context.WorkingDirectory, context.ProcessStartTime);
        if (diskReport != null)
        {
            diskReport.ExitCode = context.ExitCode;
            return diskReport;
        }

        // Layer 2: Modloader & Engine Error Pattern Scanner
        var logCrash = AnalyzeLogSignatures(lines, context.ExitCode);
        if (logCrash != null)
        {
            return logCrash;
        }

        // Layer 3: Premature Startup Exit
        if (context.StateBeforeExit is ServerState.Starting)
        {
            return BuildStartupFailureResult(lines, context.ExitCode);
        }

        // Layer 4: Non-zero exit code
        if (context.ExitCode != 0)
        {
            return BuildExitCodeFailureResult(lines, context.ExitCode);
        }

        return CrashAnalysisResult.CleanExit(context.ExitCode);
    }

    private static CrashAnalysisResult? CheckDiskCrashReports(string workingDir, DateTime? processStartTime)
    {
        if (string.IsNullOrWhiteSpace(workingDir) || !Directory.Exists(workingDir))
            return null;

        try
        {
            string crashReportsDir = Path.Combine(workingDir, "crash-reports");
            if (Directory.Exists(crashReportsDir))
            {
                var minTime = (processStartTime ?? DateTime.UtcNow.AddMinutes(-5)).AddSeconds(-15);
                var latestReport = Directory.GetFiles(crashReportsDir, "crash-*.txt")
                    .Select(f => new FileInfo(f))
                    .Where(fi => fi.LastWriteTimeUtc >= minTime)
                    .OrderByDescending(fi => fi.LastWriteTimeUtc)
                    .FirstOrDefault();

                if (latestReport != null)
                {
                    return ParseJavaCrashReport(latestReport.FullName);
                }
            }
        }
        catch { }

        return null;
    }

    private static CrashAnalysisResult ParseJavaCrashReport(string filePath)
    {
        try
        {
            string content = File.ReadAllText(filePath);
            var result = new CrashAnalysisResult
            {
                IsCrash = true,
                CrashReportPath = filePath,
                FullLogContext = content.Length > 8000 ? content[^8000..] : content
            };

            var match = Regex.Match(content, @"Description:\s*(.+)", RegexOptions.IgnoreCase, RegexTimeout);
            result.Title = match.Success ? match.Groups[1].Value.Trim() : "Server Crash Report Generated";

            if (content.Contains("OutOfMemoryError", StringComparison.OrdinalIgnoreCase))
            {
                result.Category = CrashCategory.OutOfMemory;
                result.Summary = "The server ran out of allocated memory heap space.";
            }
            else if (content.Contains("Mixin", StringComparison.OrdinalIgnoreCase))
            {
                result.Category = CrashCategory.MixinConflict;
                result.Summary = "A mod mixin transformation failed during server bootstrap.";
            }
            else
            {
                result.Category = CrashCategory.ServerTickException;
                result.Summary = result.Title;
            }

            return result;
        }
        catch (Exception ex)
        {
            return new CrashAnalysisResult
            {
                IsCrash = true,
                Category = CrashCategory.Unknown,
                Title = "Crash Report Detected",
                Summary = $"Report file: {Path.GetFileName(filePath)} ({ex.Message})",
                CrashReportPath = filePath
            };
        }
    }

    private static CrashAnalysisResult? AnalyzeLogSignatures(IReadOnlyList<string> lines, int exitCode)
    {
        if (lines.Count == 0) return null;

        var fullContext = string.Join(Environment.NewLine, lines.TakeLast(50));

        foreach (var line in lines.TakeLast(100).Reverse())
        {
            if (line.Contains("OutOfMemoryError", StringComparison.OrdinalIgnoreCase))
            {
                return new CrashAnalysisResult
                {
                    IsCrash = true,
                    Category = CrashCategory.OutOfMemory,
                    Title = "Out of Memory",
                    Summary = "Java heap space exhausted. Increase maximum RAM in Server Settings.",
                    FullLogContext = fullContext,
                    ExitCode = exitCode
                };
            }

            if (line.Contains("Missing or unsupported mandatory dependencies", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Requires: ", StringComparison.OrdinalIgnoreCase))
            {
                return new CrashAnalysisResult
                {
                    IsCrash = true,
                    Category = CrashCategory.MissingMod,
                    Title = "Missing Mod Dependencies",
                    Summary = line,
                    FullLogContext = fullContext,
                    ExitCode = exitCode
                };
            }

            if (line.Contains("MixinApplyError", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Mixin transformation failed", StringComparison.OrdinalIgnoreCase))
            {
                return new CrashAnalysisResult
                {
                    IsCrash = true,
                    Category = CrashCategory.MixinConflict,
                    Title = "Mod Mixin Conflict",
                    Summary = line,
                    FullLogContext = fullContext,
                    ExitCode = exitCode
                };
            }
        }

        return null;
    }

    private static CrashAnalysisResult BuildStartupFailureResult(IReadOnlyList<string> lines, int exitCode)
    {
        string lastLine = lines.Count > 0 ? lines[^1] : "Server terminated before reaching online state.";
        return new CrashAnalysisResult
        {
            IsCrash = true,
            Category = CrashCategory.StartupAborted,
            Title = "Server Startup Aborted",
            Summary = lastLine,
            FullLogContext = string.Join(Environment.NewLine, lines.TakeLast(50)),
            ExitCode = exitCode
        };
    }

    private static CrashAnalysisResult BuildExitCodeFailureResult(IReadOnlyList<string> lines, int exitCode)
    {
        return new CrashAnalysisResult
        {
            IsCrash = true,
            Category = CrashCategory.ProcessExitError,
            Title = $"Process Terminated with Exit Code {exitCode}",
            Summary = lines.Count > 0 ? lines[^1] : $"Process exited unexpectedly with code {exitCode}.",
            FullLogContext = string.Join(Environment.NewLine, lines.TakeLast(50)),
            ExitCode = exitCode
        };
    }
}
