using System;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using PocketMC.App.Services;
using PocketMC.Core.Models;
using Xunit;

namespace PocketMC.Tests;

public class AccentColorServiceTests
{
    [Theory]
    [InlineData("#008B00", 0, 139, 0, "#008B00")]
    [InlineData("008B00", 0, 139, 0, "#008B00")]
    [InlineData("#FF008B00", 0, 139, 0, "#008B00")]
    [InlineData("#3498DB", 52, 152, 219, "#3498DB")]
    public void TryParseHexColor_ValidHex_ParsesCorrectly(string input, byte expectedR, byte expectedG, byte expectedB, string expectedNormalized)
    {
        bool success = AccentColorService.TryParseHexColor(input, out Color color, out string normalized);

        Assert.True(success);
        Assert.Equal(expectedR, color.R);
        Assert.Equal(expectedG, color.G);
        Assert.Equal(expectedB, color.B);
        Assert.Equal(expectedNormalized, normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("not-a-color")]
    [InlineData("#12")]
    [InlineData("#12345")]
    [InlineData("#GG0000")]
    public void TryParseHexColor_InvalidHex_ReturnsFalse(string? input)
    {
        bool success = AccentColorService.TryParseHexColor(input, out _, out _);
        Assert.False(success);
    }

    [Fact]
    public void LightenAndDarken_ModifiesColorChannelsCorrectly()
    {
        Color baseColor = Color.FromRgb(100, 100, 100);
        Color light = AccentColorService.Lighten(baseColor, 0.5f);
        Color dark = AccentColorService.Darken(baseColor, 0.5f);

        Assert.True(light.R > baseColor.R);
        Assert.True(light.G > baseColor.G);
        Assert.True(light.B > baseColor.B);

        Assert.True(dark.R < baseColor.R);
        Assert.True(dark.G < baseColor.G);
        Assert.True(dark.B < baseColor.B);
    }

    [Fact]
    public void IsCustomMode_EvaluatesCorrectly()
    {
        Assert.True(AccentColorService.IsCustomMode("Custom"));
        Assert.True(AccentColorService.IsCustomMode("custom"));
        Assert.False(AccentColorService.IsCustomMode("Automatic"));
        Assert.False(AccentColorService.IsCustomMode(null));
    }

    [Fact]
    public void ApplyAccentToResources_UpdatesThemeDictionariesAndContrastingTextBrush()
    {
        var darkDict = new ResourceDictionary();
        var lightDict = new ResourceDictionary();
        var rootDict = new ResourceDictionary();
        rootDict.ThemeDictionaries[ThemeVariant.Dark] = darkDict;
        rootDict.ThemeDictionaries[ThemeVariant.Light] = lightDict;

        Color brightGold = Color.FromRgb(255, 215, 0);
        AccentColorService.ApplyAccentToResources(rootDict, brightGold);

        var darkFill = Assert.IsType<SolidColorBrush>(darkDict["AccentFillColorDefaultBrush"]);
        var lightFill = Assert.IsType<SolidColorBrush>(lightDict["AccentFillColorDefaultBrush"]);
        var rootContrast = Assert.IsType<SolidColorBrush>(rootDict["TextOnAccentFillColorPrimaryBrush"]);
        var darkContrast = Assert.IsType<SolidColorBrush>(darkDict["TextOnAccentFillColorPrimaryBrush"]);

        Assert.Equal(brightGold, darkFill.Color);
        Assert.Equal(brightGold, lightFill.Color);
        Assert.Equal(Colors.Black, rootContrast.Color);
        Assert.Equal(Colors.Black, darkContrast.Color);

        Color deepBlue = Color.FromRgb(0, 102, 214);
        AccentColorService.ApplyAccentToResources(rootDict, deepBlue);

        var whiteContrast = Assert.IsType<SolidColorBrush>(darkDict["TextOnAccentFillColorPrimaryBrush"]);
        Assert.Equal(Colors.White, whiteContrast.Color);
    }
}
