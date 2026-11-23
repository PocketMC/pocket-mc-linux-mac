using System;
using System.IO;
using Avalonia.Media;
using PocketMC.App.Services;
using PocketMC.Core.Models;
using Xunit;

namespace PocketMC.Tests;

public class WallpaperBackdropServiceTests
{
    [Fact]
    public void CalculateTintBrush_WithValidOpacity_ReturnsExpectedAlpha()
    {
        double opacity = 0.72;
        var brush = WallpaperBackdropService.CreateTintBrush(opacity);

        Assert.NotNull(brush);
        byte expectedAlpha = (byte)Math.Clamp((int)Math.Round(opacity * 255.0), 0, 255);
        Assert.Equal(expectedAlpha, brush.Color.A);
        Assert.Equal(0x18, brush.Color.R);
        Assert.Equal(0x18, brush.Color.G);
        Assert.Equal(0x18, brush.Color.B);
    }

    [Fact]
    public void GenerateBlurredImageBytes_WithValidTestFile_ProducesNonEmptyPngBytes()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"pocketmc_wallpaper_test_{Guid.NewGuid():N}.png");
        try
        {
            using (var bmp = new SkiaSharp.SKBitmap(100, 100))
            {
                using (var canvas = new SkiaSharp.SKCanvas(bmp))
                {
                    canvas.Clear(SkiaSharp.SKColors.Green);
                }
                using var image = SkiaSharp.SKImage.FromBitmap(bmp);
                using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
                using var fs = File.OpenWrite(tempFile);
                data.SaveTo(fs);
            }

            byte[]? result = WallpaperBackdropService.GenerateBlurredImageBytes(tempFile, 10.0, 50);
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            // Verify PNG magic header: 0x89, 'P', 'N', 'G'
            Assert.Equal(0x89, result[0]);
            Assert.Equal(0x50, result[1]);
            Assert.Equal(0x4E, result[2]);
            Assert.Equal(0x47, result[3]);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
