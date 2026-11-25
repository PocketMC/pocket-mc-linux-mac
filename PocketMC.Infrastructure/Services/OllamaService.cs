using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.Infrastructure.Services;

public sealed class OllamaService : IOllamaService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaService> _logger;

    public OllamaService(HttpClient httpClient, ILogger<OllamaService>? logger = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _logger = logger ?? NullLogger<OllamaService>.Instance;
    }

    public async Task<OllamaDaemonStatus> CheckDaemonHealthAsync(string endpoint, CancellationToken ct = default)
    {
        string baseUrl = NormalizeBaseUrl(endpoint);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/version");
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return OllamaDaemonStatus.Offline($"Ollama returned HTTP {(int)response.StatusCode}");
            }

            string? version = null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("version", out var vProp))
                {
                    version = vProp.GetString();
                }
            }
            catch (JsonException)
            {
                version = "unknown";
            }

            return OllamaDaemonStatus.Online(version ?? "unknown");
        }
        catch (HttpRequestException ex) when (IsConnectionRefused(ex))
        {
            return OllamaDaemonStatus.Offline("Ollama is not running. Start 'ollama serve' in terminal.");
        }
        catch (TaskCanceledException)
        {
            return OllamaDaemonStatus.Offline("Health check timed out.");
        }
        catch (Exception ex)
        {
            return OllamaDaemonStatus.Offline($"Connection error: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<OllamaModelInfo>> GetInstalledModelsAsync(string endpoint, string? apiKey = null, CancellationToken ct = default)
    {
        string baseUrl = NormalizeBaseUrl(endpoint);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/tags");
            AttachAuthIfNeeded(request, apiKey);

            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Ollama /api/tags returned HTTP {StatusCode}.", (int)response.StatusCode);
                return Array.Empty<OllamaModelInfo>();
            }

            return ParseModelsFromTagsResponse(body, isCloud: IsCloudEndpoint(baseUrl));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unable to list Ollama models from {Endpoint}.", baseUrl);
            return Array.Empty<OllamaModelInfo>();
        }
    }

    public async Task DeleteModelAsync(string endpoint, string modelName, string? apiKey = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            throw new ArgumentException("Model name cannot be empty.", nameof(modelName));
        }

        string baseUrl = NormalizeBaseUrl(endpoint);
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"{baseUrl}/api/delete")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { model = modelName }), Encoding.UTF8, "application/json")
        };
        AttachAuthIfNeeded(request, apiKey);

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException($"Failed to delete model (HTTP {(int)response.StatusCode}): {body}");
        }
    }

    public async Task PullModelAsync(
        string endpoint,
        string modelName,
        string? apiKey = null,
        IProgress<OllamaPullProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            throw new ArgumentException("Model name cannot be empty.", nameof(modelName));
        }

        string baseUrl = NormalizeBaseUrl(endpoint);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/pull")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { model = modelName, stream = true }), Encoding.UTF8, "application/json")
        };
        AttachAuthIfNeeded(request, apiKey);

        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                string errBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                progress?.Report(OllamaPullProgress.Error($"HTTP {(int)response.StatusCode}: {errBody}"));
                return;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var reader = new StreamReader(stream);

            while (true)
            {
                ct.ThrowIfCancellationRequested();
                string? line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (line is null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                OllamaPullProgress update = ParsePullProgressLine(line);
                progress?.Report(update);
                if (update.IsComplete) return;
            }

            progress?.Report(OllamaPullProgress.Success());
        }
        catch (OperationCanceledException)
        {
            progress?.Report(OllamaPullProgress.Error("Download cancelled."));
            throw;
        }
        catch (Exception ex)
        {
            progress?.Report(OllamaPullProgress.Error($"Pull failed: {ex.Message}"));
        }
    }

    public static string NormalizeBaseUrl(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return "http://localhost:11434";
        }

        string trimmed = endpoint.Trim().TrimEnd('/');
        if (!trimmed.Contains("://", StringComparison.Ordinal))
        {
            trimmed = trimmed.Contains("ollama.com", StringComparison.OrdinalIgnoreCase)
                ? "https://" + trimmed
                : "http://" + trimmed;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return "http://localhost:11434";
        }

        string basePath = uri.AbsolutePath.TrimEnd('/');
        if (basePath.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
            basePath.Equals("/api", StringComparison.OrdinalIgnoreCase))
        {
            basePath = string.Empty;
        }

        string port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
        return $"{uri.Scheme}://{uri.Host}{port}{basePath}";
    }

    private static bool IsCloudEndpoint(string baseUrl) =>
        baseUrl.Contains("ollama.com", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<OllamaModelInfo> ParseModelsFromTagsResponse(string json, bool isCloud)
    {
        var models = new List<OllamaModelInfo>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("models", out var array) || array.ValueKind != JsonValueKind.Array)
            {
                return models;
            }

            foreach (var item in array.EnumerateArray())
            {
                string name = item.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                string tag = item.TryGetProperty("model", out var m) ? m.GetString() ?? name : name;
                long size = item.TryGetProperty("size", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt64() : 0;

                string? paramSize = null;
                string? quant = null;
                string? family = null;
                if (item.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Object)
                {
                    paramSize = details.TryGetProperty("parameter_size", out var p) ? p.GetString() : null;
                    quant = details.TryGetProperty("quantization_level", out var q) ? q.GetString() : null;
                    family = details.TryGetProperty("family", out var f) ? f.GetString() : null;
                }

                models.Add(new OllamaModelInfo
                {
                    Name = name,
                    ModelTag = tag,
                    SizeBytes = size,
                    ParameterSize = paramSize,
                    QuantizationLevel = quant,
                    Family = family,
                    IsCloud = isCloud
                });
            }
        }
        catch (JsonException) { }
        return models;
    }

    private static OllamaPullProgress ParsePullProgressLine(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            string status = root.TryGetProperty("status", out var s) ? s.GetString() ?? string.Empty : string.Empty;
            string? digest = root.TryGetProperty("digest", out var d) ? d.GetString() : null;
            long? total = root.TryGetProperty("total", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetInt64() : null;
            long? completed = root.TryGetProperty("completed", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt64() : null;
            string? error = root.TryGetProperty("error", out var err) ? err.GetString() : null;
            bool isComplete = string.Equals(status, "success", StringComparison.OrdinalIgnoreCase) || error != null;

            return new OllamaPullProgress
            {
                Status = status,
                Digest = digest,
                TotalBytes = total,
                CompletedBytes = completed,
                IsComplete = isComplete,
                ErrorMessage = error
            };
        }
        catch (JsonException)
        {
            return new OllamaPullProgress { Status = line };
        }
    }

    private static void AttachAuthIfNeeded(HttpRequestMessage request, string? apiKey)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    private static bool IsConnectionRefused(HttpRequestException ex) =>
        ex.InnerException is SocketException s &&
        (s.SocketErrorCode == SocketError.ConnectionRefused ||
         s.SocketErrorCode == SocketError.HostUnreachable ||
         s.SocketErrorCode == SocketError.HostNotFound);
}
