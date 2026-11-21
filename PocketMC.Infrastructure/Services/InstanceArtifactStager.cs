using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PocketMC.Core.Models;

namespace PocketMC.Infrastructure.Services
{
    public class InstanceArtifactStager
    {
        private readonly HttpClient _httpClient;

        public InstanceArtifactStager(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient();
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "PocketMC-Desktop/1.0");
            }
        }

        public async Task<InstanceUpdateStagedArtifacts> StageAsync(
            InstanceUpdatePlan plan,
            IProgress<double>? progress = null,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(plan);

            string stagingDir = Path.Combine(plan.InstancePath, $".pocketmc-staging-{plan.OperationId:N}");
            if (Directory.Exists(stagingDir))
            {
                try { Directory.Delete(stagingDir, true); } catch { }
            }

            Directory.CreateDirectory(stagingDir);

            string artifactPath = Path.Combine(stagingDir, plan.ServerArtifactFileName);

            progress?.Report(0.1);
            await DownloadServerArtifactAsync(plan.TargetEngineType, plan.TargetVersion, artifactPath, progress, ct);
            progress?.Report(1.0);

            if (!File.Exists(artifactPath) || new FileInfo(artifactPath).Length == 0)
            {
                throw new InvalidOperationException($"Staged artifact '{plan.ServerArtifactFileName}' was not downloaded or is 0 bytes.");
            }

            return new InstanceUpdateStagedArtifacts
            {
                StagingDirectory = stagingDir,
                ServerArtifactPath = artifactPath
            };
        }

        private async Task DownloadServerArtifactAsync(
            EngineType engineType,
            string targetVersion,
            string destinationPath,
            IProgress<double>? progress,
            CancellationToken ct)
        {
            switch (engineType)
            {
                case EngineType.VanillaJava:
                    await DownloadVanillaAsync(targetVersion, destinationPath, progress, ct);
                    break;
                case EngineType.Paper:
                    await DownloadPaperAsync(targetVersion, destinationPath, progress, ct);
                    break;
                case EngineType.Fabric:
                    await DownloadFabricAsync(targetVersion, destinationPath, progress, ct);
                    break;
                case EngineType.Forge:
                    await DownloadForgeAsync(targetVersion, destinationPath, progress, ct);
                    break;
                case EngineType.NeoForge:
                    await DownloadNeoForgeAsync(targetVersion, destinationPath, progress, ct);
                    break;
                case EngineType.PocketMine:
                    await DownloadPocketMineAsync(targetVersion, destinationPath, progress, ct);
                    break;
                case EngineType.Bedrock:
                    await DownloadBedrockAsync(targetVersion, destinationPath, progress, ct);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported engine type '{engineType}' for artifact updates.");
            }
        }

        private async Task DownloadVanillaAsync(string mcVersion, string dest, IProgress<double>? progress, CancellationToken ct)
        {
            string manifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest.json";
            var manifestStr = await _httpClient.GetStringAsync(manifestUrl, ct);
            using var manifestDoc = JsonDocument.Parse(manifestStr);
            var versions = manifestDoc.RootElement.GetProperty("versions");

            string? versionMetaUrl = null;
            foreach (var v in versions.EnumerateArray())
            {
                if (v.GetProperty("id").GetString() == mcVersion)
                {
                    versionMetaUrl = v.GetProperty("url").GetString();
                    break;
                }
            }

            if (string.IsNullOrEmpty(versionMetaUrl))
                throw new InvalidOperationException($"Version {mcVersion} not found in Mojang manifest.");

            var metaStr = await _httpClient.GetStringAsync(versionMetaUrl, ct);
            using var metaDoc = JsonDocument.Parse(metaStr);
            var serverObj = metaDoc.RootElement.GetProperty("downloads").GetProperty("server");
            var serverUrl = serverObj.GetProperty("url").GetString();
            string? sha1 = serverObj.TryGetProperty("sha1", out var sha1Prop) ? sha1Prop.GetString() : null;

            if (string.IsNullOrEmpty(serverUrl))
                throw new InvalidOperationException($"No server jar download found for Vanilla {mcVersion}.");

            await DownloadFileAsync(serverUrl, dest, progress, ct, sha1, "sha1");
        }

        private async Task DownloadPaperAsync(string mcVersion, string dest, IProgress<double>? progress, CancellationToken ct)
        {
            string buildsUrl = $"https://api.papermc.io/v2/projects/paper/versions/{mcVersion}/builds";
            var buildsJson = await _httpClient.GetStringAsync(buildsUrl, ct);
            using var doc = JsonDocument.Parse(buildsJson);
            var builds = doc.RootElement.GetProperty("builds");

            int maxBuild = 0;
            string? sha256 = null;
            foreach (var b in builds.EnumerateArray())
            {
                int bNum = b.GetProperty("build").GetInt32();
                if (bNum > maxBuild)
                {
                    maxBuild = bNum;
                    if (b.TryGetProperty("downloads", out var downloads) &&
                        downloads.TryGetProperty("application", out var appObj) &&
                        appObj.TryGetProperty("sha256", out var shaProp))
                    {
                        sha256 = shaProp.GetString();
                    }
                }
            }

            if (maxBuild == 0)
                throw new InvalidOperationException($"No builds found for Paper version {mcVersion}.");

            string downloadUrl = $"https://api.papermc.io/v2/projects/paper/versions/{mcVersion}/builds/{maxBuild}/downloads/paper-{mcVersion}-{maxBuild}.jar";
            await DownloadFileAsync(downloadUrl, dest, progress, ct, sha256, "sha256");
        }

        private async Task DownloadFabricAsync(string mcVersion, string dest, IProgress<double>? progress, CancellationToken ct)
        {
            string loaderVersion = "0.15.11";
            try
            {
                var loadersStr = await _httpClient.GetStringAsync("https://meta.fabricmc.net/v2/versions/loader", ct);
                using var loadersDoc = JsonDocument.Parse(loadersStr);
                foreach (var item in loadersDoc.RootElement.EnumerateArray())
                {
                    if (item.TryGetProperty("stable", out var stable) && stable.GetBoolean())
                    {
                        loaderVersion = item.GetProperty("version").GetString() ?? loaderVersion;
                        break;
                    }
                }
            }
            catch { }

            string installerVersion = "1.0.1";
            try
            {
                var installersStr = await _httpClient.GetStringAsync("https://meta.fabricmc.net/v2/versions/installer", ct);
                using var installersDoc = JsonDocument.Parse(installersStr);
                foreach (var item in installersDoc.RootElement.EnumerateArray())
                {
                    if (item.TryGetProperty("stable", out var stable) && stable.GetBoolean())
                    {
                        installerVersion = item.GetProperty("version").GetString() ?? installerVersion;
                        break;
                    }
                }
            }
            catch { }

            string url = $"https://meta.fabricmc.net/v2/versions/loader/{mcVersion}/{loaderVersion}/{installerVersion}/server/jar";
            await DownloadFileAsync(url, dest, progress, ct);
        }

        private async Task DownloadForgeAsync(string mcVersion, string dest, IProgress<double>? progress, CancellationToken ct)
        {
            string forgeVersion = "latest";
            try
            {
                var promosStr = await _httpClient.GetStringAsync("https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json", ct);
                using var promosDoc = JsonDocument.Parse(promosStr);
                if (promosDoc.RootElement.TryGetProperty("promos", out var promos))
                {
                    if (promos.TryGetProperty($"{mcVersion}-recommended", out var rec))
                    {
                        forgeVersion = rec.GetString() ?? forgeVersion;
                    }
                    else if (promos.TryGetProperty($"{mcVersion}-latest", out var lat))
                    {
                        forgeVersion = lat.GetString() ?? forgeVersion;
                    }
                }
            }
            catch { }

            if (forgeVersion == "latest")
            {
                try
                {
                    var prismStr = await _httpClient.GetStringAsync("https://meta.prismlauncher.org/v1/net.minecraftforge/index.json", ct);
                    using var prismDoc = JsonDocument.Parse(prismStr);
                    if (prismDoc.RootElement.TryGetProperty("versions", out var versionsArray))
                    {
                        foreach (var verNode in versionsArray.EnumerateArray())
                        {
                            if (verNode.TryGetProperty("requires", out var reqArray))
                            {
                                foreach (var req in reqArray.EnumerateArray())
                                {
                                    if (req.TryGetProperty("uid", out var uid) && uid.GetString() == "net.minecraft")
                                    {
                                        if (req.TryGetProperty("equals", out var eq) && eq.GetString() == mcVersion)
                                        {
                                            forgeVersion = verNode.GetProperty("version").GetString() ?? forgeVersion;
                                            break;
                                        }
                                    }
                                }
                            }
                            if (forgeVersion != "latest") break;
                        }
                    }
                }
                catch { }
            }

            if (forgeVersion == "latest")
            {
                throw new InvalidOperationException($"Could not find a valid Forge version for Minecraft {mcVersion}.");
            }

            string url = $"https://maven.minecraftforge.net/net/minecraftforge/forge/{mcVersion}-{forgeVersion}/forge-{mcVersion}-{forgeVersion}-installer.jar";
            await DownloadFileAsync(url, dest, progress, ct);
        }

        private async Task DownloadNeoForgeAsync(string mcVersion, string dest, IProgress<double>? progress, CancellationToken ct)
        {
            string? neoforgeVersion = null;
            try
            {
                var prismStr = await _httpClient.GetStringAsync("https://meta.prismlauncher.org/v1/net.neoforged/index.json", ct);
                using var prismDoc = JsonDocument.Parse(prismStr);
                if (prismDoc.RootElement.TryGetProperty("versions", out var versionsArray))
                {
                    foreach (var verNode in versionsArray.EnumerateArray())
                    {
                        if (verNode.TryGetProperty("requires", out var reqArray))
                        {
                            foreach (var req in reqArray.EnumerateArray())
                            {
                                if (req.TryGetProperty("uid", out var uid) && uid.GetString() == "net.minecraft")
                                {
                                    if (req.TryGetProperty("equals", out var eq) && eq.GetString() == mcVersion)
                                    {
                                        neoforgeVersion = verNode.GetProperty("version").GetString();
                                        break;
                                    }
                                }
                            }
                        }
                        if (neoforgeVersion != null) break;
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(neoforgeVersion))
            {
                throw new InvalidOperationException($"Could not find a valid NeoForge version for Minecraft {mcVersion}.");
            }

            string url = $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{neoforgeVersion}/neoforge-{neoforgeVersion}-installer.jar";
            await DownloadFileAsync(url, dest, progress, ct);
        }

        private async Task DownloadPocketMineAsync(string version, string dest, IProgress<double>? progress, CancellationToken ct)
        {
            string releaseUrl = $"https://api.github.com/repos/pmmp/PocketMine-MP/releases/tags/{version}";
            var json = await _httpClient.GetStringAsync(releaseUrl, ct);
            using var doc = JsonDocument.Parse(json);

            string? pharUrl = null;
            if (doc.RootElement.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString();
                    if (name != null && name.EndsWith(".phar", StringComparison.OrdinalIgnoreCase))
                    {
                        pharUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(pharUrl))
            {
                pharUrl = $"https://github.com/pmmp/PocketMine-MP/releases/download/{version}/PocketMine-MP.phar";
            }

            await DownloadFileAsync(pharUrl, dest, progress, ct);
        }

        private async Task DownloadBedrockAsync(string version, string dest, IProgress<double>? progress, CancellationToken ct)
        {
            string downloadUrl = $"https://www.minecraft.net/bedrockdedicatedserver/bin-linux/bedrock-server-{version}.zip";
            await DownloadFileAsync(downloadUrl, dest, progress, ct);
        }

        private async Task DownloadFileAsync(
            string url,
            string destination,
            IProgress<double>? progress,
            CancellationToken ct,
            string? expectedHash = null,
            string? hashType = null)
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            await using (var contentStream = await response.Content.ReadAsStreamAsync(ct))
            await using (var fileStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        progress?.Report(0.1 + (0.85 * totalRead / totalBytes));
                    }
                }
            }

            if (!string.IsNullOrEmpty(expectedHash))
            {
                using var fs = File.OpenRead(destination);
                byte[] hashBytes;
                if (string.Equals(hashType, "sha1", StringComparison.OrdinalIgnoreCase))
                {
                    hashBytes = await SHA1.Create().ComputeHashAsync(fs, ct);
                }
                else
                {
                    hashBytes = await SHA256.Create().ComputeHashAsync(fs, ct);
                }

                string actualHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
                if (!string.Equals(actualHash, expectedHash.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(destination); } catch { }
                    throw new CryptographicException($"Checksum mismatch for downloaded artifact. Expected {expectedHash}, got {actualHash}.");
                }
            }
        }
    }
}
