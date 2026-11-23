using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PocketMC.Core.Models;

namespace PocketMC.Infrastructure.Services
{
    public class InstanceVersionTargetService
    {
        private readonly HttpClient _httpClient;

        public InstanceVersionTargetService(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient();
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "PocketMC-Desktop/1.0");
            }
        }

        public async Task<IReadOnlyList<UpdateTargetVersionInfo>> GetAvailableTargetVersionsAsync(
            ServerInstance instance,
            bool includeSnapshots = false,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(instance);

            var rawVersions = await FetchVersionsForEngineAsync(instance.EngineType, includeSnapshots, ct);
            var result = new List<UpdateTargetVersionInfo>();

            Version.TryParse(CleanVersionString(instance.EngineVersion), out var currentParsed);

            foreach (var ver in rawVersions)
            {
                bool isCurrent = ver.Equals(instance.EngineVersion, StringComparison.OrdinalIgnoreCase);
                Version.TryParse(CleanVersionString(ver), out var targetParsed);

                bool isUpgrade = false;
                if (currentParsed != null && targetParsed != null)
                {
                    isUpgrade = targetParsed > currentParsed;
                }
                else
                {
                    // If parsing is not semantic versioning, order determines upgrade
                    int currIdx = rawVersions.IndexOf(instance.EngineVersion);
                    int targetIdx = rawVersions.IndexOf(ver);
                    isUpgrade = currIdx > 0 && targetIdx >= 0 && targetIdx < currIdx;
                }

                result.Add(new UpdateTargetVersionInfo
                {
                    Id = ver,
                    Type = IsSnapshotOrPreRelease(ver) ? "snapshot" : "release",
                    IsCurrent = isCurrent,
                    IsUpgrade = isUpgrade
                });
            }

            return result;
        }

        public async Task<List<string>> FetchVersionsForEngineAsync(
            EngineType engineType,
            bool includeSnapshots = false,
            CancellationToken ct = default)
        {
            var versions = new List<string>();

            try
            {
                switch (engineType)
                {
                    case EngineType.VanillaJava:
                        {
                            var response = await _httpClient.GetStringAsync("https://launchermeta.mojang.com/mc/game/version_manifest.json", ct);
                            using var doc = JsonDocument.Parse(response);
                            if (doc.RootElement.TryGetProperty("versions", out var vers))
                            {
                                foreach (var v in vers.EnumerateArray())
                                {
                                    var type = v.GetProperty("type").GetString();
                                    var id = v.GetProperty("id").GetString();
                                    if (!string.IsNullOrEmpty(id))
                                    {
                                        if (includeSnapshots || type == "release")
                                        {
                                            versions.Add(id);
                                        }
                                    }
                                }
                            }
                            break;
                        }

                    case EngineType.Paper:
                        {
                            var response = await _httpClient.GetStringAsync("https://api.papermc.io/v2/projects/paper", ct);
                            using var doc = JsonDocument.Parse(response);
                            if (doc.RootElement.TryGetProperty("versions", out var vers))
                            {
                                foreach (var v in vers.EnumerateArray())
                                {
                                    var id = v.GetString();
                                    if (!string.IsNullOrEmpty(id) && (includeSnapshots || !IsSnapshotOrPreRelease(id)))
                                    {
                                        versions.Add(id);
                                    }
                                }
                            }
                            break;
                        }

                    case EngineType.Fabric:
                        {
                            var response = await _httpClient.GetStringAsync("https://meta.fabricmc.net/v2/versions/game", ct);
                            using var doc = JsonDocument.Parse(response);
                            foreach (var v in doc.RootElement.EnumerateArray())
                            {
                                string? id = null;
                                if (v.TryGetProperty("version", out var vProp)) id = vProp.GetString();
                                else if (v.TryGetProperty("id", out var iProp)) id = iProp.GetString();

                                var stable = v.TryGetProperty("stable", out var sProp) && sProp.GetBoolean();
                                if (!string.IsNullOrEmpty(id) && (includeSnapshots || (stable && !IsSnapshotOrPreRelease(id))))
                                {
                                    versions.Add(id);
                                }
                            }
                            break;
                        }

                    case EngineType.Forge:
                        {
                            var response = await _httpClient.GetStringAsync("https://meta.prismlauncher.org/v1/net.minecraftforge/index.json", ct);
                            using var doc = JsonDocument.Parse(response);
                            if (doc.RootElement.TryGetProperty("versions", out var vers))
                            {
                                foreach (var v in vers.EnumerateArray())
                                {
                                    if (v.TryGetProperty("requires", out var req))
                                    {
                                        foreach (var r in req.EnumerateArray())
                                        {
                                            if (r.TryGetProperty("uid", out var uid) && uid.GetString() == "net.minecraft" &&
                                                r.TryGetProperty("equals", out var eq))
                                            {
                                                var mcVer = eq.GetString();
                                                if (!string.IsNullOrEmpty(mcVer)) versions.Add(mcVer);
                                            }
                                        }
                                    }
                                }
                            }
                            break;
                        }

                    case EngineType.NeoForge:
                        {
                            var response = await _httpClient.GetStringAsync("https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge", ct);
                            using var doc = JsonDocument.Parse(response);
                            if (doc.RootElement.TryGetProperty("versions", out var vers))
                            {
                                foreach (var v in vers.EnumerateArray())
                                {
                                    var vStr = v.GetString();
                                    if (!string.IsNullOrEmpty(vStr) && (includeSnapshots || !IsSnapshotOrPreRelease(vStr)))
                                    {
                                        versions.Add(vStr);
                                    }
                                }
                            }
                            break;
                        }

                    case EngineType.Bedrock:
                        {
                            var response = await _httpClient.GetStringAsync("https://raw.githubusercontent.com/kittizz/bedrock-server-downloads/main/bedrock-server-downloads.json", ct);
                            using var doc = JsonDocument.Parse(response);
                            if (doc.RootElement.TryGetProperty("release", out var rel))
                            {
                                foreach (var p in rel.EnumerateObject())
                                {
                                    if (includeSnapshots || !IsSnapshotOrPreRelease(p.Name))
                                    {
                                        versions.Add(p.Name);
                                    }
                                }
                            }
                            break;
                        }

                    case EngineType.PocketMine:
                        {
                            var response = await _httpClient.GetStringAsync("https://api.github.com/repos/pmmp/PocketMine-MP/releases", ct);
                            using var doc = JsonDocument.Parse(response);
                            foreach (var r in doc.RootElement.EnumerateArray())
                            {
                                var isPre = r.TryGetProperty("prerelease", out var pProp) && pProp.GetBoolean();
                                var tag = r.GetProperty("tag_name").GetString();
                                if (!string.IsNullOrEmpty(tag) && (includeSnapshots || (!isPre && !IsSnapshotOrPreRelease(tag))))
                                {
                                    versions.Add(tag);
                                }
                            }
                            break;
                        }
                }
            }
            catch
            {
                // Return whatever versions could be fetched or empty list
            }

            return SortVersionsDescending(versions);
        }

        private static List<string> SortVersionsDescending(IEnumerable<string> versions)
        {
            return versions.Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(v =>
                {
                    var clean = CleanVersionString(v);
                    var coreVer = clean.Split(new[] { '-', '+' }, 2)[0];
                    Version.TryParse(coreVer, out var parsed);
                    return new { Original = v, Parsed = parsed ?? new Version(0, 0) };
                })
                .OrderByDescending(x => x.Parsed)
                .ThenByDescending(x => x.Original, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.Original)
                .ToList();
        }

        private static string CleanVersionString(string v)
        {
            if (string.IsNullOrWhiteSpace(v)) return string.Empty;
            return v.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? v[1..] : v;
        }

        private static bool IsSnapshotOrPreRelease(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return false;
            var lower = version.ToLowerInvariant();
            return lower.Contains("-rc") ||
                   lower.Contains(".rc") ||
                   lower.Contains("-pre") ||
                   lower.Contains(".pre") ||
                   lower.Contains("snapshot") ||
                   lower.Contains("preview") ||
                   lower.Contains("beta") ||
                   lower.Contains("alpha") ||
                   lower.Contains("-dev") ||
                   Regex.IsMatch(lower, @"\d{2}w\d{2}[a-z]");
        }
    }
}
