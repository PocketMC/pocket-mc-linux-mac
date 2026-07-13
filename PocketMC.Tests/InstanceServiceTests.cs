using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using PocketMC.Core.Models;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests
{
    public class InstanceServiceTests : IDisposable
    {
        private readonly string _testTempDir;
        private readonly SettingsService _settingsService;
        private readonly InstanceService _instanceService;

        public InstanceServiceTests()
        {
            _testTempDir = Path.Combine(Path.GetTempPath(), "PocketMCTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testTempDir);

            // Set up SettingsService pointing to this temp folder
            var settings = new Settings
            {
                CustomDataRoot = _testTempDir
            };

            _settingsService = new SettingsService(_testTempDir);
            // Manually set custom root to isolate tests
            _settingsService.Settings.CustomDataRoot = _testTempDir;
            _settingsService.Load(); // Re-creates subdirectory structures under testTempDir

            _instanceService = new InstanceService(_settingsService);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testTempDir))
                {
                    Directory.Delete(_testTempDir, true);
                }
            }
            catch
            {
                // Clean up ignored
            }
        }

        [Fact]
        [Trait("Category", "Unit")]
        public async Task CreateInstance_GeneratesSlugAndDirectories()
        {
            var name = "My Awesome Server!!";
            var instance = await _instanceService.CreateInstanceAsync(name, EngineType.VanillaJava, "1.20.1");

            Assert.NotNull(instance);
            Assert.Equal("my-awesome-server", instance.Slug);
            Assert.True(Directory.Exists(instance.Path));
            Assert.True(File.Exists(Path.Combine(instance.Path, "instance.json")));

            var list = await _instanceService.ListInstancesAsync();
            Assert.Single(list);
            Assert.Equal(instance.Id, list[0].Id);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public async Task CreateInstance_ResolvesSlugCollisionsByAppendingSuffix()
        {
            var name = "Test Server";
            var inst1 = await _instanceService.CreateInstanceAsync(name, EngineType.VanillaJava, "1.20.1");
            var inst2 = await _instanceService.CreateInstanceAsync(name, EngineType.VanillaJava, "1.20.1");

            Assert.Equal("test-server", inst1.Slug);
            Assert.Equal("test-server-1", inst2.Slug);
            Assert.True(Directory.Exists(inst1.Path));
            Assert.True(Directory.Exists(inst2.Path));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public async Task DeleteInstance_RemovesDirectoriesAndCentralizedRegistry()
        {
            var name = "Delete Me";
            var instance = await _instanceService.CreateInstanceAsync(name, EngineType.VanillaJava, "1.20.1");
            Assert.True(Directory.Exists(instance.Path));

            await _instanceService.DeleteInstanceAsync(instance.Slug);
            Assert.False(Directory.Exists(instance.Path));

            var list = await _instanceService.ListInstancesAsync();
            Assert.Empty(list);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public async Task RenameInstance_UpdatesSlugAndMovesDirectories()
        {
            var instance = await _instanceService.CreateInstanceAsync("Old Name", EngineType.VanillaJava, "1.20.1");
            var oldPath = instance.Path;
            Assert.True(Directory.Exists(oldPath));

            var updated = await _instanceService.RenameInstanceAsync(instance.Slug, "New Cool Name");
            Assert.Equal("new-cool-name", updated.Slug);
            Assert.False(Directory.Exists(oldPath));
            Assert.True(Directory.Exists(updated.Path));

            var list = await _instanceService.ListInstancesAsync();
            Assert.Single(list);
            Assert.Equal("new-cool-name", list[0].Slug);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public async Task CloneInstance_CopiesAllFilesAndIncrementsSlug()
        {
            var original = await _instanceService.CreateInstanceAsync("Clone Base", EngineType.VanillaJava, "1.20.1");
            // Write a dummy file to the original path
            var dummyFile = Path.Combine(original.Path, "world.txt");
            await File.WriteAllTextAsync(dummyFile, "world_data");

            var clone = await _instanceService.CloneInstanceAsync(original.Slug, "Clone Base");
            Assert.Equal("clone-base-1", clone.Slug);
            Assert.True(Directory.Exists(clone.Path));

            var cloneDummyFile = Path.Combine(clone.Path, "world.txt");
            Assert.True(File.Exists(cloneDummyFile));
            Assert.Equal("world_data", await File.ReadAllTextAsync(cloneDummyFile));

            var list = await _instanceService.ListInstancesAsync();
            Assert.Equal(2, list.Count);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public async Task ZipSlip_ThrowsSecurityExceptionAndCleansUp()
        {
            var zipPath = Path.Combine(_testTempDir, "malicious.zip");

            // Create a malicious zip file with a path traversal entry
            using (var fs = new FileStream(zipPath, FileMode.Create))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("../../../escaped_file.txt");
                using (var writer = new StreamWriter(entry.Open()))
                {
                    writer.Write("exploit payload");
                }
            }

            // Assert import fails due to Zip Slip protection
            var exception = await Assert.ThrowsAsync<SecurityException>(() =>
                _instanceService.ImportInstanceAsync(zipPath, "Malicious")
            );

            Assert.Contains("Zip Slip detected", exception.Message);

            // Assert the created directory was cleaned up and does not persist
            var instancesRoot = _settingsService.GetInstancesDirectory();
            var targetDir = Path.Combine(instancesRoot, "malicious");
            Assert.False(Directory.Exists(targetDir));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public async Task ExportAndImport_SucceedsCleanly()
        {
            var original = await _instanceService.CreateInstanceAsync("Export Import Test", EngineType.VanillaJava, "1.20.1");
            var dummyFile = Path.Combine(original.Path, "server.properties");
            await File.WriteAllTextAsync(dummyFile, "difficulty=hard");

            var zipPath = Path.Combine(_testTempDir, "export.zip");
            await _instanceService.ExportInstanceAsync(original.Slug, zipPath);
            Assert.True(File.Exists(zipPath));

            // Import into a new instance
            var imported = await _instanceService.ImportInstanceAsync(zipPath, "Imported Server");
            Assert.NotNull(imported);
            Assert.Equal("imported-server", imported.Slug);
            Assert.True(Directory.Exists(imported.Path));

            var importedFile = Path.Combine(imported.Path, "server.properties");
            Assert.True(File.Exists(importedFile));
            Assert.Equal("difficulty=hard", await File.ReadAllTextAsync(importedFile));
        }
    }
}
