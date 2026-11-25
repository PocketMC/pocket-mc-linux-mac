using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.Infrastructure.Services;

public sealed class SessionSummarizationService
{
    private const string SystemPrompt =
        "You are an expert Minecraft server administrator assistant.\n" +
        "Summarize the following server session logs concisely in Markdown.\n" +
        "Use clear headings including:\n" +
        "## Minecraft Server Session Summary\n" +
        "### Important Events\n" +
        "### Player Activity\n" +
        "### Crashes, Warnings, and Lag Spikes\n" +
        "### Recommendations\n" +
        "Be accurate, concise, and highlight errors or crash causes if present.";

    private readonly LlmProviderFactory _providerFactory;
    private readonly SummaryStorageService _storageService;
    private readonly ILogger<SessionSummarizationService> _logger;
    private readonly ConcurrentDictionary<string, Task<SummarizationResult>> _activeTasks = new(StringComparer.OrdinalIgnoreCase);

    public event Action<string>? SummarizationStarted;
    public event Action<string, SummarizationResult>? SummarizationCompleted;

    public SessionSummarizationService(
        LlmProviderFactory providerFactory,
        SummaryStorageService storageService,
        ILogger<SessionSummarizationService>? logger = null)
    {
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _logger = logger ?? NullLogger<SessionSummarizationService>.Instance;
    }

    public bool IsSummarizing(string serverDir)
        => !string.IsNullOrWhiteSpace(serverDir) && _activeTasks.ContainsKey(serverDir);

    public SessionSummary? GetLatestSummary(string serverDir)
    {
        if (string.IsNullOrWhiteSpace(serverDir))
        {
            return null;
        }

        return _storageService.ListSummaries(serverDir).FirstOrDefault();
    }

    public static string? GetSessionLogPath(string serverDir, bool preferCurrentSession = true)
    {
        if (string.IsNullOrWhiteSpace(serverDir))
        {
            return null;
        }

        string logsDir = Path.Combine(serverDir, "logs");
        if (!Directory.Exists(logsDir))
        {
            return null;
        }

        string[] preferredNames = preferCurrentSession
            ? new[] { "pocketmc-current-session.log", "pocketmc-latest.log", "pocketmc-last-session.log", "pocketmc-session.log", "latest.log" }
            : new[] { "pocketmc-last-session.log", "pocketmc-latest.log", "pocketmc-current-session.log", "pocketmc-session.log", "latest.log" };

        foreach (string name in preferredNames)
        {
            string candidate = Path.Combine(logsDir, name);
            if (File.Exists(candidate) && new FileInfo(candidate).Length > 0)
            {
                return candidate;
            }
        }

        string? latestRotated = Directory.GetFiles(logsDir, "pocketmc-*.log")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault(path => new FileInfo(path).Length > 0);

        return latestRotated;
    }

    public Task<SummarizationResult> SummarizeAsync(
        string serverDir,
        string serverName,
        Settings settings,
        DateTime sessionStart,
        DateTime sessionEnd,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverDir);
        ArgumentNullException.ThrowIfNull(settings);

        if (_activeTasks.TryGetValue(serverDir, out Task<SummarizationResult>? inFlight))
        {
            return inFlight;
        }

        SummarizationStarted?.Invoke(serverDir);
        Task<SummarizationResult> task = ExecuteSummarizeWithCompletionAsync(
            serverDir,
            serverName,
            settings,
            sessionStart,
            sessionEnd,
            cancellationToken);

        _activeTasks[serverDir] = task;
        return task;
    }

    private async Task<SummarizationResult> ExecuteSummarizeWithCompletionAsync(
        string serverDir,
        string serverName,
        Settings settings,
        DateTime sessionStart,
        DateTime sessionEnd,
        CancellationToken cancellationToken)
    {
        SummarizationResult result;
        try
        {
            result = await ExecuteSummarizeAsync(
                serverDir,
                serverName,
                settings,
                sessionStart,
                sessionEnd,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _activeTasks.TryRemove(serverDir, out _);
        }

        SummarizationCompleted?.Invoke(serverDir, result);
        return result;
    }

    private async Task<SummarizationResult> ExecuteSummarizeAsync(
        string serverDir,
        string serverName,
        Settings settings,
        DateTime sessionStart,
        DateTime sessionEnd,
        CancellationToken cancellationToken)
    {
        try
        {
            string? logPath = GetSessionLogPath(serverDir, preferCurrentSession: true);
            if (logPath == null || !File.Exists(logPath))
            {
                return SummarizationResult.Fail("No session log found. The server may not have generated any output.");
            }

            string? processedLog;
            using (var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                processedLog = SessionLogPreprocessor.Preprocess(fs);
            }

            if (string.IsNullOrWhiteSpace(processedLog))
            {
                return SummarizationResult.Fail("Session was too short to summarize (fewer than 5 meaningful events) or log is empty.");
            }

            List<string> chunks = SessionLogPreprocessor.ChunkLog(processedLog);
            string finalContent;

            if (chunks.Count == 1)
            {
                DiagnosticResult res = await _providerFactory.SendConfiguredRequestAsync(
                    settings,
                    SystemPrompt,
                    chunks[0],
                    cancellationToken).ConfigureAwait(false);
                if (!res.Success)
                {
                    return SummarizationResult.Fail($"AI API error: {res.ErrorMessage}");
                }

                finalContent = res.Content;
            }
            else
            {
                var partialSummaries = new StringBuilder();
                for (int i = 0; i < chunks.Count; i++)
                {
                    string chunkPrompt = $"This is part {i + 1} of {chunks.Count} of the server logs. Summarize this section:";
                    DiagnosticResult chunkRes = await _providerFactory.SendConfiguredRequestAsync(
                        settings,
                        chunkPrompt,
                        chunks[i],
                        cancellationToken).ConfigureAwait(false);
                    if (!chunkRes.Success)
                    {
                        return SummarizationResult.Fail($"AI API error on chunk {i + 1}: {chunkRes.ErrorMessage}");
                    }

                    partialSummaries.AppendLine($"--- Part {i + 1} ---");
                    partialSummaries.AppendLine(chunkRes.Content);
                    partialSummaries.AppendLine();
                }

                DiagnosticResult metaRes = await _providerFactory.SendConfiguredRequestAsync(
                    settings,
                    SystemPrompt + "\n\nCombine these partial summaries into one cohesive session summary:",
                    partialSummaries.ToString(),
                    cancellationToken).ConfigureAwait(false);
                if (!metaRes.Success)
                {
                    return SummarizationResult.Fail($"AI API error during final summary: {metaRes.ErrorMessage}");
                }

                finalContent = metaRes.Content;
            }

            finalContent = SummaryEmojiFormatter.Apply(finalContent);

            var summary = new SessionSummary
            {
                ServerName = serverName,
                SessionStart = sessionStart,
                SessionEnd = sessionEnd,
                Duration = sessionEnd - sessionStart,
                Content = finalContent,
                AiProvider = settings.AiProvider,
                GeneratedAt = DateTime.UtcNow
            };

            string savedPath = _storageService.Save(serverDir, summary);
            _logger.LogInformation("Session summary saved for {Server} at {Path}.", serverName, savedPath);
            return SummarizationResult.Ok(summary);
        }
        catch (OperationCanceledException)
        {
            return SummarizationResult.Fail("Summarization was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during summarization for {Server}.", serverName);
            return SummarizationResult.Fail($"Unexpected error: {ex.Message}");
        }
    }
}

public sealed class SummarizationResult
{
    public bool Success { get; init; }
    public SessionSummary? Summary { get; init; }
    public string? Error { get; init; }

    public static SummarizationResult Ok(SessionSummary summary) => new() { Success = true, Summary = summary };
    public static SummarizationResult Fail(string error) => new() { Success = false, Error = error };
}
