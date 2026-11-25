using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PocketMC.Core.Models;

public enum ModSideSupport
{
    Unknown,
    Both,
    ClientOnly,
    ServerOnly
}

public sealed class JavaModMetadata
{
    public string ModId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? Description { get; set; }
    public string LoaderType { get; set; } = "Unknown";
    public string? IconEntryPath { get; set; }
    public byte[]? IconBytes { get; set; }
    public bool IsCorrupt { get; set; }
    public string? RequiredMinecraftVersion { get; set; }
    public string? RequiredLoaderVersion { get; set; }
    public ModSideSupport SideSupport { get; set; } = ModSideSupport.Unknown;
    public string SideLabel { get; set; } = "Unknown";

    public bool IsClientOnly
    {
        get => SideSupport == ModSideSupport.ClientOnly;
        set
        {
            if (value)
                SideSupport = ModSideSupport.ClientOnly;
            else if (SideSupport == ModSideSupport.ClientOnly)
                SideSupport = ModSideSupport.Unknown;
        }
    }

    public bool IsPluginInModsFolder { get; set; }
    public bool HasPluginMetadata { get; set; }
    public string? ApiVersion { get; set; }
    public List<string> RequiredDependencies { get; set; } = new();
    public List<string> OptionalDependencies { get; set; } = new();
    public List<string> Dependencies => RequiredDependencies.Concat(OptionalDependencies).ToList();

    public void SanitizeDependencies()
    {
        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "minecraft", "forge", "neoforge", "java", "fabric", "fabricloader",
            "quilt_loader", "quilt-loader", "fml", "fabric-api"
        };

        if (!string.IsNullOrEmpty(ModId))
        {
            ignored.Add(ModId);
        }

        var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string dep in RequiredDependencies)
        {
            if (!ignored.Contains(dep) && !IsFabricApiModule(dep))
            {
                required.Add(dep);
            }
        }
        RequiredDependencies = required.ToList();

        var optional = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string dep in OptionalDependencies)
        {
            if (!ignored.Contains(dep) && !required.Contains(dep) && !IsFabricApiModule(dep))
            {
                optional.Add(dep);
            }
        }
        OptionalDependencies = optional.ToList();
    }

    private static bool IsFabricApiModule(string dependencyId)
        => Regex.IsMatch(dependencyId, @"^fabric-[a-z0-9-]+-v\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
