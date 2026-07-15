using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace PocketMC.App.Controls
{
    /// <summary>
    /// Lightweight markdown-to-Avalonia renderer for LLM diagnostic output.
    /// Supports: H1-H4, bold, italic, inline code, code blocks, bullet lists, numbered lists, horizontal rules.
    /// </summary>
    public static class MarkdownRenderer
    {
        private static readonly IBrush HeadingBrush = new SolidColorBrush(Color.Parse("#FFFFFF"));
        private static readonly IBrush TextBrush = new SolidColorBrush(Color.Parse("#e0e0e5"));
        private static readonly IBrush CodeBrush = new SolidColorBrush(Color.Parse("#1e90ff"));
        private static readonly IBrush CodeBgBrush = new SolidColorBrush(Color.Parse("#1a1a24"));
        private static readonly IBrush CodeBorderBrush = new SolidColorBrush(Color.Parse("#2c2c38"));
        private static readonly IBrush BulletBrush = new SolidColorBrush(Color.Parse("#1e90ff"));
        private static readonly IBrush HrBrush = new SolidColorBrush(Color.Parse("#2c2c38"));

        public static Control Render(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return new TextBlock { Text = string.Empty };

            var panel = new StackPanel { Spacing = 6 };
            var lines = markdown.Replace("\r\n", "\n").Split('\n');

            bool inCodeBlock = false;
            var codeBlockLines = new List<string>();
            string codeBlockLang = string.Empty;

            var tableLines = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // Code block toggle
                if (line.TrimStart().StartsWith("```"))
                {
                    // Flush table if active
                    if (tableLines.Count > 0)
                    {
                        panel.Children.Add(ParseAndRenderTable(tableLines));
                        tableLines.Clear();
                    }

                    if (inCodeBlock)
                    {
                        // End code block
                        panel.Children.Add(CreateCodeBlock(string.Join("\n", codeBlockLines)));
                        codeBlockLines.Clear();
                        inCodeBlock = false;
                    }
                    else
                    {
                        // Start code block
                        inCodeBlock = true;
                        codeBlockLang = line.TrimStart().Substring(3).Trim();
                    }
                    continue;
                }

                if (inCodeBlock)
                {
                    codeBlockLines.Add(line);
                    continue;
                }

                // Table line collection
                if (line.TrimStart().StartsWith("|"))
                {
                    tableLines.Add(line);
                    continue;
                }

                // Flush table if we hit a non-table line
                if (tableLines.Count > 0)
                {
                    panel.Children.Add(ParseAndRenderTable(tableLines));
                    tableLines.Clear();
                }

                // Empty line
                if (string.IsNullOrWhiteSpace(line))
                {
                    panel.Children.Add(new Border { Height = 4 });
                    continue;
                }

                // Horizontal rule
                if (Regex.IsMatch(line.Trim(), @"^[-*_]{3,}$"))
                {
                    panel.Children.Add(new Border
                    {
                        Height = 1,
                        Background = HrBrush,
                        Margin = new Thickness(0, 8, 0, 8)
                    });
                    continue;
                }

                // Headings
                if (line.StartsWith("####"))
                {
                    panel.Children.Add(CreateHeading(line.Substring(4).Trim().TrimEnd('#').Trim(), 14, FontWeight.SemiBold));
                    continue;
                }
                if (line.StartsWith("###"))
                {
                    panel.Children.Add(CreateHeading(line.Substring(3).Trim().TrimEnd('#').Trim(), 15, FontWeight.SemiBold));
                    continue;
                }
                if (line.StartsWith("##"))
                {
                    panel.Children.Add(CreateHeading(line.Substring(2).Trim().TrimEnd('#').Trim(), 17, FontWeight.Bold));
                    continue;
                }
                if (line.StartsWith("#"))
                {
                    panel.Children.Add(CreateHeading(line.Substring(1).Trim().TrimEnd('#').Trim(), 20, FontWeight.Bold));
                    continue;
                }

                // Bullet list items
                var bulletMatch = Regex.Match(line, @"^(\s*)[*\-+]\s+(.+)$");
                if (bulletMatch.Success)
                {
                    int indent = bulletMatch.Groups[1].Value.Length / 2;
                    panel.Children.Add(CreateBulletItem(bulletMatch.Groups[2].Value, indent));
                    continue;
                }

                // Numbered list items
                var numberedMatch = Regex.Match(line, @"^(\s*)\d+\.\s+(.+)$");
                if (numberedMatch.Success)
                {
                    int indent = numberedMatch.Groups[1].Value.Length / 2;
                    panel.Children.Add(CreateNumberedItem(line, numberedMatch.Groups[2].Value, indent));
                    continue;
                }

                // Regular paragraph with inline formatting
                panel.Children.Add(CreateFormattedTextBlock(line));
            }

            // Handle unclosed table
            if (tableLines.Count > 0)
            {
                panel.Children.Add(ParseAndRenderTable(tableLines));
            }

            // Handle unclosed code block
            if (inCodeBlock && codeBlockLines.Count > 0)
            {
                panel.Children.Add(CreateCodeBlock(string.Join("\n", codeBlockLines)));
            }

            return panel;
        }

        private static Control ParseAndRenderTable(List<string> lines)
        {
            if (lines.Count == 0) return new Border();

            var rows = new List<List<string>>();
            int maxCols = 0;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                // Skip separator rows like |---|---|
                if (Regex.IsMatch(trimmed, @"^\|[\s\-:|]*\|$"))
                    continue;

                var parts = trimmed.Split('|');
                var cells = new List<string>();

                int start = trimmed.StartsWith("|") ? 1 : 0;
                int end = trimmed.EndsWith("|") ? parts.Length - 1 : parts.Length;

                for (int j = start; j < end; j++)
                {
                    cells.Add(parts[j].Trim());
                }

                if (cells.Count > 0)
                {
                    rows.Add(cells);
                    if (cells.Count > maxCols)
                        maxCols = cells.Count;
                }
            }

            if (rows.Count == 0 || maxCols == 0)
                return new Border();

            var grid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 8, 0, 8)
            };

            for (int col = 0; col < maxCols; col++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            }

            for (int r = 0; r < rows.Count; r++)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

                var rowData = rows[r];
                // Check if row 0 is header (which is header if second line in lines list is a separator row)
                bool isHeader = r == 0 && lines.Count > 1 && Regex.IsMatch(lines[1].Trim(), @"^\|[\s\-:|]*\|$");

                for (int col = 0; col < maxCols; col++)
                {
                    string text = col < rowData.Count ? rowData[col] : string.Empty;

                    var border = new Border
                    {
                        BorderBrush = new SolidColorBrush(Color.Parse("#2c2c38")),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(12, 10),
                        Background = isHeader 
                            ? new SolidColorBrush(Color.Parse("#25252f")) 
                            : (r % 2 == 0 ? new SolidColorBrush(Color.Parse("#1a1a24")) : new SolidColorBrush(Color.Parse("#141419")))
                    };

                    var textBlock = new TextBlock
                    {
                        Text = CleanInlineFormatting(text),
                        FontSize = isHeader ? 13 : 12,
                        FontWeight = isHeader ? FontWeight.SemiBold : FontWeight.Normal,
                        Foreground = isHeader ? new SolidColorBrush(Color.Parse("#FFFFFF")) : TextBrush,
                        TextWrapping = TextWrapping.Wrap
                    };

                    border.Child = textBlock;
                    Grid.SetRow(border, r);
                    Grid.SetColumn(border, col);
                    grid.Children.Add(border);
                }
            }

            return grid;
        }

        private static Control CreateHeading(string text, double fontSize, FontWeight weight)
        {
            return new TextBlock
            {
                Text = CleanInlineFormatting(text),
                FontSize = fontSize,
                FontWeight = weight,
                Foreground = HeadingBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 4)
            };
        }

        private static Control CreateBulletItem(string text, int indentLevel)
        {
            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(16 * indentLevel, 0, 0, 0),
                Spacing = 8
            };
            stack.Children.Add(new TextBlock
            {
                Text = "•",
                FontSize = 13,
                Foreground = BulletBrush,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 1, 0, 0)
            });
            stack.Children.Add(new TextBlock
            {
                Text = CleanInlineFormatting(text),
                FontSize = 13,
                Foreground = TextBrush,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 600
            });
            return stack;
        }

        private static Control CreateNumberedItem(string originalLine, string text, int indentLevel)
        {
            var numMatch = Regex.Match(originalLine.TrimStart(), @"^(\d+)\.");
            string number = numMatch.Success ? numMatch.Groups[1].Value + "." : "•";

            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(16 * indentLevel, 0, 0, 0),
                Spacing = 8
            };
            stack.Children.Add(new TextBlock
            {
                Text = number,
                FontSize = 13,
                Foreground = BulletBrush,
                VerticalAlignment = VerticalAlignment.Top,
                MinWidth = 20
            });
            stack.Children.Add(new TextBlock
            {
                Text = CleanInlineFormatting(text),
                FontSize = 13,
                Foreground = TextBrush,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 600
            });
            return stack;
        }

        private static Control CreateCodeBlock(string code)
        {
            return new Border
            {
                Background = CodeBgBrush,
                BorderBrush = CodeBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 10),
                Margin = new Thickness(0, 4, 0, 4),
                Child = new SelectableTextBlock
                {
                    Text = code,
                    FontSize = 12,
                    FontFamily = new FontFamily("Cascadia Code, JetBrains Mono, Fira Code, Consolas, monospace"),
                    Foreground = CodeBrush,
                    TextWrapping = TextWrapping.Wrap
                }
            };
        }

        private static Control CreateFormattedTextBlock(string text)
        {
            return new TextBlock
            {
                Text = CleanInlineFormatting(text),
                FontSize = 13,
                Foreground = TextBrush,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 22
            };
        }

        /// <summary>
        /// Strips markdown inline formatting (bold, italic, code) to plain text.
        /// </summary>
        private static string CleanInlineFormatting(string text)
        {
            // Remove bold+italic: ***text*** or ___text___
            text = Regex.Replace(text, @"\*{3}(.+?)\*{3}", "$1");
            text = Regex.Replace(text, @"_{3}(.+?)_{3}", "$1");
            // Remove bold: **text** or __text__
            text = Regex.Replace(text, @"\*{2}(.+?)\*{2}", "$1");
            text = Regex.Replace(text, @"_{2}(.+?)_{2}", "$1");
            // Remove italic: *text* or _text_
            text = Regex.Replace(text, @"\*(.+?)\*", "$1");
            text = Regex.Replace(text, @"(?<!\w)_(.+?)_(?!\w)", "$1");
            // Remove inline code: `text`
            text = Regex.Replace(text, @"`(.+?)`", "$1");
            // Remove links: [text](url)
            text = Regex.Replace(text, @"\[(.+?)\]\(.+?\)", "$1");
            return text;
        }
    }
}
