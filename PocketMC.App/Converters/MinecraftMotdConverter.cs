using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PocketMC.App.Converters
{
    public class MinecraftMotdConverter : IValueConverter
    {
        private static readonly Dictionary<char, IBrush> ColorMap = new()
        {
            { '0', new SolidColorBrush(Color.FromRgb(0, 0, 0)) },
            { '1', new SolidColorBrush(Color.FromRgb(0, 0, 170)) },
            { '2', new SolidColorBrush(Color.FromRgb(0, 170, 0)) },
            { '3', new SolidColorBrush(Color.FromRgb(0, 170, 170)) },
            { '4', new SolidColorBrush(Color.FromRgb(170, 0, 0)) },
            { '5', new SolidColorBrush(Color.FromRgb(170, 0, 170)) },
            { '6', new SolidColorBrush(Color.FromRgb(255, 170, 0)) },
            { '7', new SolidColorBrush(Color.FromRgb(170, 170, 170)) },
            { '8', new SolidColorBrush(Color.FromRgb(85, 85, 85)) },
            { '9', new SolidColorBrush(Color.FromRgb(85, 85, 255)) },
            { 'a', new SolidColorBrush(Color.FromRgb(85, 255, 85)) },
            { 'b', new SolidColorBrush(Color.FromRgb(85, 255, 255)) },
            { 'c', new SolidColorBrush(Color.FromRgb(255, 85, 85)) },
            { 'd', new SolidColorBrush(Color.FromRgb(255, 85, 255)) },
            { 'e', new SolidColorBrush(Color.FromRgb(255, 255, 85)) },
            { 'f', new SolidColorBrush(Color.FromRgb(255, 255, 255)) }
        };

        private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.FromRgb(170, 170, 170));

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var text = value as string;
            var textBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };

            if (string.IsNullOrWhiteSpace(text))
            {
                textBlock.Text = "A Minecraft Server";
                textBlock.Foreground = DefaultBrush;
                return textBlock;
            }

            text = text.Trim();

            // Detect JSON MOTD format
            if (text.StartsWith("{"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    ParseJsonMotd(doc.RootElement, textBlock);
                    return textBlock;
                }
                catch (JsonException)
                {
                    // Fall back to legacy parsing
                }
            }

            // Normalise prefixes to §
            text = text.Replace("\\u00A7", "§");
            text = text.Replace("&", "§");
            text = text.Replace("\\n", "\n");

            ParseLegacyMotd(text, textBlock);
            return textBlock;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private static void ParseLegacyMotd(string text, TextBlock textBlock)
        {
            IBrush currentBrush = DefaultBrush;
            bool isBold = false;
            bool isItalic = false;
            bool isUnderline = false;
            bool isStrikethrough = false;

            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == '§' && i + 1 < text.Length)
                {
                    char code = char.ToLowerInvariant(text[i + 1]);
                    i += 2;

                    if (ColorMap.TryGetValue(code, out var brush))
                    {
                        currentBrush = brush;
                        isBold = false;
                        isItalic = false;
                        isUnderline = false;
                        isStrikethrough = false;
                    }
                    else
                    {
                        switch (code)
                        {
                            case 'l': isBold = true; break;
                            case 'o': isItalic = true; break;
                            case 'n': isUnderline = true; break;
                            case 'm': isStrikethrough = true; break;
                            case 'r':
                                currentBrush = DefaultBrush;
                                isBold = false;
                                isItalic = false;
                                isUnderline = false;
                                isStrikethrough = false;
                                break;
                        }
                    }
                }
                else
                {
                    int nextSection = text.IndexOf('§', i);
                    string chunk = nextSection == -1 ? text.Substring(i) : text.Substring(i, nextSection - i);
                    i = nextSection == -1 ? text.Length : nextSection;

                    if (!string.IsNullOrEmpty(chunk))
                    {
                        var run = new Run(chunk)
                        {
                            Foreground = currentBrush,
                            FontWeight = isBold ? FontWeight.Bold : FontWeight.Normal,
                            FontStyle = isItalic ? FontStyle.Italic : FontStyle.Normal
                        };

                        if (isUnderline && isStrikethrough)
                        {
                            run.TextDecorations = TextDecorations.Underline;
                        }
                        else if (isUnderline)
                        {
                            run.TextDecorations = TextDecorations.Underline;
                        }
                        else if (isStrikethrough)
                        {
                            run.TextDecorations = TextDecorations.Strikethrough;
                        }

                        textBlock.Inlines ??= new InlineCollection();
                        textBlock.Inlines.Add(run);
                    }
                }
            }
        }

        private static void ParseJsonMotd(JsonElement element, TextBlock textBlock)
        {
            textBlock.Inlines ??= new InlineCollection();
            if (element.ValueKind == JsonValueKind.String)
            {
                textBlock.Inlines.Add(new Run(element.GetString() ?? string.Empty) { Foreground = DefaultBrush });
                return;
            }

            if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("text", out var textProp))
                {
                    string txt = textProp.GetString() ?? string.Empty;
                    IBrush brush = DefaultBrush;
                    if (element.TryGetProperty("color", out var colorProp))
                    {
                        string colorStr = colorProp.GetString() ?? string.Empty;
                        if (colorStr.StartsWith("#") && Color.TryParse(colorStr, out var parsedColor))
                        {
                            brush = new SolidColorBrush(parsedColor);
                        }
                    }

                    if (!string.IsNullOrEmpty(txt))
                    {
                        textBlock.Inlines.Add(new Run(txt) { Foreground = brush });
                    }
                }

                if (element.TryGetProperty("extra", out var extraProp) && extraProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in extraProp.EnumerateArray())
                    {
                        ParseJsonMotd(item, textBlock);
                    }
                }
            }
        }
    }
}
