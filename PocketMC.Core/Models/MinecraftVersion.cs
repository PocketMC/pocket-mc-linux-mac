using System;
using System.Collections.Generic;

namespace PocketMC.Core.Models;

public sealed class MinecraftVersion
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "release";
    public DateTime ReleaseTime { get; set; } = DateTime.MinValue;
    public string? Url { get; set; }
    public List<string> LoaderVersions { get; set; } = new();
}
