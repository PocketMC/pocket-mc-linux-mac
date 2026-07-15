using PocketMC.Core.Models;
using PocketMC.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PocketMC.Infrastructure.Services
{
    public class ModrinthSearchResult
    {
        [JsonPropertyName("hits")]
        public List<ModrinthHit> Hits { get; set; } = new();
    }

    public class ModrinthHit
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("downloads")]
        public int Downloads { get; set; }

        [JsonPropertyName("icon_url")]
        public string? IconUrl { get; set; }

        [JsonPropertyName("slug")]
        public string Slug { get; set; } = "";

        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; } = "";
    }

    public class ModrinthVersion
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("files")]
        public List<ModrinthFile> Files { get; set; } = new();

        [JsonPropertyName("loaders")]
        public List<string> Loaders { get; set; } = new();

        [JsonPropertyName("dependencies")]
        public List<ModrinthDependency> Dependencies { get; set; } = new();

        [JsonPropertyName("version_type")]
        public string VersionType { get; set; } = "release";

        [JsonPropertyName("game_versions")]
        public List<string> GameVersions { get; set; } = new();
    }

    public class ModrinthDependency
    {
        [JsonPropertyName("version_id")]
        public string? VersionId { get; set; }

        [JsonPropertyName("project_id")]
        public string? ProjectId { get; set; }

        [JsonPropertyName("dependency_type")]
        public string DependencyType { get; set; } = ""; // "required", "optional", "incompatible", "embedded"
    }

    public class ModrinthFile
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = "";

        [JsonPropertyName("filename")]
        public string FileName { get; set; } = "";

        [JsonPropertyName("primary")]
        public bool IsPrimary { get; set; }

        [JsonPropertyName("hashes")]
        public Dictionary<string, string> Hashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public class ModrinthProject
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("slug")]
        public string Slug { get; set; } = "";

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("icon_url")]
        public string? IconUrl { get; set; }

        [JsonPropertyName("client_side")]
        public string ClientSide { get; set; } = "unknown";

        [JsonPropertyName("server_side")]
        public string ServerSide { get; set; } = "unknown";
    }

    public class ModrinthService : IAddonProvider
    {
        private const int MaxProviderAttempts = 3;
        private readonly HttpClient _httpClient;

        public ModrinthService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public string Name => "Modrinth";

        public static IReadOnlyList<string> BuildMinecraftVersionCandidates(string mcVersion)
        {
            if (string.IsNullOrEmpty(mcVersion) || mcVersion == "*")
            {
                return new[] { "" };
            }

            var parts = mcVersion.Split('.');
            if (parts.Length == 3)
            {
                return new[] { mcVersion, $"{parts[0]}.{parts[1]}" };
            }

            return new[] { mcVersion };
        }

        public static ModrinthFile? SelectCompatibleFile(ModrinthVersion version, string loader)
        {
            if (version.Files == null || version.Files.Count == 0) return null;
            if (version.Files.Count == 1) return version.Files[0];
            if (string.IsNullOrWhiteSpace(loader))
            {
                return version.Files.FirstOrDefault(f => f.IsPrimary) ?? version.Files.FirstOrDefault();
            }

            var normalizedLoader = loader.ToLowerInvariant();

            var candidates = new List<ModrinthFile>();
            foreach (var f in version.Files)
            {
                if (string.IsNullOrEmpty(f.FileName)) continue;
                var fn = f.FileName.ToLowerInvariant();

                if (IsPluginLoader(normalizedLoader))
                {
                    if (fn.Contains("fabric") || fn.Contains("forge") || fn.Contains("neoforge") || fn.Contains("quilt"))
                    {
                        continue;
                    }
                }
                else if (normalizedLoader == "fabric")
                {
                    if (fn.Contains("forge") || fn.Contains("neoforge"))
                    {
                        continue;
                    }
                }
                else if (normalizedLoader == "forge")
                {
                    if (fn.Contains("fabric") || fn.Contains("neoforge"))
                    {
                        continue;
                    }
                }
                else if (normalizedLoader == "neoforge")
                {
                    if (fn.Contains("fabric") || (fn.Contains("forge") && !fn.Contains("neoforge")))
                    {
                        continue;
                    }
                }

                candidates.Add(f);
            }

            if (candidates.Count == 0)
            {
                candidates = version.Files;
            }

            var loaderMentioned = candidates.FirstOrDefault(f => f.FileName != null && f.FileName.ToLowerInvariant().Contains(normalizedLoader));
            if (loaderMentioned != null) return loaderMentioned;

            var primary = candidates.FirstOrDefault(f => f.IsPrimary);
            if (primary != null) return primary;

            return candidates.FirstOrDefault();
        }

        private static bool IsPluginLoader(string loader)
        {
            return loader == "paper" || loader == "spigot" || loader == "bukkit";
        }

        public async Task<List<ModrinthHit>> SearchAsync(string type, string mcVersion, IReadOnlyList<string> loaders, string sort = "relevance", string query = "", int offset = 0)
        {
            var mcCandidates = BuildMinecraftVersionCandidates(mcVersion);

            foreach (var mcCand in mcCandidates)
            {
                var hits = await SearchInternalAsync(type, mcCand, loaders, sort, query, offset);
                if (hits.Count > 0)
                {
                    return hits;
                }
            }

            return new List<ModrinthHit>();
        }

        private async Task<List<ModrinthHit>> SearchInternalAsync(string type, string mcVersion, IReadOnlyList<string> loaders, string sort, string query, int offset)
        {
            try
            {
                var facetList = new List<List<string>>();
                facetList.Add(new List<string> { type });

                if (type == "project_type:mod" || type == "project_type:modpack")
                {
                    facetList.Add(new List<string> { "server_side:required", "server_side:optional" });
                }

                if (!string.IsNullOrEmpty(mcVersion) && mcVersion != "*")
                {
                    facetList.Add(new List<string> { $"versions:{mcVersion}" });
                }

                if ((type == "project_type:mod" || type == "project_type:plugin") && loaders != null && loaders.Count > 0)
                {
                    var loaderFacet = loaders.Select(l => $"categories:{l.ToLowerInvariant()}").ToList();
                    facetList.Add(loaderFacet);
                }

                string facets = JsonSerializer.Serialize(facetList);
                string url = $"https://api.modrinth.com/v2/search?query={Uri.EscapeDataString(query)}&facets={Uri.EscapeDataString(facets)}&limit=20&offset={offset}&index={sort}";

                var result = await GetFromJsonWithRetryAsync<ModrinthSearchResult>(url).ConfigureAwait(false);
                return result?.Hits ?? new();
            }
            catch
            {
                return new();
            }
        }

        public async Task<MarketplaceVersion?> GetLatestVersionAsync(string projectId, string mcVersion, string loader)
        {
            return await GetLatestVersionAsync(projectId, mcVersion, new[] { loader });
        }

        public async Task<MarketplaceVersion?> GetLatestVersionAsync(string projectId, string mcVersion, IReadOnlyList<string> loaderCandidates)
        {
            var mcCandidates = BuildMinecraftVersionCandidates(mcVersion);
            var projectInfo = await GetProjectInfoAsync(projectId).ConfigureAwait(false);
            string projectSlug = projectInfo?.Slug ?? projectId;

            foreach (var mcCand in mcCandidates)
            {
                var mVersion = await GetLatestVersionInternalAsync(projectSlug, mcCand, loaderCandidates).ConfigureAwait(false);
                if (mVersion != null)
                {
                    foreach (var loaderCand in loaderCandidates)
                    {
                        var compatFile = SelectCompatibleFile(mVersion, loaderCand);
                        if (compatFile != null)
                        {
                            var mv = MapToMarketplaceVersion(mVersion, projectInfo, compatFile);
                            mv.DownloadUrl = compatFile.Url;
                            mv.FileName = compatFile.FileName;
                            mv.Hash = GetPreferredHash(compatFile, out string? hashType);
                            mv.HashType = hashType;
                            mv.SelectedLoader = loaderCand;
                            mv.MatchedMinecraftVersion = mcCand;
                            mv.IconUrl = projectInfo?.IconUrl;
                            return mv;
                        }
                    }
                }
            }

            return null;
        }

        public async Task<MarketplaceVersion?> GetVersionByIdAsync(string versionId)
        {
            try
            {
                string url = $"https://api.modrinth.com/v2/version/{versionId}";
                var mVersion = await GetFromJsonWithRetryAsync<ModrinthVersion>(url).ConfigureAwait(false);
                if (mVersion == null) return null;

                var projectInfo = await GetProjectInfoAsync(mVersion.ProjectId).ConfigureAwait(false);
                return MapToMarketplaceVersion(mVersion, projectInfo);
            }
            catch
            {
                return null;
            }
        }

        public async Task<MarketplaceProjectInfo?> GetProjectInfoAsync(string projectIdOrSlug)
        {
            try
            {
                string url = $"https://api.modrinth.com/v2/project/{projectIdOrSlug}";
                var project = await GetFromJsonWithRetryAsync<ModrinthProject>(url).ConfigureAwait(false);
                if (project == null) return null;

                return new MarketplaceProjectInfo
                {
                    Id = project.Id,
                    Title = project.Title,
                    Slug = project.Slug,
                    IconUrl = project.IconUrl,
                    ClientSide = project.ClientSide,
                    ServerSide = project.ServerSide
                };
            }
            catch
            {
                return null;
            }
        }

        private MarketplaceVersion MapToMarketplaceVersion(
            ModrinthVersion v,
            MarketplaceProjectInfo? projectInfo,
            ModrinthFile? preferredFile = null)
        {
            var primaryFile = preferredFile ?? v.Files.FirstOrDefault(f => f.IsPrimary) ?? v.Files.FirstOrDefault() ?? new ModrinthFile();

            var result = new MarketplaceVersion
            {
                Id = v.Id,
                Name = v.Name,
                ProjectId = v.ProjectId,
                ProjectTitle = projectInfo?.Title ?? v.ProjectId,
                FileName = primaryFile.FileName,
                DownloadUrl = primaryFile.Url,
                Hash = GetPreferredHash(primaryFile, out string? hashType),
                HashType = hashType,
                ReleaseType = string.IsNullOrWhiteSpace(v.VersionType) ? "release" : v.VersionType,
                ClientSide = projectInfo?.ClientSide,
                ServerSide = projectInfo?.ServerSide,
                IconUrl = projectInfo?.IconUrl
            };

            if (!result.ReleaseType.Equals("release", StringComparison.OrdinalIgnoreCase))
            {
                result.Warnings.Add($"Only a {result.ReleaseType} version is available for this selection. Review before installing.");
            }

            foreach (var dep in v.Dependencies)
            {
                if (string.IsNullOrEmpty(dep.ProjectId)) continue;

                result.Dependencies.Add(new MarketplaceDependency
                {
                    ProjectId = dep.ProjectId,
                    VersionId = dep.VersionId,
                    Type = dep.DependencyType.ToLowerInvariant() switch
                    {
                        "required" => DependencyType.Required,
                        "optional" => DependencyType.Optional,
                        "embedded" => DependencyType.Embedded,
                        "incompatible" => DependencyType.Incompatible,
                        _ => DependencyType.Optional
                    }
                });
            }

            return result;
        }

        private async Task<ModrinthVersion?> GetLatestVersionInternalAsync(string slug, string mcVersion, IReadOnlyList<string>? loaders = null)
        {
            try
            {
                string baseUrl = $"https://api.modrinth.com/v2/project/{slug}/version";
                var queryParams = new List<string>();

                if (!string.IsNullOrEmpty(mcVersion) && mcVersion != "*")
                    queryParams.Add($"game_versions={Uri.EscapeDataString(JsonSerializer.Serialize(new[] { mcVersion }))}");

                if (loaders != null && loaders.Count > 0)
                    queryParams.Add($"loaders={Uri.EscapeDataString(JsonSerializer.Serialize(loaders))}");

                string url = queryParams.Count > 0 ? $"{baseUrl}?{string.Join("&", queryParams)}" : baseUrl;
                var versions = await GetFromJsonWithRetryAsync<List<ModrinthVersion>>(url).ConfigureAwait(false);

                if (versions?.Count > 0)
                    return SelectPreferredVersion(versions);

                if (loaders != null && loaders.Count > 0)
                {
                    string relaxedUrl = !string.IsNullOrEmpty(mcVersion) && mcVersion != "*"
                        ? $"{baseUrl}?game_versions={Uri.EscapeDataString(JsonSerializer.Serialize(new[] { mcVersion }))}"
                        : baseUrl;

                    var relaxedVersions = await GetFromJsonWithRetryAsync<List<ModrinthVersion>>(relaxedUrl)
                        .ConfigureAwait(false) ?? new();
                    var loaderMatches = relaxedVersions
                        .Where(v => v.Loaders.Any(l => loaders.Contains(l, StringComparer.OrdinalIgnoreCase)))
                        .ToList();
                    if (loaderMatches.Count > 0) return SelectPreferredVersion(loaderMatches);
                    return null;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static ModrinthVersion SelectPreferredVersion(IReadOnlyList<ModrinthVersion> versions)
        {
            return versions.FirstOrDefault(v => v.VersionType.Equals("release", StringComparison.OrdinalIgnoreCase))
                ?? versions[0];
        }

        private async Task<T?> GetFromJsonWithRetryAsync<T>(string url)
        {
            for (int attempt = 1; attempt <= MaxProviderAttempts; attempt++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    using HttpResponseMessage response = await _httpClient
                        .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                        .ConfigureAwait(false);

                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return default;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        if (ShouldRetryStatus(response.StatusCode) && attempt < MaxProviderAttempts)
                        {
                            var delay = GetRetryDelay(attempt);
                            await Task.Delay(delay).ConfigureAwait(false);
                            continue;
                        }

                        return default;
                    }

                    return await response.Content.ReadFromJsonAsync<T>().ConfigureAwait(false);
                }
                catch (Exception ex) when (IsRetryableProviderException(ex) && attempt < MaxProviderAttempts)
                {
                    await Task.Delay(GetRetryDelay(attempt)).ConfigureAwait(false);
                }
                catch
                {
                    return default;
                }
            }

            return default;
        }

        private static bool ShouldRetryStatus(HttpStatusCode statusCode)
        {
            int numericStatus = (int)statusCode;
            return statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
                   numericStatus >= 500;
        }

        private static bool IsRetryableProviderException(Exception ex) =>
            ex is HttpRequestException or TaskCanceledException;

        private static TimeSpan GetRetryDelay(int attempt) => attempt switch
        {
            1 => TimeSpan.FromMilliseconds(300),
            2 => TimeSpan.FromMilliseconds(900),
            _ => TimeSpan.FromSeconds(2)
        };

        private static string? GetPreferredHash(ModrinthFile file, out string? hashType)
        {
            if (file.Hashes.TryGetValue("sha512", out string? sha512) && !string.IsNullOrWhiteSpace(sha512))
            {
                hashType = "sha512";
                return sha512;
            }

            if (file.Hashes.TryGetValue("sha1", out string? sha1) && !string.IsNullOrWhiteSpace(sha1))
            {
                hashType = "sha1";
                return sha1;
            }

            hashType = null;
            return null;
        }

        public Task<Dictionary<string, ModrinthVersion>> GetVersionsByHashesAsync(IEnumerable<string> hashes) =>
            GetVersionsByHashesAsync(hashes, "sha1");

        public async Task<Dictionary<string, ModrinthVersion>> GetVersionsByHashesAsync(
            IEnumerable<string> hashes,
            string algorithm)
        {
            try
            {
                var requestBody = new { hashes = hashes.ToList(), algorithm };
                return await PostJsonForJsonWithRetryAsync<Dictionary<string, ModrinthVersion>>(
                        "https://api.modrinth.com/v2/version_files",
                        requestBody)
                    .ConfigureAwait(false)
                    ?? new();
            }
            catch
            {
                return new();
            }
        }

        private async Task<T?> PostJsonForJsonWithRetryAsync<T>(string url, object body)
        {
            for (int attempt = 1; attempt <= MaxProviderAttempts; attempt++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = JsonContent.Create(body)
                    };

                    using HttpResponseMessage response = await _httpClient
                        .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                        .ConfigureAwait(false);

                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return default;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        if (ShouldRetryStatus(response.StatusCode) && attempt < MaxProviderAttempts)
                        {
                            var delay = GetRetryDelay(attempt);
                            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                            {
                                if (response.Headers.TryGetValues("Retry-After", out var values) &&
                                    int.TryParse(values.FirstOrDefault(), out int seconds))
                                {
                                    delay = TimeSpan.FromSeconds(seconds);
                                }
                                else if (response.Headers.TryGetValues("X-RateLimit-Reset", out var resetValues) &&
                                         int.TryParse(resetValues.FirstOrDefault(), out int resetSeconds))
                                {
                                    delay = TimeSpan.FromSeconds(resetSeconds);
                                }
                            }
                            await Task.Delay(delay).ConfigureAwait(false);
                            continue;
                        }

                        return default;
                    }

                    return await response.Content.ReadFromJsonAsync<T>().ConfigureAwait(false);
                }
                catch (Exception ex) when (IsRetryableProviderException(ex) && attempt < MaxProviderAttempts)
                {
                    await Task.Delay(GetRetryDelay(attempt)).ConfigureAwait(false);
                }
                catch
                {
                    return default;
                }
            }

            return default;
        }
    }
}
