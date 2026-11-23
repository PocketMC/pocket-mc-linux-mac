using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;

namespace PocketMC.App.Controls
{
    /// <summary>
    /// A high-performance responsive Avalonia panel that lays out children in an adaptive grid.
    /// Calculates the optimal column count based on MinColumnWidth and stretches
    /// all children within each column to evenly fill available width with no right-side dead space.
    /// </summary>
    public class AdaptiveGridPanel : Panel
    {
        public static readonly StyledProperty<double> MinColumnWidthProperty =
            AvaloniaProperty.Register<AdaptiveGridPanel, double>(nameof(MinColumnWidth), 340.0);

        public static readonly StyledProperty<double> HorizontalSpacingProperty =
            AvaloniaProperty.Register<AdaptiveGridPanel, double>(nameof(HorizontalSpacing), 16.0);

        public static readonly StyledProperty<double> VerticalSpacingProperty =
            AvaloniaProperty.Register<AdaptiveGridPanel, double>(nameof(VerticalSpacing), 16.0);

        static AdaptiveGridPanel()
        {
            AffectsMeasure<AdaptiveGridPanel>(MinColumnWidthProperty, HorizontalSpacingProperty, VerticalSpacingProperty);
            AffectsArrange<AdaptiveGridPanel>(MinColumnWidthProperty, HorizontalSpacingProperty, VerticalSpacingProperty);
        }

        public double MinColumnWidth
        {
            get => GetValue(MinColumnWidthProperty);
            set => SetValue(MinColumnWidthProperty, value);
        }

        public double HorizontalSpacing
        {
            get => GetValue(HorizontalSpacingProperty);
            set => SetValue(HorizontalSpacingProperty, value);
        }

        public double VerticalSpacing
        {
            get => GetValue(VerticalSpacingProperty);
            set => SetValue(VerticalSpacingProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var visibleChildren = GetVisibleChildren();
            if (visibleChildren.Count == 0)
            {
                return new Size(0, 0);
            }

            double availableWidth = double.IsPositiveInfinity(availableSize.Width) || double.IsNaN(availableSize.Width)
                ? MinColumnWidth * visibleChildren.Count + HorizontalSpacing * (visibleChildren.Count - 1)
                : availableSize.Width;

            int columns = CalculateColumnCount(availableWidth);
            double itemWidth = CalculateItemWidth(availableWidth, columns);

            var childConstraint = new Size(itemWidth, double.PositiveInfinity);

            double totalHeight = 0;
            double currentRowMaxHeight = 0;

            for (int i = 0; i < visibleChildren.Count; i++)
            {
                var child = visibleChildren[i];
                child.Measure(childConstraint);

                currentRowMaxHeight = Math.Max(currentRowMaxHeight, child.DesiredSize.Height);

                if ((i + 1) % columns == 0 || i == visibleChildren.Count - 1)
                {
                    totalHeight += currentRowMaxHeight;
                    if (i < visibleChildren.Count - 1)
                    {
                        totalHeight += VerticalSpacing;
                    }
                    currentRowMaxHeight = 0;
                }
            }

            return new Size(availableWidth, totalHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var visibleChildren = GetVisibleChildren();
            if (visibleChildren.Count == 0)
            {
                return finalSize;
            }

            int columns = CalculateColumnCount(finalSize.Width);
            double itemWidth = CalculateItemWidth(finalSize.Width, columns);

            var rowHeights = new List<double>();
            double currentRowMaxHeight = 0;

            for (int i = 0; i < visibleChildren.Count; i++)
            {
                currentRowMaxHeight = Math.Max(currentRowMaxHeight, visibleChildren[i].DesiredSize.Height);

                if ((i + 1) % columns == 0 || i == visibleChildren.Count - 1)
                {
                    rowHeights.Add(currentRowMaxHeight);
                    currentRowMaxHeight = 0;
                }
            }

            double y = 0;
            int rowIndex = 0;

            for (int i = 0; i < visibleChildren.Count; i++)
            {
                int colIndex = i % columns;
                double x = colIndex * (itemWidth + HorizontalSpacing);
                double rowHeight = rowHeights[rowIndex];

                visibleChildren[i].Arrange(new Rect(x, y, itemWidth, rowHeight));

                if (colIndex == columns - 1 || i == visibleChildren.Count - 1)
                {
                    y += rowHeight + VerticalSpacing;
                    rowIndex++;
                }
            }

            return finalSize;
        }

        private int CalculateColumnCount(double availableWidth)
        {
            if (availableWidth <= 0 || MinColumnWidth <= 0) return 1;

            int count = (int)Math.Floor((availableWidth + HorizontalSpacing) / (MinColumnWidth + HorizontalSpacing));
            return Math.Max(1, count);
        }

        private double CalculateItemWidth(double availableWidth, int columns)
        {
            if (columns <= 1) return Math.Max(1.0, availableWidth);

            double totalSpacing = (columns - 1) * HorizontalSpacing;
            return Math.Max(1.0, (availableWidth - totalSpacing) / columns);
        }

        private List<Control> GetVisibleChildren()
        {
            var list = new List<Control>(Children.Count);
            foreach (var child in Children)
            {
                if (child != null && child.IsVisible)
                {
                    list.Add(child);
                }
            }
            return list;
        }
    }
}
