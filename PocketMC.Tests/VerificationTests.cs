using System.Threading.Tasks;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests
{
    public class VerificationTests
    {
        private class FakeJavaService : IJavaService
        {
            public bool ShouldSucceed { get; set; } = true;
            public Task<string> GetJavaExecutablePathAsync(string version) => Task.FromResult("java");
            public Task<bool> ValidateJavaRuntimeAsync(string executablePath, string expectedVersion) => Task.FromResult(ShouldSucceed);
            public Task ProvisionJavaRuntimeAsync(string version, System.IProgress<double>? progress = null) => Task.CompletedTask;
        }

        private class FakePHPService : IPHPService
        {
            public bool ShouldSucceed { get; set; } = true;
            public Task<string> GetPHPExecutablePathAsync(string version) => Task.FromResult("php");
            public Task<bool> ValidatePHPRuntimeAsync(string executablePath, string expectedVersion) => Task.FromResult(ShouldSucceed);
            public Task ProvisionPHPRuntimeAsync(string version, System.IProgress<double>? progress = null) => Task.CompletedTask;
        }

        [Fact]
        public async Task TestPreLaunchVerificationSuccess()
        {
            var fakeJava = new FakeJavaService { ShouldSucceed = true };
            var fakePHP = new FakePHPService { ShouldSucceed = true };
            var verifier = new PreLaunchVerifier(fakeJava, fakePHP);

            Assert.True(await verifier.VerifyJavaAsync("21"));
            Assert.True(await verifier.VerifyPHPAsync("8.2"));
        }

        [Fact]
        public async Task TestPreLaunchVerificationFailure()
        {
            var fakeJava = new FakeJavaService { ShouldSucceed = false };
            var fakePHP = new FakePHPService { ShouldSucceed = false };
            var verifier = new PreLaunchVerifier(fakeJava, fakePHP);

            Assert.False(await verifier.VerifyJavaAsync("21"));
            Assert.False(await verifier.VerifyPHPAsync("8.2"));
        }
    }
}
