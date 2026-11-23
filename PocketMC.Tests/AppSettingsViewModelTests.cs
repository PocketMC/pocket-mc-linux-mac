using System;
using PocketMC.App.Services;
using PocketMC.App.ViewModels;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests;

public class AppSettingsViewModelTests
{
    private class FakeSettingsService : ISettingsService
    {
        public Settings Settings { get; set; } = new();
        public bool SaveCalled { get; private set; }

        public void Load() { }
        public void Save() { SaveCalled = true; }
        public string GetSettingsDirectory() => "/tmp/pocketmc";
        public string GetDataRoot() => "/tmp/pocketmc/data";
        public string GetDefaultDataRoot() => "/tmp/pocketmc/data";
        public void SetDataRoot(string path) { Settings.CustomDataRoot = path; }
        public string GetInstancesDirectory() => "/tmp/pocketmc/data/instances";
        public string GetBackupsDirectory() => "/tmp/pocketmc/data/backups";
        public string GetDownloadsDirectory() => "/tmp/pocketmc/downloads";
        public string GetCacheDirectory() => "/tmp/pocketmc/cache";
        public string GetLogsDirectory() => "/tmp/pocketmc/logs";
    }

    [Fact]
    public void Constructor_InitializesPropertiesFromSettings()
    {
        var settingsService = new FakeSettingsService();
        settingsService.Settings.AccentColorMode = "Custom";
        settingsService.Settings.CustomAccentColor = "#008B00";
        settingsService.Settings.KeepComputerAwakeWhileServersRunning = true;
        settingsService.Settings.WallpaperBlurRadius = 60.0;

        var accentService = new AccentColorService(settingsService);
        var wallpaperService = new WallpaperBackdropService(settingsService);
        var backupService = new SettingsBackupService();

        var vm = new AppSettingsViewModel(settingsService, accentService, wallpaperService, backupService);

        Assert.Equal("Custom", vm.AccentColorMode);
        Assert.Equal("#008B00", vm.CustomAccentColor);
        Assert.True(vm.KeepComputerAwakeWhileServersRunning);
        Assert.Equal(60.0, vm.WallpaperBlurRadius);
    }

    [Fact]
    public void SaveSettings_PersistsChangesToSettingsService()
    {
        var settingsService = new FakeSettingsService();
        var accentService = new AccentColorService(settingsService);
        var wallpaperService = new WallpaperBackdropService(settingsService);
        var backupService = new SettingsBackupService();

        var vm = new AppSettingsViewModel(settingsService, accentService, wallpaperService, backupService);

        vm.CustomAccentColor = "#E74C3C";
        vm.WallpaperBlurRadius = 45.0;
        vm.StartMinimizedToTray = true;
        vm.SaveSettingsCommand.Execute(null);

        Assert.True(settingsService.SaveCalled);
        Assert.Equal("#E74C3C", settingsService.Settings.CustomAccentColor);
        Assert.Equal(45.0, settingsService.Settings.WallpaperBlurRadius);
        Assert.True(settingsService.Settings.StartMinimizedToTray);
    }

    [Fact]
    public void ApplyPresetColor_UpdatesAccentAndSaves()
    {
        var settingsService = new FakeSettingsService();
        var accentService = new AccentColorService(settingsService);
        var wallpaperService = new WallpaperBackdropService(settingsService);
        var backupService = new SettingsBackupService();

        var vm = new AppSettingsViewModel(settingsService, accentService, wallpaperService, backupService);

        vm.ApplyPresetColorCommand.Execute("#3498DB");

        Assert.Equal("#3498DB", vm.CustomAccentColor);
        Assert.Equal("#3498DB", settingsService.Settings.CustomAccentColor);
        Assert.True(settingsService.SaveCalled);
    }
}
