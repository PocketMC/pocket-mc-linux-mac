using System.Collections.Generic;

namespace PocketMC.Core.Models;

public class AddonUpdateCheckResult
{
    public bool IsUpdateAvailable { get; set; }
    public string? LatestVersionId { get; set; }
    public string? LatestVersionName { get; set; }
    public string? LatestFileName { get; set; }
    public string? LatestDownloadUrl { get; set; }
    public string? ProjectTitle { get; set; }
    public string? Hash { get; set; }
    public string? HashType { get; set; }
    public string ReleaseType { get; set; } = "release";
    public List<string> Warnings { get; set; } = new();
    public string? Error { get; set; }
}

public class ModpackImportResult
{
    public string Name { get; set; } = string.Empty;
    public string MinecraftVersion { get; set; } = string.Empty;
    public string Loader { get; set; } = string.Empty;
    public string LoaderVersion { get; set; } = string.Empty;
    public List<ModpackFile> Mods { get; set; } = new();
    public int TotalExtractedOverrides { get; set; }
}

public class ModpackFile
{
    public string Name { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? ProjectId { get; set; }
    public string? VersionId { get; set; }
    public string? HashSha512 { get; set; }
    public long FileSize { get; set; }
}
