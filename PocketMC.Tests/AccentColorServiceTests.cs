using System;
using Avalonia.Media;
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
}
