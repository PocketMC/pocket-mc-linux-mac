using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PocketMC.Core.Models;

namespace PocketMC.Infrastructure.Services;

public sealed class BanSidecarService
{
    public const string FileName = "pocketmc-bans.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly ILogger<BanSidecarService> _logger;

    public BanSidecarService(ILogger<BanSidecarService>? logger = null)
    {
        _logger = logger ?? NullLogger<BanSidecarService>.Instance;
    }

    public async Task<List<BannedPlayerEntry>> GetBannedPlayersAsync(string instancePath)
    {
        List<BedrockBanSidecarEntry> entries = await ReadEntriesAsync(instancePath);
        return entries.Select(entry => new BannedPlayerEntry
        {
            Name = entry.Name,
            Reason = entry.Reason,
            Created = entry.BannedAt.ToUniversalTime().ToString("O"),
            Expires = "forever",
            IsSidecar = true
        }).ToList();
    }

    public async Task AddBanAsync(string instancePath, string name, string reason)
    {
        List<BedrockBanSidecarEntry> entries = await ReadEntriesAsync(instancePath);
        entries.RemoveAll(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase));
        entries.Add(new BedrockBanSidecarEntry
        {
            Name = name,
            Xuid = string.Empty,
            BannedAt = DateTime.UtcNow,
            Reason = reason,
            BannedBy = "console"
        });

        await WriteEntriesAsync(instancePath, entries);
    }

    public async Task RemoveBanAsync(string instancePath, string name)
    {
        List<BedrockBanSidecarEntry> entries = await ReadEntriesAsync(instancePath);
        int removed = entries.RemoveAll(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
        {
            await WriteEntriesAsync(instancePath, entries);
        }
    }

    private async Task<List<BedrockBanSidecarEntry>> ReadEntriesAsync(string instancePath)
    {
        string path = Path.Combine(instancePath, FileName);
        if (!File.Exists(path))
        {
            return new List<BedrockBanSidecarEntry>();
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            List<BedrockBanSidecarEntry>? entries = await JsonSerializer.DeserializeAsync<List<BedrockBanSidecarEntry>>(stream, JsonOptions);
            return entries ?? new List<BedrockBanSidecarEntry>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read Bedrock ban sidecar at {Path}.", path);
            return new List<BedrockBanSidecarEntry>();
        }
    }

    private async Task WriteEntriesAsync(string instancePath, List<BedrockBanSidecarEntry> entries)
    {
        string path = Path.Combine(instancePath, FileName);
        string tempPath = $"{path}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, entries, JsonOptions);
            }

            if (File.Exists(path))
            {
                File.Replace(tempPath, path, null);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }

    private sealed class BedrockBanSidecarEntry
    {
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("xuid")]
        public string Xuid { get; set; } = string.Empty;

        public DateTime BannedAt { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string BannedBy { get; set; } = "console";
    }
}
