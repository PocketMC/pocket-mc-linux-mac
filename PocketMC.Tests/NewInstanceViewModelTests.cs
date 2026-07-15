using System;
using System.IO;
using System.Threading.Tasks;
using PocketMC.App.ViewModels;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using Xunit;

namespace PocketMC.Tests
{
    public class FakeJavaService : IJavaService
    {
        public bool ProvisionCalled { get; private set; }
        public Task<string> GetJavaExecutablePathAsync(string version) => Task.FromResult("java");
        public Task<bool> ValidateJavaRuntimeAsync(string executablePath, string expectedVersion) => Task.FromResult(true);
        public Task ProvisionJavaRuntimeAsync(string version)
        {
            ProvisionCalled = true;
            return Task.CompletedTask;
        }
    }

    public class FakePHPService : IPHPService
    {
        public bool ProvisionCalled { get; private set; }
        public Task<string> GetPHPExecutablePathAsync(string version) => Task.FromResult("php");
        public Task<bool> ValidatePHPRuntimeAsync(string executablePath, string expectedVersion) => Task.FromResult(true);
        public Task ProvisionPHPRuntimeAsync(string version)
        {
            ProvisionCalled = true;
            return Task.CompletedTask;
        }
    }

    public class NewInstanceViewModelTests
    {
        [Fact]
        public void NewInstanceViewModel_ValidationRules()
        {
            var instService = new FakeInstanceService();
            var javaService = new FakeJavaService();
            var phpService = new FakePHPService();

            var vm = new NewInstanceViewModel(instService, javaService, phpService);

            // 1. Initial State: Name empty
            vm.Name = "";
            Assert.True(vm.HasErrors);

            // 2. Name too short
            vm.Name = "ab";
            Assert.True(vm.HasErrors);

            // 3. Name with special characters
            vm.Name = "invalid name!";
            Assert.True(vm.HasErrors);

            // 4. Valid name
            vm.Name = "valid-name_123";
            Assert.False(vm.HasErrors);
        }

        [Fact]
        public async Task NewInstanceViewModel_VersionPopulatesBasedOnEngine()
        {
            var instService = new FakeInstanceService();
            var javaService = new FakeJavaService();
            var phpService = new FakePHPService();

            var vm = new NewInstanceViewModel(instService, javaService, phpService);

            // Default VanillaJava
            vm.SelectedEngine = EngineType.VanillaJava;
            await vm.LoadVersionsAsync();
            Assert.Contains("1.21", vm.Versions);

            // Switch to PocketMine
            vm.SelectedEngine = EngineType.PocketMine;
            await vm.LoadVersionsAsync();
            Assert.Contains("5.1.0", vm.Versions);

            // Switch to Bedrock
            vm.SelectedEngine = EngineType.Bedrock;
            await vm.LoadVersionsAsync();
            Assert.Contains("1.21.0", vm.Versions);
        }

        [Fact]
        public async Task NewInstanceViewModel_CreateCommandExecution()
        {
            var instService = new FakeInstanceService();
            var javaService = new FakeJavaService();
            var phpService = new FakePHPService();

            var vm = new NewInstanceViewModel(instService, javaService, phpService);

            vm.Name = "valid-server";
            vm.SelectedEngine = EngineType.VanillaJava;
            await vm.LoadVersionsAsync();

            // Cannot create without accepting EULA
            vm.AcceptEula = false;
            Assert.False(vm.CreateInstanceCommand.CanExecute(null));

            // Can create after accepting EULA
            vm.AcceptEula = true;
            Assert.True(vm.CreateInstanceCommand.CanExecute(null));

            // Run create
            await vm.CreateInstanceCommand.ExecuteAsync(null);

            // Check if Java service was provisioned
            Assert.True(javaService.ProvisionCalled);
        }
    }
}
