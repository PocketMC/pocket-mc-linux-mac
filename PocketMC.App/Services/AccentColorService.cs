using System;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using PocketMC.Core.Models;
using PocketMC.Core.Services;

namespace PocketMC.App.Services;

public sealed class AccentColorService
{
    public const string AutomaticMode = "Automatic";
    public const string CustomMode = "Custom";
    public const string DefaultCustomAccentColor = "#008B00";

    private static readonly Color FallbackSystemAccent = Color.FromRgb(0x00, 0x8B, 0x00);
    private readonly ISettingsService? _settingsService;

    public event Action<Color>? AccentChanged;
    public static event Action<Color>? GlobalAccentChanged;

    public AccentColorService(ISettingsService? settingsService = null)
    {
        _settingsService = settingsService;
    }

    public void ApplyCurrentAccent()
    {
        var settings = _settingsService?.Settings;
        if (settings != null && IsCustomMode(settings.AccentColorMode) &&
            TryParseHexColor(settings.CustomAccentColor, out Color customColor, out _))
        {
            ApplyCustomAccent(customColor);
            return;
        }

        ApplySystemAccent();
    }

    public void ApplyCustomAccent(Color color)
    {
        ApplyAccentToApplication(color);
        NotifyAccentChanged(color);
    }

    public void ApplySystemAccent()
    {
        Color systemColor = GetSystemAccentColor();
        ApplyAccentToApplication(systemColor);
        NotifyAccentChanged(systemColor);
    }

    public void ReassertAccent()
    {
        var settings = _settingsService?.Settings;
        if (settings != null && IsCustomMode(settings.AccentColorMode) &&
            TryParseHexColor(settings.CustomAccentColor, out Color customColor, out _))
        {
            ApplyAccentToApplication(customColor);
            return;
        }

        Color systemColor = GetSystemAccentColor();
        ApplyAccentToApplication(systemColor);
    }

    public Color GetCurrentAccentColor()
    {
        var settings = _settingsService?.Settings;
        if (settings != null && IsCustomMode(settings.AccentColorMode) &&
            TryParseHexColor(settings.CustomAccentColor, out Color customColor, out _))
        {
            return customColor;
        }

        return GetSystemAccentColor();
    }

    public static bool IsCustomMode(string? mode) =>
        string.Equals(mode, CustomMode, StringComparison.OrdinalIgnoreCase);

    public static bool TryParseHexColor(string? value, out Color color, out string normalizedHex)
    {
        color = default;
        normalizedHex = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string cleaned = value.Trim();
        if (cleaned.StartsWith("#", StringComparison.Ordinal))
        {
            cleaned = cleaned.Substring(1);
        }

        if (cleaned.Length == 6)
        {
            if (uint.TryParse(cleaned, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
            {
                byte r = (byte)((rgb >> 16) & 0xFF);
                byte g = (byte)((rgb >> 8) & 0xFF);
                byte b = (byte)(rgb & 0xFF);
                color = Color.FromArgb(0xFF, r, g, b);
                normalizedHex = $"#{r:X2}{g:X2}{b:X2}";
                return true;
            }
        }
        else if (cleaned.Length == 8)
        {
            if (uint.TryParse(cleaned, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint argb))
            {
                byte a = (byte)((argb >> 24) & 0xFF);
                byte r = (byte)((argb >> 16) & 0xFF);
                byte g = (byte)((argb >> 8) & 0xFF);
                byte b = (byte)(argb & 0xFF);
                color = Color.FromArgb(a, r, g, b);
                normalizedHex = $"#{r:X2}{g:X2}{b:X2}";
                return true;
            }
        }

        return false;
    }

    public static Color Lighten(Color color, float factor)
    {
        factor = Math.Clamp(factor, 0f, 1f);
        byte r = (byte)Math.Clamp(color.R + (255 - color.R) * factor, 0, 255);
        byte g = (byte)Math.Clamp(color.G + (255 - color.G) * factor, 0, 255);
        byte b = (byte)Math.Clamp(color.B + (255 - color.B) * factor, 0, 255);
        return Color.FromArgb(color.A, r, g, b);
    }

    public static Color Darken(Color color, float factor)
    {
        factor = Math.Clamp(factor, 0f, 1f);
        byte r = (byte)Math.Clamp(color.R * (1f - factor), 0, 255);
        byte g = (byte)Math.Clamp(color.G * (1f - factor), 0, 255);
        byte b = (byte)Math.Clamp(color.B * (1f - factor), 0, 255);
        return Color.FromArgb(color.A, r, g, b);
    }

    private Color GetSystemAccentColor()
    {
        if (Application.Current != null &&
            Application.Current.TryGetResource("SystemAccentColor", Application.Current.ActualThemeVariant, out var res) &&
            res is Color systemColor)
        {
            return systemColor;
        }

        return FallbackSystemAccent;
    }

    private void ApplyAccentToApplication(Color color)
    {
        if (Application.Current == null)
        {
            return;
        }

        var app = Application.Current;
        app.Resources["SystemAccentColor"] = color;
        app.Resources["SystemAccentColorLight1"] = Lighten(color, 0.15f);
        app.Resources["SystemAccentColorLight2"] = Lighten(color, 0.30f);
        app.Resources["SystemAccentColorLight3"] = Lighten(color, 0.45f);
        app.Resources["SystemAccentColorDark1"] = Darken(color, 0.15f);
        app.Resources["SystemAccentColorDark2"] = Darken(color, 0.30f);
        app.Resources["SystemAccentColorDark3"] = Darken(color, 0.45f);

        app.Resources["AccentBrush"] = new SolidColorBrush(color);
        app.Resources["AccentColorBrush"] = new SolidColorBrush(color);
    }

    private void NotifyAccentChanged(Color color)
    {
        AccentChanged?.Invoke(color);
        GlobalAccentChanged?.Invoke(color);
    }
}
