using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace PocketMC.App.Converters;

public class EngineIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Support both string (SelectedInstanceEngineType) and Enum (EngineType) values
        string? engineStr = null;
        if (value is string s)
        {
            engineStr = s;
        }
        else if (value != null)
        {
            engineStr = value.ToString();
        }

        if (!string.IsNullOrEmpty(engineStr))
        {
            // Normalize engine type name to match png filenames:
            // "vanilla", "fabric", "forge", "neoforge", "papermc", "pocketmine-mp", "bds"
            string normalized = engineStr.ToLowerInvariant().Replace(" ", "-");
            
            // Special cases
            if (normalized == "pocketmine") normalized = "pocketmine-mp";
            if (normalized == "bedrock") normalized = "bds";
            if (normalized == "paper") normalized = "papermc";
            if (normalized == "vanillajava") normalized = "vanilla";

            try
            {
                var uri = new Uri($"avares://PocketMC.App/Assets/{normalized}.png");
                using var stream = AssetLoader.Open(uri);
                return new Bitmap(stream);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EngineIconConverter] Failed to load icon '{normalized}': {ex.Message}");
                // Fallback to vanilla icon if not found
                try
                {
                    var fallbackUri = new Uri("avares://PocketMC.App/Assets/vanilla.png");
                    using var stream = AssetLoader.Open(fallbackUri);
                    return new Bitmap(stream);
                }
                catch (Exception exFallback)
                {
                    Console.WriteLine($"[EngineIconConverter] Failed to load fallback vanilla icon: {exFallback.Message}");
                }
            }
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
