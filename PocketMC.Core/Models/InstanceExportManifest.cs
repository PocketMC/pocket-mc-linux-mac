using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PocketMC.Core.Models;

public sealed class InstanceExportManifest
{
    public const string CurrentExportVersion = "1.0";
    public const string ManifestFileName = "pocketmc-export.json";

    [JsonPropertyName("exportVersion")]
    public string ExportVersion { get; set; } = CurrentExportVersion;

    [JsonPropertyName("origin")]
    public InstanceExportOrigin Origin { get; set; } = new();

    [JsonPropertyName("serverMeta")]
    public InstanceExportServerMeta ServerMeta { get; set; } = new();

    [JsonPropertyName("software")]
    public InstanceExportSoftware Software { get; set; } = new();

    [JsonPropertyName("runtime")]
    public InstanceExportRuntime Runtime { get; set; } = new();

    [JsonPropertyName("addons")]
    public List<InstanceExportAddon> Addons { get; set; } = new();
}

public sealed class InstanceExportOrigin
{
    [JsonPropertyName("pocketMcVersion")]
    public string PocketMcVersion { get; set; } = "1.0.3";

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class InstanceExportServerMeta
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }
}

public sealed class InstanceExportSoftware
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("minecraftVersion")]
    public string MinecraftVersion { get; set; } = string.Empty;

    [JsonPropertyName("loaderVersion")]
    public string? LoaderVersion { get; set; }
}

public sealed class InstanceExportRuntime
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "Java";

    [JsonPropertyName("javaMajorVersion")]
    public int? JavaMajorVersion { get; set; }
}

public sealed class InstanceExportAddon
{
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("projectId")]
    public string ProjectId { get; set; } = string.Empty;

    [JsonPropertyName("versionId")]
    public string? VersionId { get; set; }

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;
}
