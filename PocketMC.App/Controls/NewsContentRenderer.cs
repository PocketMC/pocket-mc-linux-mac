using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using PocketMC.Infrastructure.News;

namespace PocketMC.App.Controls;

public static class NewsContentRenderer
{
    private static readonly FontFamily CodeFont = new("Cascadia Mono, Consolas, Menlo, DejaVu Sans Mono, monospace");

    public static StackPanel Build(NewsItem? item, bool includeTitle = true)
    {
        var panel = new StackPanel
        {
            Spacing = 10
        };

        if (item == null)
        {
            return panel;
        }

        foreach (NewsContentBlock block in item.Blocks)
        {
            if (!includeTitle && block.Type == NewsBlockType.Title)
            {
                continue;
            }

            Control control = RenderBlock(block);
            panel.Children.Add(control);
        }

        return panel;
    }

    private static Control RenderBlock(NewsContentBlock block)
    {
        return block.Type switch
        {
            NewsBlockType.Title => CreateTitle(block.Text),
            NewsBlockType.Subtitle => CreateSubtitle(block.Text),
            NewsBlockType.Heading => CreateHeading(block.Text),
            NewsBlockType.Paragraph => CreateParagraph(block.Text),
            NewsBlockType.BulletList => CreateList(block.Items, numbered: false),
            NewsBlockType.NumberedList => CreateList(block.Items, numbered: true),
            NewsBlockType.Warning => CreateCallout("Warning", block.Text, "#F59E0B", "#1AF59E0B"),
            NewsBlockType.Important => CreateCallout(
                "Important",
                block.Text,
                ResolveBrush("AccentFillColorDefaultBrush", "#008B00"),
                ResolveBrush("ControlFillColorSecondaryBrush", "#14FFFFFF")),
            NewsBlockType.Code => CreateCodeBlock(block.Text),
            NewsBlockType.Link => CreateLinkControl(block.Text, block.Link),
            NewsBlockType.Divider => new Border
            {
                Height = 1,
                Margin = new Thickness(0, 6),
                Background = ResolveBrush("CardStrokeColorDefaultBrush", "#24FFFFFF")
            },
            _ => CreateParagraph(block.Text)
        };
    }

    private static Control CreateTitle(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 24,
            FontWeight = FontWeight.SemiBold,
            Foreground = ResolveBrush("TextFillColorPrimaryBrush", "#FFFFFF"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 2)
        };
    }

    private static Control CreateSubtitle(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 14,
            Foreground = ResolveBrush("TextFillColorSecondaryBrush", "#B8FFFFFF"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4)
        };
    }

    private static Control CreateHeading(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 17,
            FontWeight = FontWeight.SemiBold,
            Foreground = ResolveBrush("TextFillColorPrimaryBrush", "#FFFFFF"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 2)
        };
    }

    private static Control CreateParagraph(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 13.5,
            LineHeight = 20,
            Foreground = ResolveBrush("TextFillColorPrimaryBrush", "#E6FFFFFF"),
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static Control CreateList(IReadOnlyList<string>? items, bool numbered)
    {
        var listPanel = new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(4, 2, 0, 2)
        };

        if (items == null || items.Count == 0)
        {
            return listPanel;
        }

        IBrush markerBrush = ResolveBrush("TextFillColorSecondaryBrush", "#B8FFFFFF");
        IBrush textBrush = ResolveBrush("TextFillColorPrimaryBrush", "#E6FFFFFF");

        for (int i = 0; i < items.Count; i++)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions(numbered ? "26,*" : "18,*")
            };

            var marker = new TextBlock
            {
                Text = numbered ? $"{i + 1}." : "\u2022",
                FontSize = 13.5,
                Foreground = markerBrush,
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(marker, 0);

            var content = new TextBlock
            {
                Text = items[i],
                FontSize = 13.5,
                LineHeight = 20,
                Foreground = textBrush,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(content, 1);

            row.Children.Add(marker);
            row.Children.Add(content);
            listPanel.Children.Add(row);
        }

        return listPanel;
    }

    private static Control CreateCallout(string heading, string text, string accentHex, string backgroundHex)
    {
        return CreateCallout(heading, text, BrushFromHex(accentHex), BrushFromHex(backgroundHex));
    }

    private static Control CreateCallout(string heading, string text, IBrush accentBrush, IBrush backgroundBrush)
    {
        var content = new StackPanel
        {
            Spacing = 4
        };

        content.Children.Add(new TextBlock
        {
            Text = heading,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = accentBrush
        });

        content.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 13,
            LineHeight = 19,
            Foreground = ResolveBrush("TextFillColorPrimaryBrush", "#E6FFFFFF"),
            TextWrapping = TextWrapping.Wrap
        });

        return new Border
        {
            Margin = new Thickness(0, 4),
            Padding = new Thickness(14, 10),
            CornerRadius = new CornerRadius(6),
            BorderBrush = accentBrush,
            BorderThickness = new Thickness(4, 1, 1, 1),
            Background = backgroundBrush,
            Child = content
        };
    }

    private static Control CreateCodeBlock(string text)
    {
        var box = new TextBox
        {
            Text = text,
            IsReadOnly = true,
            FontFamily = CodeFont,
            FontSize = 12.5,
            Foreground = ResolveBrush("ConsoleTextBrush", "#F4F4F7"),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true
        };

        return new Border
        {
            Margin = new Thickness(0, 4),
            Padding = new Thickness(12, 10),
            CornerRadius = new CornerRadius(6),
            Background = ResolveBrush("ConsoleBackgroundBrush", "#CC0B0B0D"),
            BorderBrush = ResolveBrush("ControlStrokeColorDefaultBrush", "#1DFFFFFF"),
            BorderThickness = new Thickness(1),
            Child = box
        };
    }

    private static Control CreateLinkControl(string text, Uri? link)
    {
        string label = string.IsNullOrWhiteSpace(text) ? (link?.ToString() ?? string.Empty) : text;
        IBrush accentBrush = ResolveBrush("AccentTextFillColorPrimaryBrush", "#4ECC4E");

        var textBlock = new TextBlock
        {
            Text = label,
            FontSize = 13.5,
            FontWeight = FontWeight.Medium,
            Foreground = accentBrush,
            TextDecorations = TextDecorations.Underline,
            TextWrapping = TextWrapping.Wrap
        };

        var button = new Button
        {
            Content = textBlock,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0, 2),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        if (Avalonia.Application.Current != null)
        {
            try
            {
                button.Cursor = new Cursor(StandardCursorType.Hand);
            }
            catch
            {
                // Headless platform without cursor factory
            }
        }

        if (link != null && (link.Scheme == Uri.UriSchemeHttp || link.Scheme == Uri.UriSchemeHttps))
        {
            ToolTip.SetTip(button, link.ToString());
            button.Click += (_, _) => OpenSafeUrl(link.ToString());
        }
        else
        {
            button.IsEnabled = false;
        }

        return button;
    }

    private static void OpenSafeUrl(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else
            {
                Process.Start("xdg-open", url);
            }
        }
        catch
        {
            // Ignore OS browser launch errors
        }
    }

    private static IBrush ResolveBrush(string resourceKey, string fallbackHex)
    {
        try
        {
            if (Application.Current != null &&
                Application.Current.TryFindResource(resourceKey, Application.Current.ActualThemeVariant, out object? resource) &&
                resource is IBrush brush)
            {
                return brush;
            }
        }
        catch
        {
            // Fall back safely in headless test environments
        }

        return BrushFromHex(fallbackHex);
    }

    private static IBrush BrushFromHex(string hex)
    {
        return Color.TryParse(hex, out Color color)
            ? new SolidColorBrush(color)
            : Brushes.Transparent;
    }
}
