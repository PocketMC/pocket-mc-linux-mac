using System;
using PocketMC.Core.Models;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests;

public class SettingsBackupServiceTests
{
    [Fact]
    public void CreateBackupPackage_WithAppearance_PopulatesAppearanceData()
    {
        var settings = new Settings
        {
            WindowBackdrop = "BlurredWallpaper",
            AccentColorMode = "Custom",
            CustomAccentColor = "#008B00",
            WallpaperBlurRadius = 75.0,
            WallpaperTintOpacity = 0.80
        };

        var categories = new SettingsBackupCategories
        {
            IncludeAppearance = true,
            IncludeAppBehavior = false
        };

        var service = new SettingsBackupService();
        var package = service.CreateBackupPackage(settings, categories, "1.0.0");

        Assert.NotNull(package);
        Assert.NotNull(package.Appearance);
        Assert.Null(package.AppBehavior);
        Assert.Equal("BlurredWallpaper", package.Appearance.WindowBackdrop);
        Assert.Equal("#008B00", package.Appearance.CustomAccentColor);
    }

    [Fact]
    public void ExportAndImportJson_RoundtripsSuccessfullyAndAppliesToSettings()
    {
        var original = new Settings
        {
            WindowBackdrop = "AcrylicBlur",
            AccentColorMode = "Custom",
            CustomAccentColor = "#3498DB",
            WallpaperBlurRadius = 50.0,
            EnableDiscordRpc = true,
            CurseForgeApiKey = "test-curse-key-123"
        };

        var service = new SettingsBackupService();
        var package = service.CreateBackupPackage(original, new SettingsBackupCategories(), "1.2.0");

        string json = service.ExportToJson(package);
        Assert.False(string.IsNullOrWhiteSpace(json));

        var restoredPackage = service.ImportFromJson(json);
        Assert.NotNull(restoredPackage);
        Assert.Equal("1.2.0", restoredPackage.AppVersion);

        var targetSettings = new Settings();
        service.ApplyBackupPackage(targetSettings, restoredPackage);

        Assert.Equal("AcrylicBlur", targetSettings.WindowBackdrop);
        Assert.Equal("#3498DB", targetSettings.CustomAccentColor);
        Assert.Equal(50.0, targetSettings.WallpaperBlurRadius);
        Assert.Equal("test-curse-key-123", targetSettings.CurseForgeApiKey);
    }
}
