using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PocketMC.App.Controls;
using PocketMC.Infrastructure.News;

namespace PocketMC.App.Views;

public partial class NewsArticleView : UserControl
{
    public NewsArticleView()
    {
        InitializeComponent();
    }

    public void Display(NewsItem? item)
    {
        ArticleContentHost.Children.Clear();

        if (item == null)
        {
            TxtTypeBadge.Text = string.Empty;
            TxtPriorityBadge.Text = string.Empty;
            PriorityBadgeBorder.IsVisible = false;
            TxtPublishedDate.Text = string.Empty;
            return;
        }

        TxtTypeBadge.Text = item.Metadata.Type.ToString();
        TxtPublishedDate.Text = item.Metadata.PublishedUtc.ToLocalTime().ToString("MMM dd, yyyy");

        if (item.Metadata.Priority != NewsPriority.Normal)
        {
            PriorityBadgeBorder.IsVisible = true;
            TxtPriorityBadge.Text = item.Metadata.Priority.ToString();
            bool isCritical = item.Metadata.Priority == NewsPriority.Critical;
            string hex = isCritical ? "#EF4444" : "#F59E0B";
            string bgHex = isCritical ? "#26EF4444" : "#26F59E0B";
            if (Color.TryParse(hex, out Color borderColor) && Color.TryParse(bgHex, out Color bgColor))
            {
                var brush = new SolidColorBrush(borderColor);
                PriorityBadgeBorder.BorderBrush = brush;
                PriorityBadgeBorder.Background = new SolidColorBrush(bgColor);
                TxtPriorityBadge.Foreground = brush;
            }
        }
        else
        {
            PriorityBadgeBorder.IsVisible = false;
            TxtPriorityBadge.Text = string.Empty;
        }

        ArticleContentHost.Children.Add(NewsContentRenderer.Build(item));
    }
}
