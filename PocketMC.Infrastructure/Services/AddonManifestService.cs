using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PocketMC.Core.Models;

namespace PocketMC.Infrastructure.Services
{
    public class AddonManifestEntry
    {
        public string Provider { get; set; } = "";
        public string ProjectId { get; set; } = "";
        public string VersionId { get; set; } = "";
        public string FileName { get; set; } = "";
        public DateTime InstalledAt { get; set; }
        public string? ProjectTitle { get; set; }
        public string? ProjectSlug { get; set; }
        public string? IconUrl { get; set; }
        public string? DisplayName { get; set; }
        public string? ClientSide { get; set; }
        public string? ServerSide { get; set; }
        public string? FileHash { get; set; }
        public string? FileHashType { get; set; }
        public string? MinecraftVersion { get; set; }
        public string? Loader { get; set; }
        public string? DownloadUrl { get; set; }
    }

    public class AddonManifest
    {
        public List<AddonManifestEntry> Entries { get; set; } = new();
    }

    public class AddonManifestService
    {
        private const string ManifestFileName = "addon_manifest.json";
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);
        private readonly ILogger<AddonManifestService> _logger;

        public AddonManifestService(ILogger<AddonManifestService>? logger = null)
        {
            _logger = logger ?? NullLogger<AddonManifestService>.Instance;
        }

        private SemaphoreSlim GetLock(string serverDir)
        {
            return _locks.GetOrAdd(serverDir, _ => new SemaphoreSlim(1, 1));
        }

        public async Task<AddonManifest> LoadManifestAsync(string serverDir)
        {
            string path = Path.Combine(serverDir, ManifestFileName);
            if (!File.Exists(path)) return new AddonManifest();

            try
            {
                string json = await File.ReadAllTextAsync(path);
                return JsonSerializer.Deserialize<AddonManifest>(json) ?? new AddonManifest();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load add-on manifest at {ManifestPath}; returning an empty manifest.", path);
                return new AddonManifest();
            }
        }

        public AddonManifest LoadManifest(string serverDir)
        {
            string path = Path.Combine(serverDir, ManifestFileName);
            if (!File.Exists(path)) return new AddonManifest();

            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<AddonManifest>(json) ?? new AddonManifest();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load add-on manifest at {ManifestPath}; returning an empty manifest.", path);
                return new AddonManifest();
            }
        }

        public async Task SaveManifestAsync(string serverDir, AddonManifest manifest)
        {
            string path = Path.Combine(serverDir, ManifestFileName);
            string json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            await FileUtils.AtomicWriteAllTextAsync(path, json);
        }

        public async Task RegisterInstallAsync(string serverDir, string provider, string projectId, string versionId, string fileName)
        {
            await RegisterInstallAsync(serverDir, provider, projectId, versionId, fileName, null, null, null);
        }

        public async Task RegisterInstallAsync(
            string serverDir,
            string provider,
            string projectId,
            string versionId,
            string fileName,
            string? projectTitle,
            string? iconUrl,
            string? displayName,
            string? clientSide = null,
            string? serverSide = null,
            string? fileHash = null,
            string? fileHashType = null,
            string? minecraftVersion = null,
            string? loader = null,
            string? downloadUrl = null,
            string? projectSlug = null)
        {
            var sem = GetLock(serverDir);
            await sem.WaitAsync();
            try
            {
                var manifest = await LoadManifestAsync(serverDir);
                string safeFileName = MarketplaceFileNameSanitizer.RequireSafeFileName(fileName);

                var existing = manifest.Entries.FirstOrDefault(e => e.ProjectId == projectId && e.Provider == provider);

                if (existing == null && (!string.IsNullOrWhiteSpace(projectTitle) || !string.IsNullOrWhiteSpace(projectSlug)))
                {
                    existing = manifest.Entries.FirstOrDefault(e =>
                        (!string.IsNullOrWhiteSpace(projectTitle) && e.ProjectTitle != null && e.ProjectTitle.Equals(projectTitle, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(projectSlug) && e.ProjectSlug != null && e.ProjectSlug.Equals(projectSlug, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(projectTitle) && e.DisplayName != null && e.DisplayName.Equals(projectTitle, StringComparison.OrdinalIgnoreCase)) ||
                        e.FileName.Equals(safeFileName, StringComparison.OrdinalIgnoreCase)
                    );
                }

                projectSlug ??= existing?.ProjectSlug;
                projectTitle ??= existing?.ProjectTitle;
                iconUrl ??= existing?.IconUrl;
                displayName ??= existing?.DisplayName;
                clientSide ??= existing?.ClientSide;
                serverSide ??= existing?.ServerSide;
                fileHash ??= existing?.FileHash;
                fileHashType ??= existing?.FileHashType;
                minecraftVersion ??= existing?.MinecraftVersion;
                loader ??= existing?.Loader;
                downloadUrl ??= existing?.DownloadUrl;

                manifest.Entries.RemoveAll(e =>
                    (e.ProjectId == projectId && e.Provider == provider) ||
                    e.FileName.Equals(safeFileName, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(projectTitle) && e.ProjectTitle != null && e.ProjectTitle.Equals(projectTitle, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(projectSlug) && e.ProjectSlug != null && e.ProjectSlug.Equals(projectSlug, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(projectTitle) && e.DisplayName != null && e.DisplayName.Equals(projectTitle, StringComparison.OrdinalIgnoreCase))
                );

                manifest.Entries.Add(new AddonManifestEntry
                {
                    Provider = provider,
                    ProjectId = projectId,
                    VersionId = versionId,
                    FileName = safeFileName,
                    InstalledAt = DateTime.UtcNow,
                    ProjectTitle = projectTitle,
                    ProjectSlug = projectSlug,
                    IconUrl = iconUrl,
                    DisplayName = displayName,
                    ClientSide = clientSide,
                    ServerSide = serverSide,
                    FileHash = fileHash,
                    FileHashType = fileHashType,
                    MinecraftVersion = minecraftVersion,
                    Loader = loader,
                    DownloadUrl = downloadUrl
                });

                await SaveManifestAsync(serverDir, manifest);
            }
            finally
            {
                sem.Release();
            }
        }

        public async Task UpdateManifestFileNameAsync(string serverDir, string oldFileName, string newFileName)
        {
            var sem = GetLock(serverDir);
            await sem.WaitAsync();
            try
            {
                var manifest = await LoadManifestAsync(serverDir);
                var entry = manifest.Entries.FirstOrDefault(e => e.FileName.Equals(oldFileName, StringComparison.OrdinalIgnoreCase));
                if (entry != null)
                {
                    entry.FileName = newFileName;
                    await SaveManifestAsync(serverDir, manifest);
                }
            }
            finally
            {
                sem.Release();
            }
        }

        public async Task UnregisterAsync(string serverDir, string provider, string projectId)
        {
            var sem = GetLock(serverDir);
            await sem.WaitAsync();
            try
            {
                var manifest = await LoadManifestAsync(serverDir);
                int count = manifest.Entries.RemoveAll(e => e.ProjectId == projectId && e.Provider == provider);
                if (count > 0)
                {
                    await SaveManifestAsync(serverDir, manifest);
                }
            }
            finally
            {
                sem.Release();
            }
        }

        public async Task UnregisterByFileNameAsync(string serverDir, string fileName)
        {
            var sem = GetLock(serverDir);
            await sem.WaitAsync();
            try
            {
                var manifest = await LoadManifestAsync(serverDir);
                int count = manifest.Entries.RemoveAll(e => e.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
                if (count > 0)
                {
                    await SaveManifestAsync(serverDir, manifest);
                }
            }
            finally
            {
                sem.Release();
            }
        }

        public async Task<bool> IsInstalledAsync(string serverDir, string provider, string projectId, EngineCompatibility compat, string? projectTitle = null, string? projectSlug = null)
        {
            var manifest = await LoadManifestAsync(serverDir);
            var entry = manifest.Entries.Find(e => e.ProjectId == projectId && e.Provider == provider);

            if (entry == null && (!string.IsNullOrWhiteSpace(projectTitle) || !string.IsNullOrWhiteSpace(projectSlug)))
            {
                entry = manifest.Entries.Find(e =>
                    (!string.IsNullOrWhiteSpace(projectTitle) && e.ProjectTitle != null && e.ProjectTitle.Equals(projectTitle, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(projectSlug) && e.ProjectSlug != null && e.ProjectSlug.Equals(projectSlug, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(projectTitle) && e.DisplayName != null && e.DisplayName.Equals(projectTitle, StringComparison.OrdinalIgnoreCase))
                );
            }

            if (entry == null) return false;

            string? filePath = ResolveAddonFilePath(serverDir, compat.PrimaryAddonSubDir, entry.FileName);

            if (filePath == null || !File.Exists(filePath))
            {
                await UnregisterAsync(serverDir, entry.Provider, entry.ProjectId);
                return false;
            }

            return true;
        }

        public async Task SyncManifestAsync(string serverDir, ModrinthService modrinth, EngineCompatibility compat)
        {
            var manifest = await LoadManifestAsync(serverDir);

            // 1. Cleanup stale entries
            var entriesToRemove = new List<AddonManifestEntry>();
            foreach (var entry in manifest.Entries)
            {
                string subDir = compat.PrimaryAddonSubDir;
                string? filePath = ResolveAddonFilePath(serverDir, subDir, entry.FileName);
                if (filePath == null || !File.Exists(filePath))
                {
                    entriesToRemove.Add(entry);
                }
            }

            // 2. Identify untracked files
            string targetDir = Path.Combine(serverDir, compat.PrimaryAddonSubDir);
            var untrackedFiles = new List<string>();

            if (Directory.Exists(targetDir))
            {
                string[] extensions = compat.Family switch
                {
                    EngineFamily.Bedrock => new[] { "*.mcpack", "*.mcaddon" },
                    EngineFamily.Pocketmine => new[] { "*.phar" },
                    _ => new[] { "*.jar" }
                };

                var files = new List<string>();
                foreach (var ext in extensions)
                {
                    files.AddRange(Directory.GetFiles(targetDir, ext));
                }

                untrackedFiles = files.Where(f => !manifest.Entries.Any(e => e.FileName == Path.GetFileName(f))).ToList();
            }

            var newEntries = new List<AddonManifestEntry>();

            if (untrackedFiles.Count > 0)
            {
                var hashToLocalPath = new Dictionary<string, string>();
                foreach (var file in untrackedFiles)
                {
                    try
                    {
                        string hash = await CalculateSha1Async(file);
                        hashToLocalPath[hash] = file;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Skipping unreadable add-on file {FilePath} while syncing manifest.", file);
                    }
                }

                if (hashToLocalPath.Count > 0)
                {
                    var modrinthResults = await modrinth.GetVersionsByHashesAsync(hashToLocalPath.Keys);
                    
                    var tasks = modrinthResults.Select(async kvp =>
                    {
                        var hash = kvp.Key;
                        var version = kvp.Value;
                        if (hashToLocalPath.TryGetValue(hash, out string? localPath))
                        {
                            var projectInfo = await modrinth.GetProjectInfoAsync(version.ProjectId).ConfigureAwait(false);
                            var file = version.Files.FirstOrDefault(f => f.Hashes.ContainsValue(hash)) ??
                                       version.Files.FirstOrDefault(f => f.IsPrimary) ??
                                       version.Files.FirstOrDefault();
                            
                            string? fileHash = null;
                            string? fileHashType = null;
                            if (file != null)
                            {
                                fileHash = hash;
                                fileHashType = file.Hashes.FirstOrDefault(h => h.Value.Equals(hash, StringComparison.OrdinalIgnoreCase)).Key ?? "sha1";
                            }

                            return new AddonManifestEntry
                            {
                                Provider = "Modrinth",
                                ProjectId = version.ProjectId,
                                VersionId = version.Id,
                                FileName = Path.GetFileName(localPath),
                                InstalledAt = DateTime.UtcNow,
                                ProjectTitle = projectInfo?.Title ?? version.Name,
                                DisplayName = version.Name,
                                IconUrl = projectInfo?.IconUrl,
                                ClientSide = projectInfo?.ClientSide,
                                ServerSide = projectInfo?.ServerSide,
                                FileHash = fileHash,
                                FileHashType = fileHashType,
                                MinecraftVersion = version.GameVersions.FirstOrDefault(),
                                Loader = version.Loaders.FirstOrDefault() ?? compat.LoaderName,
                                DownloadUrl = file?.Url
                            };
                        }
                        return null;
                    });

                    var resolvedEntries = await Task.WhenAll(tasks);
                    foreach (var entry in resolvedEntries)
                    {
                        if (entry != null) newEntries.Add(entry);
                    }
                }
            }

            // 3. Apply changes to manifest
            if (entriesToRemove.Count > 0 || newEntries.Count > 0)
            {
                var sem = GetLock(serverDir);
                await sem.WaitAsync();
                try
                {
                    var latestManifest = await LoadManifestAsync(serverDir);
                    bool modified = false;

                    foreach (var entry in entriesToRemove)
                    {
                        if (latestManifest.Entries.RemoveAll(e => e.FileName == entry.FileName && e.ProjectId == entry.ProjectId) > 0)
                        {
                            modified = true;
                        }
                    }

                    foreach (var entry in newEntries)
                    {
                        if (!latestManifest.Entries.Any(e => e.FileName == entry.FileName))
                        {
                            latestManifest.Entries.Add(entry);
                            modified = true;
                        }
                    }

                    if (modified)
                    {
                        await SaveManifestAsync(serverDir, latestManifest);
                    }
                }
                finally
                {
                    sem.Release();
                }
            }
        }

        private async Task<string> CalculateSha1Async(string filePath)
        {
            using var sha1 = SHA1.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            var hashBytes = await sha1.ComputeHashAsync(stream);
            var sb = new StringBuilder();
            foreach (var b in hashBytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static string? ResolveAddonFilePath(string serverDir, string subDir, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
            {
                return null;
            }

            string addonDir = Path.Combine(serverDir, subDir);
            return PathSafety.ValidateContainedPath(addonDir, fileName);
        }

        public async Task InstallAddonAsync(
            string serverDir,
            string subDir,
            ResolvedDependency dep,
            HttpClient httpClient,
            CancellationToken ct,
            IProgress<double>? progress = null)
        {
            string? filePath = ResolveAddonFilePath(serverDir, subDir, dep.FileName);
            if (filePath == null)
            {
                throw new InvalidDataException($"Add-on filename '{dep.FileName}' is unsafe.");
            }

            var dir = Path.GetDirectoryName(filePath);
            if (dir != null) Directory.CreateDirectory(dir);

            using var response = await httpClient.GetAsync(dep.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            long totalSize = response.Content.Headers.ContentLength ?? 0;
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var downloadStream = await response.Content.ReadAsStreamAsync(ct);

            byte[] buffer = new byte[81920];
            int bytesRead;
            long totalRead = 0;

            while ((bytesRead = await downloadStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                totalRead += bytesRead;
                if (totalSize > 0 && progress != null)
                {
                    progress.Report((double)totalRead / totalSize * 100.0);
                }
            }

            await RegisterInstallAsync(
                serverDir,
                dep.Provider,
                dep.ProjectId,
                dep.VersionId ?? "",
                dep.FileName,
                dep.ProjectTitle,
                dep.IconUrl,
                dep.VersionName,
                dep.ClientSide,
                dep.ServerSide,
                dep.Hash,
                dep.HashType,
                dep.MatchedMinecraftVersion,
                dep.SelectedLoader,
                dep.DownloadUrl,
                null);
        }
    }
}

