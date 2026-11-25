using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using PocketMC.Core.Models;

namespace PocketMC.Infrastructure.Mods;

public static class JavaModMetadataService
{
    private const int MaxIconBytes = 2 * 1024 * 1024;
    private static readonly ConcurrentDictionary<(string Path, long Length, DateTime LastWriteUtc, string ExpectedLoader), JavaModMetadata> Cache = new();

    public static bool IsPluginJar(string filePath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(filePath);
            return archive.GetEntry("plugin.yml") != null || archive.GetEntry("paper-plugin.yml") != null;
        }
        catch
        {
            return false;
        }
    }

    public static JavaModMetadata ScanJar(string filePath, string? expectedLoader = null)
    {
        try
        {
            var fi = new FileInfo(filePath);
            if (!fi.Exists)
            {
                return BuildFallbackMetadata(Path.GetFileName(filePath), isCorrupt: true);
            }

            var key = (fi.FullName, fi.Length, fi.LastWriteTimeUtc, expectedLoader ?? string.Empty);
            if (Cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            JavaModMetadata metadata = ScanJarInternal(fi, expectedLoader);
            metadata.FileName = fi.Name;
            Cache[key] = metadata;
            return metadata;
        }
        catch
        {
            return BuildFallbackMetadata(Path.GetFileName(filePath), isCorrupt: true);
        }
    }

    private static JavaModMetadata BuildFallbackMetadata(string fileName, bool isCorrupt) => new()
    {
        DisplayName = CleanJarName(fileName),
        FileName = fileName,
        LoaderType = "Unknown",
        IsCorrupt = isCorrupt
    };

    private static JavaModMetadata ScanJarInternal(FileInfo fi, string? expectedLoader)
    {
        var metadata = new JavaModMetadata { DisplayName = CleanJarName(fi.Name) };

        try
        {
            using var archive = ZipFile.OpenRead(fi.FullName);

            bool isPluginExpected = IsPluginLoader(expectedLoader);
            ZipArchiveEntry? quiltEntry = archive.GetEntry("quilt.mod.json");
            ZipArchiveEntry? fabricEntry = archive.GetEntry("fabric.mod.json");
            ZipArchiveEntry? neoforgeEntry = archive.GetEntry("META-INF/neoforge.mods.toml");
            ZipArchiveEntry? forgeEntry = archive.GetEntry("META-INF/mods.toml");
            ZipArchiveEntry? pluginEntry = archive.GetEntry("paper-plugin.yml") ?? archive.GetEntry("plugin.yml");

            metadata.HasPluginMetadata = pluginEntry != null;

            if (isPluginExpected && pluginEntry != null)
            {
                ParsePluginYml(pluginEntry, metadata);
            }
            else if (quiltEntry != null && (string.Equals(expectedLoader, "Quilt", StringComparison.OrdinalIgnoreCase) || fabricEntry == null))
            {
                ParseQuiltJson(quiltEntry, metadata, archive);
            }
            else if (fabricEntry != null)
            {
                ParseFabricJson(fabricEntry, metadata, archive);
            }
            else if (neoforgeEntry != null)
            {
                ParseModsToml(neoforgeEntry, metadata, "NeoForge", archive);
            }
            else if (forgeEntry != null)
            {
                ParseModsToml(forgeEntry, metadata, "Forge", archive);
            }
            else if (pluginEntry != null)
            {
                ParsePluginYml(pluginEntry, metadata);
                if (!isPluginExpected && !string.IsNullOrWhiteSpace(expectedLoader))
                {
                    metadata.IsPluginInModsFolder = true;
                }
            }
            else if (quiltEntry != null)
            {
                ParseQuiltJson(quiltEntry, metadata, archive);
            }

            if (metadata.IconBytes == null)
            {
                TryFindFallbackIcon(archive, metadata);
            }

            metadata.SanitizeDependencies();
            return metadata;
        }
        catch
        {
            metadata.IsCorrupt = true;
            return metadata;
        }
    }

    private static bool IsPluginLoader(string? loader) =>
        !string.IsNullOrWhiteSpace(loader) &&
        (loader.Equals("Plugin", StringComparison.OrdinalIgnoreCase) ||
         loader.Equals("Paper", StringComparison.OrdinalIgnoreCase) ||
         loader.Equals("Purpur", StringComparison.OrdinalIgnoreCase) ||
         loader.Equals("Spigot", StringComparison.OrdinalIgnoreCase) ||
         loader.Equals("Bukkit", StringComparison.OrdinalIgnoreCase) ||
         loader.Equals("BungeeCord", StringComparison.OrdinalIgnoreCase) ||
         loader.Equals("Waterfall", StringComparison.OrdinalIgnoreCase) ||
         loader.Equals("Velocity", StringComparison.OrdinalIgnoreCase));

    private static void ParseFabricJson(ZipArchiveEntry entry, JavaModMetadata metadata, ZipArchive archive)
    {
        metadata.LoaderType = "Fabric";
        using var stream = entry.Open();
        using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        JsonElement root = doc.RootElement;

        if (root.TryGetProperty("id", out var id)) metadata.ModId = id.GetString() ?? string.Empty;
        if (root.TryGetProperty("name", out var name) && !string.IsNullOrWhiteSpace(name.GetString())) metadata.DisplayName = name.GetString()!;
        if (root.TryGetProperty("version", out var ver)) metadata.Version = ver.GetString();
        if (root.TryGetProperty("description", out var desc)) metadata.Description = desc.GetString();
        if (root.TryGetProperty("icon", out var icon) && icon.ValueKind == JsonValueKind.String)
        {
            ReadIconFromEntry(archive, icon.GetString(), metadata);
        }

        if (root.TryGetProperty("environment", out var env) && env.ValueKind == JsonValueKind.String)
        {
            switch (env.GetString()?.Trim().ToLowerInvariant())
            {
                case "client":
                    metadata.SideSupport = ModSideSupport.ClientOnly;
                    metadata.SideLabel = "Client Only";
                    break;
                case "server":
                    metadata.SideSupport = ModSideSupport.ServerOnly;
                    metadata.SideLabel = "Server Only";
                    break;
                case "*":
                    metadata.SideSupport = ModSideSupport.Both;
                    metadata.SideLabel = "Both";
                    break;
            }
        }

        if (root.TryGetProperty("depends", out var depends) && depends.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty prop in depends.EnumerateObject())
            {
                if (prop.Name.Equals("minecraft", StringComparison.OrdinalIgnoreCase))
                {
                    metadata.RequiredMinecraftVersion = prop.Value.ToString();
                }
                else
                {
                    metadata.RequiredDependencies.Add(prop.Name);
                }
            }
        }

        if (root.TryGetProperty("suggests", out var suggests) && suggests.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty prop in suggests.EnumerateObject())
            {
                metadata.OptionalDependencies.Add(prop.Name);
            }
        }
    }

    private static void ParseQuiltJson(ZipArchiveEntry entry, JavaModMetadata metadata, ZipArchive archive)
    {
        metadata.LoaderType = "Quilt";
        using var stream = entry.Open();
        using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        if (!doc.RootElement.TryGetProperty("quilt_loader", out var ql)) return;

        if (ql.TryGetProperty("id", out var id)) metadata.ModId = id.GetString() ?? string.Empty;
        if (ql.TryGetProperty("version", out var ver)) metadata.Version = ver.GetString();

        if (ql.TryGetProperty("metadata", out var meta))
        {
            if (meta.TryGetProperty("name", out var name) && !string.IsNullOrWhiteSpace(name.GetString())) metadata.DisplayName = name.GetString()!;
            if (meta.TryGetProperty("description", out var desc)) metadata.Description = desc.GetString();
            if (meta.TryGetProperty("icon", out var icon) && icon.ValueKind == JsonValueKind.String)
            {
                ReadIconFromEntry(archive, icon.GetString(), metadata);
            }
        }

        if (ql.TryGetProperty("depends", out var deps) && deps.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement dep in deps.EnumerateArray())
            {
                if (dep.ValueKind == JsonValueKind.String && dep.GetString() is { } s)
                {
                    metadata.RequiredDependencies.Add(s);
                }
                else if (dep.ValueKind == JsonValueKind.Object && dep.TryGetProperty("id", out var depId) && depId.GetString() is { } idStr)
                {
                    if (dep.TryGetProperty("optional", out var opt) && opt.ValueKind == JsonValueKind.True)
                        metadata.OptionalDependencies.Add(idStr);
                    else
                        metadata.RequiredDependencies.Add(idStr);
                }
            }
        }
    }

    private static void ParseModsToml(ZipArchiveEntry entry, JavaModMetadata metadata, string loaderType, ZipArchive archive)
    {
        metadata.LoaderType = loaderType;
        using var reader = new StreamReader(entry.Open());
        string text = reader.ReadToEnd();

        string? modId = MatchTomlString(text, "modId");
        string? displayName = MatchTomlString(text, "displayName");
        string? version = MatchTomlString(text, "version");
        string? description = MatchTomlString(text, "description");
        string? logoFile = MatchTomlString(text, "logoFile");

        if (!string.IsNullOrWhiteSpace(modId)) metadata.ModId = modId;
        if (!string.IsNullOrWhiteSpace(displayName)) metadata.DisplayName = displayName;
        if (!string.IsNullOrWhiteSpace(version) && !version.StartsWith("${", StringComparison.Ordinal)) metadata.Version = version;
        if (!string.IsNullOrWhiteSpace(description)) metadata.Description = description;
        if (!string.IsNullOrWhiteSpace(logoFile)) ReadIconFromEntry(archive, logoFile, metadata);
    }

    private static void ParsePluginYml(ZipArchiveEntry entry, JavaModMetadata metadata)
    {
        metadata.LoaderType = "Plugin";
        using var reader = new StreamReader(entry.Open());
        string[] lines = reader.ReadToEnd().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        string? currentListKey = null;

        foreach (string rawLine in lines)
        {
            string trimmed = rawLine.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (rawLine.StartsWith(" ") || rawLine.StartsWith("\t"))
            {
                if (trimmed.StartsWith("- ", StringComparison.Ordinal) && currentListKey is "depend" or "softdepend")
                {
                    string depName = trimmed.Substring(2).Trim().Trim('"', '\'');
                    if (!string.IsNullOrWhiteSpace(depName))
                    {
                        if (currentListKey == "depend") metadata.RequiredDependencies.Add(depName);
                        else metadata.OptionalDependencies.Add(depName);
                    }
                }
                continue;
            }

            int colon = trimmed.IndexOf(':');
            if (colon <= 0)
            {
                currentListKey = null;
                continue;
            }

            string key = trimmed.Substring(0, colon).Trim().ToLowerInvariant();
            string val = trimmed.Substring(colon + 1).Trim().Trim('"', '\'');
            currentListKey = key;

            switch (key)
            {
                case "name":
                    if (!string.IsNullOrWhiteSpace(val)) metadata.DisplayName = val;
                    break;
                case "version":
                    if (!string.IsNullOrWhiteSpace(val)) metadata.Version = val;
                    break;
                case "description":
                    if (!string.IsNullOrWhiteSpace(val)) metadata.Description = val;
                    break;
                case "api-version":
                    if (!string.IsNullOrWhiteSpace(val)) metadata.ApiVersion = val;
                    break;
                case "depend" when val.StartsWith("[", StringComparison.Ordinal) && val.EndsWith("]", StringComparison.Ordinal):
                    foreach (string dep in val.Trim('[', ']').Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        metadata.RequiredDependencies.Add(dep.Trim().Trim('"', '\''));
                    }
                    break;
                case "softdepend" when val.StartsWith("[", StringComparison.Ordinal) && val.EndsWith("]", StringComparison.Ordinal):
                    foreach (string dep in val.Trim('[', ']').Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        metadata.OptionalDependencies.Add(dep.Trim().Trim('"', '\''));
                    }
                    break;
            }
        }
    }

    private static string? MatchTomlString(string toml, string key)
    {
        Match m = Regex.Match(toml, $@"^\s*{Regex.Escape(key)}\s*=\s*[""']([^""'\r\n]+)[""']", RegexOptions.Multiline | RegexOptions.CultureInvariant);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static void ReadIconFromEntry(ZipArchive archive, string? iconPath, JavaModMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(iconPath)) return;
        string normalized = iconPath.TrimStart('/').Replace('\\', '/');
        ZipArchiveEntry? iconEntry = archive.GetEntry(normalized)
            ?? archive.Entries.FirstOrDefault(e => string.Equals(e.FullName, normalized, StringComparison.OrdinalIgnoreCase));

        if (iconEntry == null || iconEntry.Length <= 0 || iconEntry.Length > MaxIconBytes)
        {
            return;
        }

        using var ms = new MemoryStream();
        using var entryStream = iconEntry.Open();
        entryStream.CopyTo(ms);
        metadata.IconEntryPath = iconEntry.FullName;
        metadata.IconBytes = ms.ToArray();
    }

    private static void TryFindFallbackIcon(ZipArchive archive, JavaModMetadata metadata)
    {
        ZipArchiveEntry? iconEntry = archive.GetEntry("icon.png")
            ?? archive.GetEntry("logo.png")
            ?? archive.Entries.FirstOrDefault(e => e.FullName.EndsWith("/icon.png", StringComparison.OrdinalIgnoreCase));
        if (iconEntry != null && iconEntry.Length > 0 && iconEntry.Length <= MaxIconBytes)
        {
            ReadIconFromEntry(archive, iconEntry.FullName, metadata);
        }
    }

    private static string CleanJarName(string fileName)
    {
        string stripped = Regex.Replace(fileName, @"\.(jar|disabled)$", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        stripped = Regex.Replace(stripped, @"\.jar$", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return string.IsNullOrWhiteSpace(stripped) ? fileName : stripped;
    }
}
