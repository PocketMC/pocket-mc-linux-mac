using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PocketMC.Core.Models;

namespace PocketMC.Infrastructure.Services;

public sealed class ModpackParser
{
    private readonly ILogger<ModpackParser> _logger;

    public ModpackParser(ILogger<ModpackParser>? logger = null)
    {
        _logger = logger ?? NullLogger<ModpackParser>.Instance;
    }

    public async Task<ModpackImportResult> ParseZipAsync(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);

        // 1. Modrinth pack
        var modrinthIndex = archive.GetEntry("modrinth.index.json");
        if (modrinthIndex != null)
        {
            return await ParseModrinthPackAsync(modrinthIndex);
        }

        // 2. CurseForge pack
        var curseManifest = archive.GetEntry("manifest.json");
        if (curseManifest != null)
        {
            return await ParseCurseForgePackAsync(curseManifest);
        }

        throw new InvalidDataException("Unsupported modpack format. Neither modrinth.index.json nor manifest.json was found.");
    }

    private async Task<ModpackImportResult> ParseModrinthPackAsync(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        var index = await JsonNode.ParseAsync(stream);

        var result = new ModpackImportResult
        {
            Name = index?["name"]?.ToString() ?? "Imported Modpack",
            MinecraftVersion = index?["dependencies"]?["minecraft"]?.ToString() ?? "1.20.1"
        };

        if (index?["dependencies"]?["fabric-loader"] != null)
        {
            result.Loader = "Fabric";
            result.LoaderVersion = index?["dependencies"]?["fabric-loader"]?.ToString() ?? string.Empty;
        }
        else if (index?["dependencies"]?["forge"] != null)
        {
            result.Loader = "Forge";
            result.LoaderVersion = index?["dependencies"]?["forge"]?.ToString() ?? string.Empty;
        }
        else if (index?["dependencies"]?["neoforge"] != null)
        {
            result.Loader = "NeoForge";
            result.LoaderVersion = index?["dependencies"]?["neoforge"]?.ToString() ?? string.Empty;
        }
        else if (index?["dependencies"]?["quilt-loader"] != null)
        {
            result.Loader = "Quilt";
            result.LoaderVersion = index?["dependencies"]?["quilt-loader"]?.ToString() ?? string.Empty;
        }

        if (string.IsNullOrEmpty(result.Loader) ||
           (result.Loader != "Fabric" && result.Loader != "Forge" && result.Loader != "NeoForge"))
        {
            throw new InvalidOperationException($"Unsupported Modpack Loader: {result.Loader ?? "Unknown"}. Only Fabric, Forge, and NeoForge are supported.");
        }

        var files = index?["files"]?.AsArray();
        int totalFiles = 0;
        int skippedFiles = 0;

        if (files != null)
        {
            foreach (var f in files)
            {
                if (f == null) continue;
                totalFiles++;

                var env = f["env"];
                if (env != null && env["server"]?.ToString() == "unsupported")
                {
                    skippedFiles++;
                    continue;
                }

                var downloadUrl = f["downloads"]?.AsArray()?.FirstOrDefault()?.ToString();
                var destPath = f["path"]?.ToString();
                var sha512 = f["hashes"]?["sha512"]?.ToString();
                var fileSizeVal = f["fileSize"]?.GetValue<long>() ?? 0;

                if (downloadUrl != null && destPath != null)
                {
                    if (destPath.Trim().EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("Skipping .zip file from Modrinth pack: {Path}", destPath);
                        continue;
                    }

                    if (destPath.Contains(".."))
                    {
                        _logger.LogWarning("Skipping mod file with suspicious path: {Path}", destPath);
                        continue;
                    }

                    string? provider = null;
                    string? projectId = null;
                    string? versionId = null;

                    if (downloadUrl.Contains("cdn.modrinth.com/data/"))
                    {
                        var parts = downloadUrl.Split('/');
                        if (parts.Length >= 7)
                        {
                            provider = "Modrinth";
                            projectId = parts[4];
                            versionId = parts[6];
                        }
                    }

                    result.Mods.Add(new ModpackFile
                    {
                        Name = Path.GetFileName(destPath),
                        DownloadUrl = downloadUrl,
                        DestinationPath = destPath,
                        Provider = provider,
                        ProjectId = projectId,
                        VersionId = versionId,
                        HashSha512 = sha512,
                        FileSize = fileSizeVal
                    });
                }
            }
        }

        if (totalFiles > 0)
        {
            double skipRatio = (double)skippedFiles / totalFiles;
            if (skipRatio > 0.50)
            {
                throw new InvalidOperationException($"This modpack appears to be primarily client-side ({skipRatio:P0} client-only mods) and is unsupported for server installation.");
            }
        }

        return result;
    }

    private async Task<ModpackImportResult> ParseCurseForgePackAsync(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        var manifest = await JsonNode.ParseAsync(stream);

        var result = new ModpackImportResult
        {
            Name = manifest?["name"]?.ToString() ?? "Imported CurseForge Pack",
            MinecraftVersion = manifest?["minecraft"]?["version"]?.ToString() ?? "1.20.1"
        };

        var loaders = manifest?["minecraft"]?["modLoaders"]?.AsArray();
        var primaryLoader = loaders?.FirstOrDefault();
        if (primaryLoader != null)
        {
            string loaderId = primaryLoader["id"]?.ToString() ?? string.Empty;
            if (loaderId.StartsWith("fabric-", StringComparison.OrdinalIgnoreCase))
            {
                result.Loader = "Fabric";
                result.LoaderVersion = loaderId[7..];
            }
            else if (loaderId.StartsWith("forge-", StringComparison.OrdinalIgnoreCase))
            {
                result.Loader = "Forge";
                result.LoaderVersion = loaderId[6..];
            }
            else if (loaderId.StartsWith("neoforge-", StringComparison.OrdinalIgnoreCase))
            {
                result.Loader = "NeoForge";
                result.LoaderVersion = loaderId[9..];
            }
        }

        if (string.IsNullOrEmpty(result.Loader) ||
           (result.Loader != "Fabric" && result.Loader != "Forge" && result.Loader != "NeoForge"))
        {
            throw new InvalidOperationException($"Unsupported Modpack Loader: {result.Loader ?? "Unknown"}. Only Fabric, Forge, and NeoForge are supported.");
        }

        var files = manifest?["files"]?.AsArray();
        if (files != null)
        {
            foreach (var f in files)
            {
                if (f == null) continue;

                string projectID = f["projectID"]?.ToString() ?? string.Empty;
                string fileID = f["fileID"]?.ToString() ?? string.Empty;

                if (!string.IsNullOrEmpty(projectID) && !string.IsNullOrEmpty(fileID))
                {
                    result.Mods.Add(new ModpackFile
                    {
                        Name = $"CF-{projectID}-{fileID}",
                        DestinationPath = $"mods/{projectID}-{fileID}.jar",
                        DownloadUrl = $"CURSEFORGE:{projectID}:{fileID}",
                        Provider = "CurseForge",
                        ProjectId = projectID,
                        VersionId = fileID
                    });
                }
            }
        }

        if (result.Mods.Count == 0 && files != null && files.Count > 0)
        {
            throw new InvalidOperationException("This modpack contains no downloadable server mods.");
        }

        return result;
    }
}
