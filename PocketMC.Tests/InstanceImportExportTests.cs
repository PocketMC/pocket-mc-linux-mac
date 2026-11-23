using System;
using System.IO;
using System.Threading.Tasks;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.App.ViewModels.Instances;
using Xunit;

namespace PocketMC.Tests
{
    public class InstanceImportExportTests
    {
        [Fact]
        public async Task InstanceExportViewModel_InitializesAndExecutesExport()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pocketmc-export-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var testZipPath = Path.Combine(tempDir, "export-output.zip");

            try
            {
                var instance = new ServerInstance
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Export Instance",
                    Slug = "test-export-instance",
                    Path = tempDir,
                    EngineType = EngineType.Paper,
                    EngineVersion = "1.20.4"
                };

                var fakeService = new FakeExportInstanceService();
                var vm = new InstanceExportViewModel(instance, fakeService);

                Assert.Equal("Test Export Instance", vm.InstanceName);
                Assert.True(vm.IncludeWorlds);
                Assert.False(vm.IsExporting);
                Assert.False(vm.IsComplete);

                vm.DestinationZipPath = testZipPath;
                await vm.ExportCommand.ExecuteAsync(null);

                Assert.True(vm.IsComplete);
                Assert.False(vm.HasError);
                Assert.True(fakeService.ExportCalled);
                Assert.Equal(instance.Slug, fakeService.ExportedSlug);
                Assert.Equal(testZipPath, fakeService.ExportedPath);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public async Task InstanceImportViewModel_ValidatesAndImportsInstance()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pocketmc-import-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var testZipPath = Path.Combine(tempDir, "sample-export.zip");
            File.WriteAllText(testZipPath, "dummy-zip-content");

            try
            {
                var fakeService = new FakeExportInstanceService();
                var vm = new InstanceImportViewModel(fakeService);

                Assert.False(vm.CanImport);

                vm.ZipPath = testZipPath;
                vm.RequestedName = "My Imported Server";
                Assert.True(vm.CanImport);

                await vm.ImportCommand.ExecuteAsync(null);

                Assert.True(vm.IsComplete);
                Assert.False(vm.HasError);
                Assert.True(fakeService.ImportCalled);
                Assert.Equal("My Imported Server", fakeService.ImportedName);
                Assert.Equal(testZipPath, fakeService.ImportedZipPath);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        private class FakeExportInstanceService : IInstanceService
        {
            public bool ExportCalled { get; private set; }
            public string? ExportedSlug { get; private set; }
            public string? ExportedPath { get; private set; }

            public bool ImportCalled { get; private set; }
            public string? ImportedName { get; private set; }
            public string? ImportedZipPath { get; private set; }

            public Task ExportInstanceAsync(string slug, string targetZipPath)
            {
                ExportCalled = true;
                ExportedSlug = slug;
                ExportedPath = targetZipPath;
                return Task.CompletedTask;
            }

            public Task<ServerInstance> CreateInstanceAsync(string name, EngineType engineType, string version) => throw new NotImplementedException();
            public Task DeleteInstanceAsync(string slug) => throw new NotImplementedException();
            public Task<ServerInstance> RenameInstanceAsync(string slug, string newName) => throw new NotImplementedException();
            public Task<ServerInstance> CloneInstanceAsync(string slug, string newName) => throw new NotImplementedException();
            public Task SaveInstanceAsync(ServerInstance instance) => Task.CompletedTask;
            public Task<ServerInstance?> GetInstanceAsync(string slug) => Task.FromResult<ServerInstance?>(null);
            public Task<ServerInstance> ImportInstanceAsync(string sourceZipPath, string name)
            {
                ImportCalled = true;
                ImportedZipPath = sourceZipPath;
                ImportedName = name;
                return Task.FromResult(new ServerInstance
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Slug = "my-imported-server",
                    Path = "/tmp/my-imported-server",
                    EngineType = EngineType.VanillaJava,
                    EngineVersion = "1.20.4"
                });
            }
            public Task<System.Collections.Generic.List<ServerInstance>> ListInstancesAsync() => Task.FromResult(new System.Collections.Generic.List<ServerInstance>());
        }
    }
}
