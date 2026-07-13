using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.Infrastructure.Services
{
    public class InstanceService : IInstanceService
    {
        private readonly ISettingsService _settingsService;
        private readonly string _registryPath;
        private readonly List<ServerInstance> _cache = new();

        public InstanceService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            _registryPath = Path.Combine(_settingsService.GetSettingsDirectory(), "instances.json");
            LoadRegistry();
        }

        private void LoadRegistry()
        {
            lock (_cache)
            {
                _cache.Clear();
                if (File.Exists(_registryPath))
                {
                    try
                    {
                        var json = File.ReadAllText(_registryPath);
                        var list = JsonSerializer.Deserialize<List<ServerInstance>>(json);
                        if (list != null)
                        {
                            _cache.AddRange(list);
                        }
                    }
                    catch
                    {
                        ReconstructRegistry();
                    }
                }
                else
                {
                    ReconstructRegistry();
                }
            }
        }

        private void SaveRegistry()
        {
            lock (_cache)
            {
                try
                {
                    var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_registryPath, json);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving instances registry: {ex.Message}");
                }
            }
        }

        private void ReconstructRegistry()
        {
            var instancesDir = _settingsService.GetInstancesDirectory();
            if (!Directory.Exists(instancesDir))
            {
                Directory.CreateDirectory(instancesDir);
                return;
            }

            _cache.Clear();
            foreach (var dir in Directory.GetDirectories(instancesDir))
            {
                var metaPath = Path.Combine(dir, "instance.json");
                if (File.Exists(metaPath))
                {
                    try
                    {
                        var json = File.ReadAllText(metaPath);
                        var instance = JsonSerializer.Deserialize<ServerInstance>(json);
                        if (instance != null)
                        {
                            _cache.Add(instance);
                        }
                    }
                    catch
                    {
                        // Skip corrupted local metadata
                    }
                }
            }
            SaveRegistry();
        }

        public Task<List<ServerInstance>> ListInstancesAsync()
        {
            lock (_cache)
            {
                // Ensure synchronization with disk state in case folders were manually removed
                var validInstances = new List<ServerInstance>();
                bool dirty = false;

                foreach (var inst in _cache)
                {
                    if (Directory.Exists(inst.Path))
                    {
                        validInstances.Add(inst);
                    }
                    else
                    {
                        dirty = true;
                    }
                }

                if (dirty)
                {
                    _cache.Clear();
                    _cache.AddRange(validInstances);
                    SaveRegistry();
                }

                return Task.FromResult(_cache.ToList());
            }
        }

        public Task<ServerInstance> CreateInstanceAsync(string name, EngineType engineType, string version)
        {
            var baseSlug = Slugify(name);
            if (string.IsNullOrEmpty(baseSlug))
            {
                baseSlug = "server";
            }

            var instancesRoot = _settingsService.GetInstancesDirectory();
            var slug = ResolveSlugAndDirectory(baseSlug, instancesRoot, out var targetDir);

            Directory.CreateDirectory(targetDir);

            var instance = new ServerInstance
            {
                Id = Guid.NewGuid(),
                Name = name,
                Slug = slug,
                Path = targetDir,
                EngineType = engineType,
                EngineVersion = version,
                CreatedAt = DateTime.UtcNow
            };

            WriteLocalMetadata(instance);

            lock (_cache)
            {
                _cache.Add(instance);
                SaveRegistry();
            }

            return Task.FromResult(instance);
        }

        public Task DeleteInstanceAsync(string slug)
        {
            lock (_cache)
            {
                var instance = _cache.FirstOrDefault(i => i.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
                if (instance != null)
                {
                    if (Directory.Exists(instance.Path))
                    {
                        Directory.Delete(instance.Path, true);
                    }
                    _cache.Remove(instance);
                    SaveRegistry();
                }
            }
            return Task.CompletedTask;
        }

        public Task<ServerInstance> RenameInstanceAsync(string slug, string newName)
        {
            lock (_cache)
            {
                var instance = _cache.FirstOrDefault(i => i.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
                if (instance == null)
                {
                    throw new KeyNotFoundException($"Instance with slug '{slug}' not found.");
                }

                var baseSlug = Slugify(newName);
                var instancesRoot = _settingsService.GetInstancesDirectory();
                var newSlug = ResolveSlugAndDirectory(baseSlug, instancesRoot, out var newDir);

                if (Directory.Exists(instance.Path))
                {
                    Directory.Move(instance.Path, newDir);
                }
                else
                {
                    Directory.CreateDirectory(newDir);
                }

                instance.Name = newName;
                instance.Slug = newSlug;
                instance.Path = newDir;

                WriteLocalMetadata(instance);
                SaveRegistry();

                return Task.FromResult(instance);
            }
        }

        public Task<ServerInstance> CloneInstanceAsync(string slug, string newName)
        {
            ServerInstance original;
            lock (_cache)
            {
                original = _cache.FirstOrDefault(i => i.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
                if (original == null)
                {
                    throw new KeyNotFoundException($"Instance with slug '{slug}' not found.");
                }
            }

            var baseSlug = Slugify(newName);
            var instancesRoot = _settingsService.GetInstancesDirectory();
            var newSlug = ResolveSlugAndDirectory(baseSlug, instancesRoot, out var newDir);

            if (Directory.Exists(original.Path))
            {
                CopyDirectory(original.Path, newDir);
            }
            else
            {
                Directory.CreateDirectory(newDir);
            }

            var clone = new ServerInstance
            {
                Id = Guid.NewGuid(),
                Name = newName,
                Slug = newSlug,
                Path = newDir,
                EngineType = original.EngineType,
                EngineVersion = original.EngineVersion,
                JvmArgs = original.JvmArgs,
                CreatedAt = DateTime.UtcNow
            };

            WriteLocalMetadata(clone);

            lock (_cache)
            {
                _cache.Add(clone);
                SaveRegistry();
            }

            return Task.FromResult(clone);
        }

        public Task ExportInstanceAsync(string slug, string targetZipPath)
        {
            ServerInstance instance;
            lock (_cache)
            {
                instance = _cache.FirstOrDefault(i => i.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
                if (instance == null)
                {
                    throw new KeyNotFoundException($"Instance with slug '{slug}' not found.");
                }
            }

            if (!Directory.Exists(instance.Path))
            {
                throw new DirectoryNotFoundException($"Instance directory not found: {instance.Path}");
            }

            if (File.Exists(targetZipPath))
            {
                File.Delete(targetZipPath);
            }

            ZipFile.CreateFromDirectory(instance.Path, targetZipPath);
            return Task.CompletedTask;
        }

        public Task<ServerInstance> ImportInstanceAsync(string sourceZipPath, string name)
        {
            if (!File.Exists(sourceZipPath))
            {
                throw new FileNotFoundException($"Source ZIP file not found: {sourceZipPath}");
            }

            var baseSlug = Slugify(name);
            var instancesRoot = _settingsService.GetInstancesDirectory();
            var slug = ResolveSlugAndDirectory(baseSlug, instancesRoot, out var targetDir);

            Directory.CreateDirectory(targetDir);
            var targetDirFullPath = Path.GetFullPath(targetDir) + Path.DirectorySeparatorChar;

            try
            {
                using (var archive = ZipFile.OpenRead(sourceZipPath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        var destinationPath = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));

                        // Zip Slip validation: check if the destination path escapes the target directory
                        if (!destinationPath.StartsWith(targetDirFullPath, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new SecurityException($"Zip Slip detected: Entry '{entry.FullName}' resolves to '{destinationPath}' which is outside target root '{targetDirFullPath}'");
                        }

                        if (entry.Name == "") // Directory entry
                        {
                            Directory.CreateDirectory(destinationPath);
                        }
                        else
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                            entry.ExtractToFile(destinationPath, overwrite: true);
                        }
                    }
                }
            }
            catch
            {
                // Absolute clean up on import failure to prevent partial/corrupted directory state on disk
                if (Directory.Exists(targetDir))
                {
                    Directory.Delete(targetDir, true);
                }
                throw;
            }

            // Look for existing local metadata or write a fresh one
            var metaPath = Path.Combine(targetDir, "instance.json");
            ServerInstance? importedInstance = null;

            if (File.Exists(metaPath))
            {
                try
                {
                    var json = File.ReadAllText(metaPath);
                    importedInstance = JsonSerializer.Deserialize<ServerInstance>(json);
                }
                catch
                {
                    // Ignore corrupted local metadata during import
                }
            }

            if (importedInstance == null)
            {
                importedInstance = new ServerInstance
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Slug = slug,
                    Path = targetDir,
                    EngineType = EngineType.VanillaJava, // Default fallback
                    EngineVersion = "latest",
                    CreatedAt = DateTime.UtcNow
                };
            }
            else
            {
                // Update slug, path, and name to match the imported instance metadata destination
                importedInstance.Id = Guid.NewGuid(); // Give it a fresh unique GUID
                importedInstance.Name = name;
                importedInstance.Slug = slug;
                importedInstance.Path = targetDir;
            }

            WriteLocalMetadata(importedInstance);

            lock (_cache)
            {
                _cache.Add(importedInstance);
                SaveRegistry();
            }

            return Task.FromResult(importedInstance);
        }

        private void WriteLocalMetadata(ServerInstance instance)
        {
            var metaPath = Path.Combine(instance.Path, "instance.json");
            var json = JsonSerializer.Serialize(instance, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(metaPath, json);
        }

        private string Slugify(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var lower = name.ToLowerInvariant();
            var clean = Regex.Replace(lower, @"[^a-z0-9\-_\s]", "");
            var spacesToHyphens = Regex.Replace(clean, @"[\s_]+", "-");
            return spacesToHyphens.Trim('-');
        }

        private string ResolveSlugAndDirectory(string baseSlug, string instancesRoot, out string targetDir)
        {
            var slug = baseSlug;
            targetDir = Path.Combine(instancesRoot, slug);
            int suffix = 1;
            while (Directory.Exists(targetDir))
            {
                slug = $"{baseSlug}-{suffix}";
                targetDir = Path.Combine(instancesRoot, slug);
                suffix++;
            }
            return slug;
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException($"Source directory does not exist: {sourceDir}");
            }

            Directory.CreateDirectory(destinationDir);

            foreach (var file in dir.GetFiles())
            {
                var targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            foreach (var subDir in dir.GetDirectories())
            {
                var targetSubDirPath = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, targetSubDirPath);
            }
        }
    }
}
