using System;
using System.Collections.Generic;
using System.Linq;

namespace PocketMC.Core.Models;

public enum OllamaMode
{
    Local,
    Cloud
}

public class OllamaModelInfo
{
    public string Name { get; init; } = string.Empty;
    public string ModelTag { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public string? ParameterSize { get; init; }
    public string? QuantizationLevel { get; init; }
    public string? Family { get; init; }
    public bool IsCloud { get; init; }
    public IReadOnlyList<string> Capabilities { get; init; } = Array.Empty<string>();

    public string FormattedSize
    {
        get
        {
            if (SizeBytes <= 0) return "Unknown";
            const double gb = 1024.0 * 1024.0 * 1024.0;
            const double mb = 1024.0 * 1024.0;
            return SizeBytes >= gb ? $"{SizeBytes / gb:F2} GB" : $"{SizeBytes / mb:F1} MB";
        }
    }

    public string DetailsSummary =>
        string.Join(" · ", new[] { ParameterSize, QuantizationLevel, FormattedSize }.Where(x => !string.IsNullOrWhiteSpace(x)));

    public override string ToString() => Name;
}

public class OllamaPullProgress
{
    public string Status { get; init; } = string.Empty;
    public string? Digest { get; init; }
    public long? TotalBytes { get; init; }
    public long? CompletedBytes { get; init; }
    public bool IsComplete { get; init; }
    public string? ErrorMessage { get; init; }

    public double Percent =>
        TotalBytes is > 0 && CompletedBytes.HasValue
            ? Math.Clamp((double)CompletedBytes.Value / TotalBytes.Value * 100.0, 0.0, 100.0)
            : 0.0;

    public static OllamaPullProgress Success() => new() { Status = "success", IsComplete = true };
    public static OllamaPullProgress Error(string message) => new() { Status = "error", IsComplete = true, ErrorMessage = message };
}

public class OllamaDaemonStatus
{
    public bool IsRunning { get; init; }
    public string? Version { get; init; }
    public string? ErrorMessage { get; init; }

    public static OllamaDaemonStatus Online(string version) => new() { IsRunning = true, Version = version };
    public static OllamaDaemonStatus Offline(string error) => new() { IsRunning = false, ErrorMessage = error };
}
