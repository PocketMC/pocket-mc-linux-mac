using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using PocketMC.Core.Models;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests;

public class RuntimeManagementParityTests
{
    [Fact]
    public async Task PHPService_ResolveDownloadSpecAndManageDirectory_SupportsPm4AndPm5Versions()
    {
        var pm4Linux = PHPService.ResolveDownloadSpec("8.0", isMacOs: false, Architecture.X64);
        Assert.Equal("pm4-php-8.0-latest", pm4Linux.Tag);
        Assert.Contains("PM4", pm4Linux.AssetPattern);
        Assert.EndsWith(".tar.gz", pm4Linux.FallbackDownloadUrl);

        var pm5Mac = PHPService.ResolveDownloadSpec("8.2", isMacOs: true, Architecture.Arm64);
        Assert.Equal("pm5-php-8.2-latest", pm5Mac.Tag);
        Assert.Contains("MacOS-arm64-PM5", pm5Mac.AssetPattern);

        var tempRoot = Path.Combine(Path.GetTempPath(), "pmc_php_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var settingsService = new TempSettingsService(tempRoot);
            string phpDir = Path.Combine(settingsService.GetSettingsDirectory(), "Runtimes", "php-8.2");
            Directory.CreateDirectory(Path.Combine(phpDir, "bin"));
            settingsService.Settings.DownloadedRuntimes["php"] = new()
            {
                ["8.2"] = phpDir
            };

            var phpService = new PHPService(settingsService);
            Assert.Equal(phpDir, phpService.GetPHPRuntimeDirectory("8.2"));

            await phpService.DeletePHPRuntimeAsync("8.2");
            Assert.False(Directory.Exists(phpDir));
            Assert.Null(phpService.GetPHPRuntimeDirectory("8.2"));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    [Fact]
    public async Task JavaManagementViewModel_PopulatesJavaAndPhpRuntimesAndDownloadsMissing()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "pmc_runtimevm_test_" + Guid.NewGuid().ToString("N"));
        string customDir = Path.Combine(tempRoot, "Runtimes", "custom-jdk-21");
        Directory.CreateDirectory(Path.Combine(customDir, "bin"));
        await File.WriteAllBytesAsync(Path.Combine(customDir, "bin", "java"), new byte[1024 * 128]);

        try
        {
            var settingsService = new TempSettingsService(tempRoot);
            var javaService = new StubJavaServiceForVm(installedVersion: "21", javaDir: customDir);
            var phpService = new StubPhpServiceForVm(installedVersion: "8.2", phpDir: customDir);

            var vm = new PocketMC.App.ViewModels.JavaManagementViewModel(javaService, settingsService, phpService);
            await vm.RefreshAsync();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Contains(vm.Runtimes, r => r.Version == "21" && r.BadgeText == "READY");
            Assert.Contains(vm.Runtimes, r => r.Version == "8" && r.BadgeText == "MISSING");
            Assert.Contains(vm.Runtimes, r => r.IsCustom && r.BadgeText == "CUSTOM" && r.DetailText.Contains("MB"));

            Assert.Equal(4, vm.PhpRuntimes.Count);
            Assert.Contains(vm.PhpRuntimes, p => p.Version == "8.2" && p.BadgeText == "READY");
            Assert.Contains(vm.PhpRuntimes, p => p.Version == "8.0" && p.BadgeText == "MISSING");
            Assert.Contains("PHP runtimes installed", vm.GlobalStatus);

            await vm.DownloadMissingCommand.ExecuteAsync(null);
            Assert.Contains("8", javaService.ProvisionedVersions);
            Assert.Contains("8.0", phpService.ProvisionedVersions);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    private sealed class StubJavaServiceForVm : PocketMC.Core.Services.IJavaService
    {
        private readonly string _installedVersion;
        private readonly string _dir;
        public System.Collections.Generic.List<string> ProvisionedVersions { get; } = new();

        public StubJavaServiceForVm(string installedVersion, string javaDir)
        {
            _installedVersion = installedVersion;
            _dir = javaDir;
        }

        public Task<string> GetJavaExecutablePathAsync(string version) => Task.FromResult(Path.Combine(_dir, "bin", "java"));
        public Task<bool> ValidateJavaRuntimeAsync(string executablePath, string expectedVersion) => Task.FromResult(expectedVersion == _installedVersion);
        public Task ProvisionJavaRuntimeAsync(string version, IProgress<double>? progress = null)
        {
            ProvisionedVersions.Add(version);
            progress?.Report(1.0);
            return Task.CompletedTask;
        }
        public Task<bool> IsJavaRuntimeInstalledAsync(string version) => Task.FromResult(version == _installedVersion);
        public Task DeleteJavaRuntimeAsync(string version) => Task.CompletedTask;
        public Task RegisterCustomJavaRuntimeAsync(string version, string path) => Task.CompletedTask;
        public string? GetJavaRuntimeDirectory(string version) => version == _installedVersion ? _dir : null;
    }

    private sealed class StubPhpServiceForVm : PocketMC.Core.Services.IPHPService
    {
        private readonly string _installedVersion;
        private readonly string _dir;
        public System.Collections.Generic.List<string> ProvisionedVersions { get; } = new();

        public StubPhpServiceForVm(string installedVersion, string phpDir)
        {
            _installedVersion = installedVersion;
            _dir = phpDir;
        }

        public Task<string> GetPHPExecutablePathAsync(string version) => Task.FromResult(Path.Combine(_dir, "bin", "php"));
        public Task<bool> ValidatePHPRuntimeAsync(string executablePath, string expectedVersion) => Task.FromResult(versionMatch(expectedVersion));
        private bool versionMatch(string expectedVersion) => expectedVersion == _installedVersion;
        public Task ProvisionPHPRuntimeAsync(string version, IProgress<double>? progress = null)
        {
            ProvisionedVersions.Add(version);
            progress?.Report(1.0);
            return Task.CompletedTask;
        }
        public Task<bool> IsPHPRuntimeInstalledAsync(string version) => Task.FromResult(version == _installedVersion);
        public Task DeletePHPRuntimeAsync(string version) => Task.CompletedTask;
        public string? GetPHPRuntimeDirectory(string version) => version == _installedVersion ? _dir : null;
    }

    private sealed class TempSettingsService : PocketMC.Core.Services.ISettingsService
    {
        private readonly string _root;

        public TempSettingsService(string root)
        {
            _root = root;
            Settings = new Settings { CustomDataRoot = root };
        }

        public Settings Settings { get; }
        public string GetSettingsDirectory() => _root;
        public string GetDataRoot() => _root;
        public string GetDefaultDataRoot() => _root;
        public void SetDataRoot(string path) => Settings.CustomDataRoot = path;
        public string GetInstancesDirectory() => Path.Combine(_root, "Instances");
        public string GetBackupsDirectory() => Path.Combine(_root, "Backups");
        public string GetDownloadsDirectory() => Path.Combine(_root, "Downloads");
        public string GetCacheDirectory() => Path.Combine(_root, "Cache");
        public string GetLogsDirectory() => Path.Combine(_root, "Logs");
        public void Load() { }
        public void Save() { }
    }
}
