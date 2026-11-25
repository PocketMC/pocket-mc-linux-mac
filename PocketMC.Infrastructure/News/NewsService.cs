using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PocketMC.Core.Services;

namespace PocketMC.Infrastructure.News
{
    public sealed record NewsSyncResult(
        bool Succeeded,
        int DownloadedCount,
        int InvalidCount,
        string? ErrorMessage = null);

    public sealed class NewsService : IDisposable
    {
        private const string RepositoryContentsUrl = "https://api.github.com/repos/PocketMC/pocket-mc-windows/contents/news?ref=master";
        private readonly ISettingsService _settingsService;
        private readonly HttpClient _httpClient;
        private readonly NewsTextParser _parser;
        private readonly ILogger<NewsService>? _logger;
        private readonly string _cacheDirectory;
        private readonly SemaphoreSlim _syncGate = new(1, 1);
        private readonly object _cacheLock = new();

        public event Action? NewsStateChanged;

        public NewsService(
            ISettingsService settingsService,
            HttpClient httpClient,
            NewsTextParser? parser = null,
            ILogger<NewsService>? logger = null)
        {
            _settingsService = settingsService;
            _httpClient = httpClient;
            _parser = parser ?? new NewsTextParser();
            _logger = logger;

            string root = Path.Combine(_settingsService.GetDataRoot(), "news");
            _cacheDirectory = Path.Combine(root, "cache");
            Directory.CreateDirectory(_cacheDirectory);
        }

        public string CacheDirectory => _cacheDirectory;

        public IReadOnlyList<NewsItem> GetCachedNews()
        {
            if (!Directory.Exists(_cacheDirectory)) return Array.Empty<NewsItem>();

            List<NewsItem> items = new();
            foreach (string path in Directory.EnumerateFiles(_cacheDirectory, "*.txt", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    string content = File.ReadAllText(path, Encoding.UTF8);
                    NewsItem item = _parser.ParseCached(Path.GetFileName(path), content);
                    items.Add(item);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to read cached news file {Path}", path);
                }
            }

            return items.OrderByDescending(item => item.Metadata.PublishedUtc).ToList();
        }

        public int GetUnreadNewsCount()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return GetCachedNews().Count(item =>
                !item.Metadata.IsRead &&
                (item.Metadata.ExpiresUtc == null || item.Metadata.ExpiresUtc > now));
        }

        public IReadOnlyList<NewsItem> GetPendingPopupNews(string? currentVersionText = null)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            Version? currentVersion = ParseVersion(currentVersionText ?? Configuration.AppConfig.AppVersion);

            return GetCachedNews()
                .Where(item =>
                    item.Metadata.Popup &&
                    !item.Metadata.IsRead &&
                    (item.Metadata.ExpiresUtc == null || item.Metadata.ExpiresUtc > now) &&
                    (currentVersion == null || IsForVersion(item.Metadata, currentVersion)))
                .OrderBy(item => item.Metadata.PublishedUtc)
                .ThenBy(item => item.Metadata.Id, StringComparer.Ordinal)
                .ToList();
        }

        private static bool IsForVersion(NewsMetadata metadata, Version currentVersion)
            => (metadata.MinimumVersion == null || currentVersion >= metadata.MinimumVersion) &&
               (metadata.MaximumVersion == null || currentVersion <= metadata.MaximumVersion);

        private static Version? ParseVersion(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string normalized = System.Text.RegularExpressions.Regex.Replace(
                value.Trim(),
                @"[-+].*$",
                string.Empty,
                System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1));
            string[] parts = normalized.Split('.');
            if (parts.Length == 2) normalized += ".0";
            return Version.TryParse(normalized, out Version? version) ? version : null;
        }

        public void MarkRead(string newsId)
        {
            if (string.IsNullOrWhiteSpace(newsId) || !Directory.Exists(_cacheDirectory)) return;

            lock (_cacheLock)
            {
                foreach (string path in Directory.GetFiles(_cacheDirectory, "*.txt", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        string source = File.ReadAllText(path, Encoding.UTF8);
                        NewsItem item = _parser.ParseCached(Path.GetFileName(path), source);
                        if (string.Equals(item.Metadata.Id, newsId, StringComparison.Ordinal) && !item.Metadata.IsRead)
                        {
                            string updatedSource = _parser.MarkRead(item.FileName, source);
                            string tempFile = path + ".tmp";
                            File.WriteAllText(tempFile, updatedSource, Encoding.UTF8);
                            File.Move(tempFile, path, overwrite: true);
                            NewsStateChanged?.Invoke();
                            return;
                        }
                    }
                    catch { }
                }
            }
        }

        public async Task<NewsSyncResult> SyncAsync(CancellationToken cancellationToken = default)
        {
            if (!await _syncGate.WaitAsync(0, cancellationToken))
            {
                return new NewsSyncResult(false, 0, 0, "Sync already in progress.");
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, RepositoryContentsUrl);
                request.Headers.UserAgent.ParseAdd("PocketMC-CrossPlatform");

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return new NewsSyncResult(false, 0, 0, $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return new NewsSyncResult(false, 0, 0, "Invalid GitHub directory payload.");
                }

                int downloaded = 0;
                int invalid = 0;

                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    string? name = element.TryGetProperty("name", out var n) ? n.GetString() : null;
                    string? downloadUrl = element.TryGetProperty("download_url", out var d) ? d.GetString() : null;

                    if (string.IsNullOrWhiteSpace(name) || !name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(downloadUrl))
                    {
                        continue;
                    }

                    string targetPath = Path.Combine(_cacheDirectory, name);
                    if (File.Exists(targetPath))
                    {
                        continue; // Already downloaded
                    }

                    try
                    {
                        using var fileReq = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
                        fileReq.Headers.UserAgent.ParseAdd("PocketMC-CrossPlatform");
                        using var fileResp = await _httpClient.SendAsync(fileReq, cancellationToken);

                        if (fileResp.IsSuccessStatusCode)
                        {
                            string fileContent = await fileResp.Content.ReadAsStringAsync(cancellationToken);
                            // Validate by parsing
                            _parser.Parse(name, fileContent);

                            string tempFile = targetPath + ".tmp";
                            await File.WriteAllTextAsync(tempFile, fileContent, Encoding.UTF8, cancellationToken);
                            File.Move(tempFile, targetPath, overwrite: true);
                            downloaded++;
                        }
                    }
                    catch
                    {
                        invalid++;
                    }
                }

                if (downloaded > 0)
                {
                    NewsStateChanged?.Invoke();
                }

                return new NewsSyncResult(true, downloaded, invalid);
            }
            catch (Exception ex)
            {
                return new NewsSyncResult(false, 0, 0, ex.Message);
            }
            finally
            {
                _syncGate.Release();
            }
        }

        public void Dispose()
        {
            _syncGate.Dispose();
        }
    }
}
