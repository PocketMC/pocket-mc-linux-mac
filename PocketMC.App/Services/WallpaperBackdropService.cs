using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PocketMC.Core.Services;
using SkiaSharp;

namespace PocketMC.App.Services;

public sealed class WallpaperBackdropService : IDisposable
{
    private const string DefaultWallpaperAssetUri = "avares://PocketMC.App/Assets/default_wallpaper.png";
    private readonly ISettingsService? _settingsService;

    private Bitmap? _cachedBlurredBitmap;
    private string? _cachedWallpaperPath;
    private double _cachedBlurRadius = -1;

    public WallpaperBackdropService(ISettingsService? settingsService = null)
    {
        _settingsService = settingsService;
    }

    public static SolidColorBrush CreateTintBrush(double opacity)
    {
        byte alpha = (byte)Math.Clamp((int)Math.Round(opacity * 255.0), 0, 255);
        return new SolidColorBrush(Color.FromArgb(alpha, 0x18, 0x18, 0x18));
    }

    public void Apply(Image? wallpaperImage, Border? tintOverlay, double blurRadius = 80.0, double tintOpacity = 0.72, string? customPath = null)
    {
        if (tintOverlay != null)
        {
            tintOverlay.Background = CreateTintBrush(tintOpacity);
            tintOverlay.IsVisible = true;
        }

        if (wallpaperImage == null)
        {
            return;
        }

        string effectivePath = customPath ?? _settingsService?.Settings?.CustomBackgroundImagePath ?? string.Empty;

        try
        {
            if (_cachedBlurredBitmap == null ||
                _cachedWallpaperPath != effectivePath ||
                Math.Abs(_cachedBlurRadius - blurRadius) > 0.5)
            {
                _cachedBlurredBitmap?.Dispose();
                _cachedBlurredBitmap = GenerateBlurredBitmap(effectivePath, blurRadius);
                _cachedWallpaperPath = effectivePath;
                _cachedBlurRadius = blurRadius;
            }

            if (_cachedBlurredBitmap != null)
            {
                wallpaperImage.Source = _cachedBlurredBitmap;
                wallpaperImage.Stretch = Stretch.UniformToFill;
                wallpaperImage.IsVisible = true;
            }
            else
            {
                wallpaperImage.IsVisible = false;
            }
        }
        catch
        {
            wallpaperImage.IsVisible = false;
        }
    }

    public void Detach(Image? wallpaperImage, Border? tintOverlay)
    {
        if (wallpaperImage != null)
        {
            wallpaperImage.IsVisible = false;
            wallpaperImage.Source = null;
        }

        if (tintOverlay != null)
        {
            tintOverlay.IsVisible = false;
        }
    }

    public static Bitmap? GenerateBlurredBitmap(string? path, double blurRadius, int maxDimension = 1920)
    {
        byte[]? bytes = GenerateBlurredImageBytes(path, blurRadius, maxDimension);
        if (bytes == null || bytes.Length == 0)
        {
            return null;
        }

        using var ms = new MemoryStream(bytes);
        return new Bitmap(ms);
    }

    public static byte[]? GenerateBlurredImageBytes(string? path, double blurRadius, int maxDimension = 1920)
    {
        using var stream = OpenWallpaperStream(path);
        if (stream == null)
        {
            return null;
        }

        using var originalSkia = SKBitmap.Decode(stream);
        if (originalSkia == null)
        {
            return null;
        }

        int targetWidth = originalSkia.Width;
        int targetHeight = originalSkia.Height;

        if (targetWidth > maxDimension || targetHeight > maxDimension)
        {
            float scale = Math.Min((float)maxDimension / targetWidth, (float)maxDimension / targetHeight);
            targetWidth = Math.Max(1, (int)(targetWidth * scale));
            targetHeight = Math.Max(1, (int)(targetHeight * scale));
        }

        using var scaledBitmap = originalSkia.Resize(new SKImageInfo(targetWidth, targetHeight), SKFilterQuality.Medium);
        if (scaledBitmap == null)
        {
            return null;
        }

        var blurredBitmap = new SKBitmap(targetWidth, targetHeight, scaledBitmap.ColorType, scaledBitmap.AlphaType);
        using (var canvas = new SKCanvas(blurredBitmap))
        {
            canvas.Clear(SKColors.Transparent);
            float sigma = (float)Math.Max(1.0, blurRadius / 2.0);
            using var paint = new SKPaint
            {
                ImageFilter = SKImageFilter.CreateBlur(sigma, sigma),
                IsAntialias = true
            };
            canvas.DrawBitmap(scaledBitmap, 0, 0, paint);
        }

        using var image = SKImage.FromBitmap(blurredBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    private static Stream? OpenWallpaperStream(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            try
            {
                return File.OpenRead(path);
            }
            catch
            {
                // Fall back to default
            }
        }

        try
        {
            var uri = new Uri(DefaultWallpaperAssetUri);
            if (AssetLoader.Exists(uri))
            {
                return AssetLoader.Open(uri);
            }
        }
        catch
        {
            // Ignore asset loading error
        }

        return null;
    }

    public void Dispose()
    {
        _cachedBlurredBitmap?.Dispose();
        _cachedBlurredBitmap = null;
    }
}
