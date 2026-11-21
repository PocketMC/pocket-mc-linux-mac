using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PocketMC.Core.Models;

namespace PocketMC.Infrastructure.Services;

public class GeyserProvisioningService
{
    private const string ProviderGeyserApi = "https://download.geysermc.org/v2/projects";
    private readonly DownloaderService _downloader;
    private readonly ModrinthService _modrinth;
    private readonly ILogger<GeyserProvisioningService> _logger;

    public GeyserProvisioningService(
        DownloaderService downloader,
        ModrinthService modrinth,
        ILogger<GeyserProvisioningService>? logger = null)
    {
        _downloader = downloader;
        _modrinth = modrinth;
        _logger = logger ?? NullLogger<GeyserProvisioningService>.Instance;
    }

    public async Task EnsureGeyserSetupAsync(
        string instancePath,
        string serverType,
        string minecraftVersion,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (serverType.StartsWith("Vanilla", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Geyser requires Paper, Fabric, or Forge. Vanilla is not supported.");
            }

            if (serverType.StartsWith("Bedrock", StringComparison.OrdinalIgnoreCase) ||
                serverType.StartsWith("Pocketmine", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Bedrock and PocketMine servers do not need Geyser.");
            }

            string loader = ResolveLoader(serverType, minecraftVersion);
            string targetDir = loader is "fabric" or "forge" or "neoforge" ? "mods" : "plugins";
            string dirPath = Path.Combine(instancePath, targetDir);
            Directory.CreateDirectory(dirPath);

            _logger.LogInformation("Provisioning Geyser: server={ServerType}, mc={McVer}, loader={Loader}, dir={Dir}",
                serverType, minecraftVersion, loader, targetDir);

            // 1. Geyser
            string geyserUrl = $"{ProviderGeyserApi}/geyser/versions/latest/builds/latest/downloads/{loader}";
            string geyserPath = Path.Combine(dirPath, $"Geyser-{char.ToUpper(loader[0]) + loader[1..]}.jar");
            await _downloader.DownloadFileAsync(geyserUrl, geyserPath, null, progress, cancellationToken);

            // 2. Fabric API if loader is Fabric
            if (loader == "fabric")
            {
                try
                {
                    var fabricApiVersion = await _modrinth.GetLatestVersionAsync("fabric-api", minecraftVersion, new[] { loader });
                    if (fabricApiVersion != null && !string.IsNullOrWhiteSpace(fabricApiVersion.DownloadUrl))
                    {
                        string fabricApiPath = Path.Combine(dirPath, "Fabric-API.jar");
                        await _downloader.DownloadFileAsync(fabricApiVersion.DownloadUrl, fabricApiPath, null, progress, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not automatically download Fabric API for {McVer}.", minecraftVersion);
                }
            }

            // 3. Floodgate
            bool isPaperLike = serverType.Equals("Paper", StringComparison.OrdinalIgnoreCase) ||
                               serverType.Equals("Spigot", StringComparison.OrdinalIgnoreCase);

            string floodgateUrl = isPaperLike
                ? $"{ProviderGeyserApi}/floodgate/versions/latest/builds/latest/downloads/spigot"
                : $"{ProviderGeyserApi}/floodgate/versions/latest/builds/latest/downloads/{loader}";

            string floodgatePath = Path.Combine(dirPath, $"Floodgate-{char.ToUpper(loader[0]) + loader[1..]}.jar");
            try
            {
                await _downloader.DownloadFileAsync(floodgateUrl, floodgatePath, null, progress, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Floodgate download failed; continuing with Geyser only.");
            }

            WriteConnectGuide(instancePath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to provision Geyser and Floodgate.");
            throw;
        }
    }

    public void PatchGeyserConfigPort(string instancePath, int targetPort)
    {
        string[] possiblePaths =
        {
            Path.Combine(instancePath, "plugins", "Geyser-Spigot", "config.yml"),
            Path.Combine(instancePath, "mods", "Geyser-Fabric", "config.yml"),
            Path.Combine(instancePath, "mods", "Geyser-Forge", "config.yml"),
            Path.Combine(instancePath, "mods", "Geyser-NeoForge", "config.yml"),
            Path.Combine(instancePath, "mods", "geyser", "config.yml"),
            Path.Combine(instancePath, "config", "Geyser-Fabric", "config.yml"),
            Path.Combine(instancePath, "config", "Geyser-Forge", "config.yml"),
            Path.Combine(instancePath, "config", "Geyser-NeoForge", "config.yml"),
            Path.Combine(instancePath, "config", "geyser", "config.yml")
        };

        foreach (var path in possiblePaths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                string[] lines = File.ReadAllLines(path);
                bool modified = false;
                bool inBedrockSection = false;

                for (int i = 0; i < lines.Length; i++)
                {
                    string trimmed = lines[i].TrimStart();
                    if (trimmed.StartsWith("bedrock:", StringComparison.OrdinalIgnoreCase))
                    {
                        inBedrockSection = true;
                    }
                    else if (inBedrockSection && lines[i].Length > 0 && !lines[i].StartsWith(" ") && !lines[i].StartsWith("\t"))
                    {
                        inBedrockSection = false;
                    }

                    if (inBedrockSection && trimmed.StartsWith("port:", StringComparison.OrdinalIgnoreCase))
                    {
                        string indentation = lines[i][..lines[i].IndexOf("port:", StringComparison.OrdinalIgnoreCase)];
                        string newPortLine = $"{indentation}port: {targetPort}";
                        if (lines[i] != newPortLine)
                        {
                            lines[i] = newPortLine;
                            modified = true;
                        }
                        break;
                    }
                }

                if (modified)
                {
                    File.WriteAllLines(path, lines);
                    _logger.LogInformation("Patched Geyser port to {Port} in {Path}", targetPort, path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to patch Geyser config port at {Path}", path);
            }
        }
    }

    private static string ResolveLoader(string serverType, string minecraftVersion)
    {
        return serverType.ToLowerInvariant() switch
        {
            "paper" or "spigot" => "spigot",
            "fabric" => "fabric",
            "forge" => IsNewerOrEqual(minecraftVersion, "1.20.5") ? "neoforge" : "forge",
            "neoforge" => "neoforge",
            _ => "spigot"
        };
    }

    private static bool IsNewerOrEqual(string version, string threshold)
    {
        try
        {
            var vParts = version.Split('.').Select(int.Parse).ToArray();
            var tParts = threshold.Split('.').Select(int.Parse).ToArray();

            int len = Math.Max(vParts.Length, tParts.Length);
            for (int i = 0; i < len; i++)
            {
                int v = i < vParts.Length ? vParts[i] : 0;
                int t = i < tParts.Length ? tParts[i] : 0;
                if (v > t) return true;
                if (v < t) return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void WriteConnectGuide(string instancePath)
    {
        try
        {
            string guidePath = Path.Combine(instancePath, "BEDROCK-CONNECT.txt");
            if (File.Exists(guidePath)) return;

            File.WriteAllText(guidePath,
                "=== Bedrock Cross-Play (Geyser + Floodgate) ===\n\n" +
                "Java players:   Connect with the Java IP on the Java port (e.g., 25565).\n" +
                "Bedrock players: Connect with the SAME IP on Geyser's Bedrock UDP port (default: 19132).\n\n" +
                "First run:\n" +
                "  1. Start the server once to allow Geyser to initialize its config.\n" +
                "  2. Bedrock clients can join using their Xbox/Microsoft account or linked Java account.\n\n" +
                "Tunneling (Playit.gg):\n" +
                "  - Java tunnel: Minecraft Java (TCP)\n" +
                "  - Geyser tunnel: Minecraft Bedrock (UDP 19132)\n");
        }
        catch
        {
        }
    }
}
