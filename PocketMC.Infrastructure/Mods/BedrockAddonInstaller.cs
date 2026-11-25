using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PocketMC.Core.Models;
using PocketMC.Infrastructure.Services;
using PocketMC.Infrastructure.Utils;

namespace PocketMC.Infrastructure.Mods;

public sealed class BedrockAddonInstaller
{
    private const string BehaviorPacksDir = "behavior_packs";
    private const string ResourcePacksDir = "resource_packs";
    private const string WorldsDir = "worlds";
    private const string DefaultWorldName = "Bedrock level";
    private const string WorldBehaviorJson = "world_behavior_packs.json";
    private const string WorldResourceJson = "world_resource_packs.json";

    private static readonly JsonDocumentOptions LenientJsonOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions IndentedJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly ILogger<BedrockAddonInstaller> _logger;

    public BedrockAddonInstaller(ILogger<BedrockAddonInstaller>? logger = null)
    {
        _logger = logger ?? NullLogger<BedrockAddonInstaller>.Instance;
    }

    public IReadOnlyList<BedrockPackInfo> GetPacks(string serverDir)
    {
        var result = new List<BedrockPackInfo>();
        if (string.IsNullOrWhiteSpace(serverDir) || !Directory.Exists(serverDir))
        {
            return result;
        }

        string worldDir = ResolveWorldDirectory(serverDir);
        Dictionary<string, int> activeBps = ReadWorldJsonEntries(Path.Combine(worldDir, WorldBehaviorJson));
        Dictionary<string, int> activeRps = ReadWorldJsonEntries(Path.Combine(worldDir, WorldResourceJson));

        CollectPacks(Path.Combine(serverDir, BehaviorPacksDir), BedrockPackType.Behavior, activeBps, result);
        CollectPacks(Path.Combine(serverDir, ResourcePacksDir), BedrockPackType.Resource, activeRps, result);

        return result;
    }

    public async Task<IReadOnlyList<BedrockPackInfo>> InstallAddonAsync(
        string sourceFilePath,
        string serverDir,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException($"Addon file not found: {sourceFilePath}");
        }

        string ext = Path.GetExtension(sourceFilePath).ToLowerInvariant();
        if (ext is not ".mcpack" and not ".mcaddon" and not ".zip")
        {
            throw new NotSupportedException($"Unsupported addon format: {ext}. Only .mcpack, .mcaddon, and .zip are supported.");
        }

        string tempExtractDir = Path.Combine(Path.GetTempPath(), $"pocketmc-bds-addon-{Guid.NewGuid():N}");
        var installed = new List<BedrockPackInfo>();

        try
        {
            Directory.CreateDirectory(tempExtractDir);
            await ExtractRecursivelyAsync(sourceFilePath, tempExtractDir, maxDepth: 4, cancellationToken);

            List<string> manifestPaths = Directory.GetFiles(tempExtractDir, "manifest.json", SearchOption.AllDirectories)
                .OrderBy(p => p.Length)
                .ToList();

            if (manifestPaths.Count == 0)
            {
                throw new InvalidOperationException($"No Bedrock manifest.json found inside '{Path.GetFileName(sourceFilePath)}'.");
            }

            foreach (string manifestPath in manifestPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                BedrockPackInfo? pack = await IngestSinglePackAsync(manifestPath, serverDir, cancellationToken);
                if (pack != null)
                {
                    installed.Add(pack);
                }
            }

            if (installed.Count == 0)
            {
                throw new InvalidOperationException($"Could not install any valid Bedrock packs from '{Path.GetFileName(sourceFilePath)}'.");
            }

            return installed;
        }
        finally
        {
            if (Directory.Exists(tempExtractDir))
            {
                try { Directory.Delete(tempExtractDir, true); }
                catch { }
            }
        }
    }

    public async Task SetPackEnabledAsync(
        string serverDir,
        string packUuid,
        BedrockPackType packType,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        string worldDir = ResolveWorldDirectory(serverDir);
        string jsonPath = Path.Combine(worldDir, packType == BedrockPackType.Behavior ? WorldBehaviorJson : WorldResourceJson);
        JsonArray entries = ReadWorldJsonArray(jsonPath);

        if (isEnabled)
        {
            bool exists = entries.OfType<JsonObject>().Any(obj =>
                string.Equals(obj["pack_id"]?.GetValue<string>(), packUuid, StringComparison.OrdinalIgnoreCase));
            if (!exists)
            {
                BedrockPackInfo? pack = GetPacks(serverDir).FirstOrDefault(p =>
                    p.PackType == packType && string.Equals(p.Uuid, packUuid, StringComparison.OrdinalIgnoreCase));
                int[] versionArray = ParseVersionArray(pack?.Version ?? "1.0.0");
                entries.Add(new JsonObject
                {
                    ["pack_id"] = packUuid,
                    ["version"] = new JsonArray(versionArray[0], versionArray[1], versionArray[2])
                });
            }
        }
        else
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i] is JsonObject obj &&
                    string.Equals(obj["pack_id"]?.GetValue<string>(), packUuid, StringComparison.OrdinalIgnoreCase))
                {
                    entries.RemoveAt(i);
                }
            }
        }

        Directory.CreateDirectory(worldDir);
        await FileUtils.AtomicWriteAllTextAsync(jsonPath, entries.ToJsonString(IndentedJsonOptions), cancellationToken: cancellationToken);
    }

    public async Task DeletePackAsync(
        string serverDir,
        string packUuid,
        BedrockPackType packType,
        CancellationToken cancellationToken = default)
    {
        await SetPackEnabledAsync(serverDir, packUuid, packType, isEnabled: false, cancellationToken);

        BedrockPackInfo? pack = GetPacks(serverDir).FirstOrDefault(p =>
            p.PackType == packType && string.Equals(p.Uuid, packUuid, StringComparison.OrdinalIgnoreCase));

        if (pack != null && !string.IsNullOrWhiteSpace(pack.DirectoryPath) && Directory.Exists(pack.DirectoryPath))
        {
            await FileUtils.CleanDirectoryAsync(pack.DirectoryPath, cancellationToken);
        }
    }

    private async Task<BedrockPackInfo?> IngestSinglePackAsync(string manifestPath, string serverDir, CancellationToken cancellationToken)
    {
        string? packSourceDir = Path.GetDirectoryName(manifestPath);
        if (string.IsNullOrWhiteSpace(packSourceDir)) return null;

        ParsedManifest? parsed = ParseManifest(manifestPath);
        if (parsed == null) return null;

        string categoryDirName = parsed.PackType == BedrockPackType.Behavior ? BehaviorPacksDir : ResourcePacksDir;
        string categoryRoot = Path.Combine(serverDir, categoryDirName);
        Directory.CreateDirectory(categoryRoot);

        string safeName = SanitizeDirectoryName(parsed.Name);
        if (string.IsNullOrWhiteSpace(safeName)) safeName = parsed.Uuid;
        string? targetDir = PathSafety.ValidateContainedPath(categoryRoot, safeName);
        if (targetDir == null) return null;

        await FileUtils.CopyDirectoryAsync(packSourceDir, targetDir, cancellationToken);
        await SetPackEnabledAsync(serverDir, parsed.Uuid, parsed.PackType, isEnabled: true, cancellationToken);

        string? iconPath = File.Exists(Path.Combine(targetDir, "pack_icon.png"))
            ? Path.Combine(targetDir, "pack_icon.png")
            : null;

        return new BedrockPackInfo
        {
            Uuid = parsed.Uuid,
            Name = parsed.Name,
            Description = parsed.Description,
            Version = parsed.Version,
            PackType = parsed.PackType,
            DirectoryPath = targetDir,
            IconPath = iconPath,
            IsEnabled = true,
            LastModified = DateTime.UtcNow
        };
    }

    private static async Task ExtractRecursivelyAsync(string archivePath, string destDir, int maxDepth, CancellationToken cancellationToken)
    {
        if (maxDepth <= 0) return;

        await Task.Run(() => SafeZipExtractor.ExtractZip(archivePath, destDir), cancellationToken);

        foreach (string nestedArchive in Directory.GetFiles(destDir, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".mcpack", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".mcaddon", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .ToList())
        {
            string nestedDest = Path.Combine(destDir, "_nested_" + Path.GetFileNameWithoutExtension(nestedArchive) + "_" + Guid.NewGuid().ToString("N")[..6]);
            Directory.CreateDirectory(nestedDest);
            await ExtractRecursivelyAsync(nestedArchive, nestedDest, maxDepth - 1, cancellationToken);
            File.Delete(nestedArchive);
        }
    }

    private void CollectPacks(
        string rootDir,
        BedrockPackType packType,
        IReadOnlyDictionary<string, int> activeOrders,
        List<BedrockPackInfo> output)
    {
        if (!Directory.Exists(rootDir)) return;

        foreach (string dir in Directory.GetDirectories(rootDir))
        {
            string manifestPath = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifestPath)) continue;

            ParsedManifest? parsed = ParseManifest(manifestPath);
            if (parsed == null) continue;

            bool isEnabled = activeOrders.TryGetValue(parsed.Uuid, out int loadOrder);
            var di = new DirectoryInfo(dir);
            double sizeKb = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                .Sum(f => { try { return new FileInfo(f).Length; } catch { return 0L; } }) / 1024.0;

            string? iconPath = File.Exists(Path.Combine(dir, "pack_icon.png"))
                ? Path.Combine(dir, "pack_icon.png")
                : null;

            output.Add(new BedrockPackInfo
            {
                Uuid = parsed.Uuid,
                Name = parsed.Name,
                Description = parsed.Description,
                Version = parsed.Version,
                PackType = packType,
                DirectoryPath = dir,
                IconPath = iconPath,
                IsEnabled = isEnabled,
                LoadOrder = isEnabled ? loadOrder : -1,
                SizeKb = sizeKb,
                LastModified = di.LastWriteTimeUtc
            });
        }
    }

    private ParsedManifest? ParseManifest(string manifestPath)
    {
        try
        {
            string json = File.ReadAllText(manifestPath);
            using var doc = JsonDocument.Parse(json, LenientJsonOptions);
            JsonElement root = doc.RootElement;
            if (!root.TryGetProperty("header", out var header)) return null;

            string uuid = header.TryGetProperty("uuid", out var uuidEl) ? (uuidEl.GetString() ?? string.Empty) : string.Empty;
            if (string.IsNullOrWhiteSpace(uuid)) return null;

            string name = header.TryGetProperty("name", out var nameEl) ? (nameEl.GetString() ?? uuid) : uuid;
            string desc = header.TryGetProperty("description", out var descEl) ? (descEl.GetString() ?? string.Empty) : string.Empty;
            string version = FormatVersion(header.TryGetProperty("version", out var verEl) ? verEl : default);

            BedrockPackType packType = BedrockPackType.Behavior;
            if (root.TryGetProperty("modules", out var modules) && modules.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement mod in modules.EnumerateArray())
                {
                    if (mod.TryGetProperty("type", out var typeEl))
                    {
                        string t = (typeEl.GetString() ?? string.Empty).ToLowerInvariant();
                        if (t is "resources" or "skin_pack" or "world_template")
                        {
                            packType = BedrockPackType.Resource;
                            break;
                        }
                    }
                }
            }

            return new ParsedManifest(uuid, name, desc, version, packType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Bedrock manifest at {Path}.", manifestPath);
            return null;
        }
    }

    private static string ResolveWorldDirectory(string serverDir)
    {
        string levelName = DefaultWorldName;
        string propsPath = Path.Combine(serverDir, "server.properties");
        if (File.Exists(propsPath))
        {
            foreach (string rawLine in File.ReadAllLines(propsPath))
            {
                string trimmed = rawLine.Trim();
                if (trimmed.StartsWith("level-name=", StringComparison.OrdinalIgnoreCase))
                {
                    string candidate = trimmed.Substring("level-name=".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(candidate))
                    {
                        levelName = candidate;
                    }
                    break;
                }
            }
        }

        string worldsRoot = Path.Combine(serverDir, WorldsDir);
        return PathSafety.ValidateContainedPath(worldsRoot, levelName) ?? Path.Combine(worldsRoot, DefaultWorldName);
    }

    private static Dictionary<string, int> ReadWorldJsonEntries(string jsonPath)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        JsonArray arr = ReadWorldJsonArray(jsonPath);
        int index = 0;
        foreach (JsonNode? node in arr)
        {
            if (node is JsonObject obj && obj["pack_id"]?.GetValue<string>() is { } id && !string.IsNullOrWhiteSpace(id))
            {
                result[id] = index++;
            }
        }
        return result;
    }

    private static JsonArray ReadWorldJsonArray(string jsonPath)
    {
        if (!File.Exists(jsonPath)) return new JsonArray();
        try
        {
            return JsonNode.Parse(File.ReadAllText(jsonPath)) as JsonArray ?? new JsonArray();
        }
        catch
        {
            return new JsonArray();
        }
    }

    private static string FormatVersion(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Array)
        {
            var parts = el.EnumerateArray().Select(p => p.ToString()).ToArray();
            if (parts.Length > 0) return string.Join(".", parts);
        }
        else if (el.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(el.GetString()))
        {
            return el.GetString()!;
        }
        return "1.0.0";
    }

    private static int[] ParseVersionArray(string version)
    {
        string[] parts = version.Split('.', StringSplitOptions.RemoveEmptyEntries);
        int major = parts.Length > 0 && int.TryParse(parts[0], out int a) ? a : 1;
        int minor = parts.Length > 1 && int.TryParse(parts[1], out int b) ? b : 0;
        int patch = parts.Length > 2 && int.TryParse(parts[2], out int c) ? c : 0;
        return new[] { major, minor, patch };
    }

    private static string SanitizeDirectoryName(string name)
        => Regex.Replace(name, @"[^A-Za-z0-9_\- ]", "_").Trim();

    private sealed record ParsedManifest(string Uuid, string Name, string Description, string Version, BedrockPackType PackType);
}
