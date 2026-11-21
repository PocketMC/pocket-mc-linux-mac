using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json.Nodes;

namespace PocketMC.Infrastructure.Services;

public static class MarketplaceArchiveInspector
{
    public static IReadOnlyList<string> InspectServerCompatibilityWarnings(string filePath, bool isPlugin = false)
    {
        string extension = Path.GetExtension(filePath);
        if (!extension.Equals(".jar", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<string>();
        }

        try
        {
            using ZipArchive archive = ZipFile.OpenRead(filePath);
            var warnings = new List<string>();

            InspectFabricLikeManifest(archive, "fabric.mod.json", "Fabric", warnings);
            InspectFabricLikeManifest(archive, "quilt.mod.json", "Quilt", warnings);
            InspectForgeLikeManifest(archive, warnings);
            InspectPluginManifest(archive, isPlugin, warnings);

            return warnings;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public static bool IsClientOnlyAddon(string filePath)
    {
        string extension = Path.GetExtension(filePath);
        if (!extension.Equals(".jar", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            using ZipArchive archive = ZipFile.OpenRead(filePath);
            var warnings = new List<string>();

            InspectFabricLikeManifest(archive, "fabric.mod.json", "Fabric", warnings);
            InspectFabricLikeManifest(archive, "quilt.mod.json", "Quilt", warnings);
            InspectForgeLikeManifest(archive, warnings);

            return warnings.Any(w => w.Contains("client only", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static void InspectFabricLikeManifest(
        ZipArchive archive,
        string manifestName,
        string loaderName,
        List<string> warnings)
    {
        ZipArchiveEntry? entry = archive.GetEntry(manifestName);
        if (entry == null)
        {
            return;
        }

        try
        {
            using Stream stream = entry.Open();
            JsonNode? manifest = JsonNode.Parse(stream);
            string? environment = manifest?["environment"]?.ToString();
            if (environment != null && environment.Equals("client", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add("client only mod");
            }
        }
        catch
        {
        }
    }

    private static void InspectForgeLikeManifest(ZipArchive archive, List<string> warnings)
    {
        ZipArchiveEntry? modsTomlEntry = archive.GetEntry("META-INF/mods.toml") ?? archive.GetEntry("META-INF/neoforge.mods.toml");
        if (modsTomlEntry == null)
        {
            return;
        }

        try
        {
            using Stream stream = modsTomlEntry.Open();
            using StreamReader reader = new(stream);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("displayTest", StringComparison.OrdinalIgnoreCase) &&
                    trimmed.Contains("IGNORE_SERVER_VERSION", StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add("client only mod");
                    return;
                }
                if (trimmed.StartsWith("side", StringComparison.OrdinalIgnoreCase) &&
                    trimmed.Contains("CLIENT", StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add("client only mod");
                    return;
                }
            }
        }
        catch
        {
        }
    }

    private static void InspectPluginManifest(ZipArchive archive, bool isPlugin, List<string> warnings)
    {
        if (!isPlugin)
        {
            return;
        }

        bool hasPluginYml = archive.GetEntry("plugin.yml") != null || archive.GetEntry("paper-plugin.yml") != null;
        if (!hasPluginYml)
        {
            warnings.Add("Missing plugin.yml or paper-plugin.yml");
        }
    }
}
