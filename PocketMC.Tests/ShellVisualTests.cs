using System;
using PocketMC.App.Navigation;
using PocketMC.App.ViewModels;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.Tests;

public class ShellVisualTests
{
    private sealed class StubSettingsService : ISettingsService
    {
        public Settings Settings { get; } = new();
        public void Save() { }
        public void Load() { }
        public string GetDataRoot() => Path.GetTempPath();
        public void SetDataRoot(string path) { }
        public string GetDefaultDataRoot() => Path.GetTempPath();
        public string GetSettingsDirectory() => Path.GetTempPath();
        public string GetInstancesDirectory() => Path.GetTempPath();
        public string GetBackupsDirectory() => Path.GetTempPath();
        public string GetDownloadsDirectory() => Path.GetTempPath();
        public string GetCacheDirectory() => Path.GetTempPath();
        public string GetLogsDirectory() => Path.GetTempPath();
    }

    [Fact]
    public void TogglePane_UpdatesIsPaneOpenAndSidebarWidth()
    {
        var settings = new StubSettingsService();
        var nav = new AppNavigationService();
        var vm = new MainWindowViewModel(
            nav,
            new StubServiceProvider(),
            settings,
            null!);

        Assert.True(vm.IsPaneOpen);
        Assert.Equal(224, vm.SidebarWidth);

        vm.TogglePaneCommand.Execute(null);

        Assert.False(vm.IsPaneOpen);
        Assert.Equal(56, vm.SidebarWidth);
    }

    private sealed class StubServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
