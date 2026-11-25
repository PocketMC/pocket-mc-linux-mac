using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Configuration;

namespace PocketMC.Infrastructure.Services;

/// <summary>
/// Discovers and installs verified Pumpkin native binaries from official stable Pumpkin-MC releases.
/// </summary>
public sealed class PumpkinProvider : IRefreshableServerSoftwareProvider
{
    private readonly HttpClient _httpClient;
    private readonly DownloaderService _downloader;
    private readonly IPosixFilePermissionService? _permissionService;
    private readonly ILogger<PumpkinProvider> _logger;
    private List<MinecraftVersion>? _cachedVersions;

    public string DisplayName => "Pumpkin";
    public DateTimeOffset? LastSuccessfulRefreshUtc { get; private set; }
    public string? LastRefreshError { get; private set; }

    public PumpkinProvider(
        HttpClient httpClient,
        DownloaderService downloader,
        IPosixFilePermissionService? permissionService = null,
        ILogger<PumpkinProvider>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _permissionService = permissionService;
        _logger = logger ?? NullLogger<PumpkinProvider>.Instance;
    }

    public async Task<List<MinecraftVersion>> GetAvailableVersionsAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (!forceRefresh && _cachedVersions is { Count: > 0 })
        {
            return _cachedVersions;
        }

        try
        {
            LastRefreshError = null;
            string[] assetCandidates = GetAssetNameCandidates();
            string releasesUrl = GetReleasesUrl();
            var versions = new Dictionary<string, MinecraftVersion>(StringComparer.Ordinal);

            for (int page = 1; ; page++)
            {
                using var document = await GetJsonAsync($"{releasesUrl}?per_page=100&page={page}", cancellationToken).ConfigureAwait(false);
                var releases = document.RootElement;
                if (releases.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidDataException("Pumpkin release API returned an invalid release list.");
                }

                foreach (var release in releases.EnumerateArray())
                {
                    string? tag = GetString(release, "tag_name");
                    if (!IsStableRelease(release) || string.IsNullOrWhiteSpace(tag) || !TryFindMatchingAsset(release, assetCandidates, out var asset, out var assetName))
                    {
                        continue;
                    }

                    string url = ValidateDownloadUrl(GetString(asset, "browser_download_url"), tag, assetName);
                    versions.TryAdd(tag, new MinecraftVersion
                    {
                        Id = tag,
                        Type = "release",
                        ReleaseTime = release.TryGetProperty("published_at", out var published) && published.TryGetDateTime(out var date)
                            ? date.ToUniversalTime()
                            : DateTime.MinValue,
                        Url = url
                    });
                }

                if (releases.GetArrayLength() < 100)
                {
                    break;
                }
            }

            _cachedVersions = versions.Values.OrderByDescending(v => v.ReleaseTime).ToList();
            LastSuccessfulRefreshUtc = DateTimeOffset.UtcNow;
            return _cachedVersions;
        }
        catch (Exception ex)
        {
            LastRefreshError = ex.Message;
            _logger.LogWarning(ex, "Failed to refresh Pumpkin versions.");
            throw;
        }
    }

    public async Task<string> DownloadSoftwareAsync(
        string versionId,
        string destinationPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(versionId))
        {
            throw new ArgumentException("Select an exact Pumpkin release.", nameof(versionId));
        }

        string[] assetCandidates = GetAssetNameCandidates();
        using var document = await GetJsonAsync($"{GetReleasesUrl()}/tags/{Uri.EscapeDataString(versionId)}", cancellationToken).ConfigureAwait(false);
        var release = document.RootElement;

        if (!IsStableRelease(release) || !string.Equals(GetString(release, "tag_name"), versionId, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Pumpkin {versionId} is not a valid stable release.");
        }

        if (!TryFindMatchingAsset(release, assetCandidates, out var asset, out var assetName))
        {
            throw new InvalidOperationException($"Pumpkin {versionId} has no release binary for this OS/architecture ({string.Join(", ", assetCandidates)}).");
        }

        string url = ValidateDownloadUrl(GetString(asset, "browser_download_url"), versionId, assetName);
        string hash = await GetSha256Async(release, asset, versionId, assetName, cancellationToken).ConfigureAwait(false);

        string? dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _logger.LogInformation("Installing verified Pumpkin {Version} ({Asset}).", versionId, assetName);
        await _downloader.DownloadFileAsync(url, destinationPath, hash, "SHA256", progress, cancellationToken).ConfigureAwait(false);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _permissionService?.EnsureExecutablePermission(destinationPath);
        }

        return destinationPath;
    }

    public static string GetExecutableFileName() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "pumpkin.exe" : "pumpkin";

    public static string[] GetAssetNameCandidates(OSPlatform? platformOverride = null, Architecture? archOverride = null)
    {
        var arch = archOverride ?? RuntimeInformation.OSArchitecture;

        if (platformOverride == OSPlatform.OSX || (!platformOverride.HasValue && RuntimeInformation.IsOSPlatform(OSPlatform.OSX)))
        {
            return arch switch
            {
                Architecture.Arm64 => new[] { "pumpkin-ARM64-macOS" },
                Architecture.X64 => new[] { "pumpkin-X64-macOS", "pumpkin-ARM64-macOS" },
                _ => throw new PlatformNotSupportedException("Pumpkin on macOS requires ARM64 or x64.")
            };
        }

        if (platformOverride == OSPlatform.Windows || (!platformOverride.HasValue && RuntimeInformation.IsOSPlatform(OSPlatform.Windows)))
        {
            return arch switch
            {
                Architecture.X64 => new[] { "pumpkin-X64-Windows.exe" },
                Architecture.Arm64 => new[] { "pumpkin-ARM64-Windows.exe" },
                _ => throw new PlatformNotSupportedException("Pumpkin on Windows requires x64 or ARM64.")
            };
        }

        return arch switch
        {
            Architecture.X64 => new[] { "pumpkin-X64-Linux", "pumpkin-X64-v3-Linux" },
            Architecture.Arm64 => new[] { "pumpkin-ARM64-Linux" },
            _ => throw new PlatformNotSupportedException("Pumpkin on Linux requires x64 or ARM64.")
        };
    }

    private async Task<string> GetSha256Async(JsonElement release, JsonElement asset, string tag, string assetName, CancellationToken cancellationToken)
    {
        string? digest = GetString(asset, "digest");
        if (!string.IsNullOrWhiteSpace(digest))
        {
            if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) || !IsSha256(digest[7..]))
            {
                throw new InvalidDataException("Pumpkin executable returned an invalid SHA256 digest.");
            }
            return digest[7..];
        }

        if (TryGetAsset(release, "checksums.sha256", out var checksumAsset))
        {
            string checksumUrl = ValidateDownloadUrl(GetString(checksumAsset, "browser_download_url"), tag, "checksums.sha256");
            using var request = CreateRequest(checksumUrl);
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            string checksums = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            foreach (string line in checksums.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length == 2 && fields[1].TrimStart('*') == assetName && IsSha256(fields[0]))
                {
                    return fields[0];
                }
            }
        }

        throw new InvalidDataException($"Pumpkin {tag} has no valid SHA256 checksum for {assetName}.");
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static HttpRequestMessage CreateRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd($"PocketMC.App/{AppConfig.AppVersion}");
        return request;
    }

    private static string GetReleasesUrl()
    {
        if (!Uri.TryCreate(AppConfig.ProviderPumpkinReleases, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !uri.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The Pumpkin release endpoint must use GitHub API over HTTPS.");
        }
        return uri.AbsoluteUri.TrimEnd('/');
    }

    private static string ValidateDownloadUrl(string? url, string tag, string assetName)
    {
        string expectedPath = $"/Pumpkin-MC/Pumpkin/releases/download/{tag}/{assetName}";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            Uri.UnescapeDataString(uri.AbsolutePath) != expectedPath)
        {
            throw new InvalidDataException("Pumpkin returned an asset URL outside the official Pumpkin release.");
        }
        return uri.AbsoluteUri;
    }

    private static bool TryFindMatchingAsset(JsonElement release, string[] candidates, out JsonElement asset, out string matchedName)
    {
        foreach (string candidate in candidates)
        {
            if (TryGetAsset(release, candidate, out asset))
            {
                matchedName = candidate;
                return true;
            }
        }
        asset = default;
        matchedName = string.Empty;
        return false;
    }

    private static bool IsStableRelease(JsonElement release) =>
        release.ValueKind == JsonValueKind.Object &&
        release.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.False &&
        release.TryGetProperty("prerelease", out var prerelease) && prerelease.ValueKind == JsonValueKind.False;

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool TryGetAsset(JsonElement release, string name, out JsonElement asset)
    {
        if (release.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var candidate in assets.EnumerateArray())
            {
                if (string.Equals(GetString(candidate, "name"), name, StringComparison.Ordinal))
                {
                    asset = candidate;
                    return true;
                }
            }
        }
        asset = default;
        return false;
    }

    private static bool IsSha256(string value) => value.Length == 64 && value.All(char.IsAsciiHexDigit);
}
