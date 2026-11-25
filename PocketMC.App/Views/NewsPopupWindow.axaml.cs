using Avalonia.Controls;
using Avalonia.Interactivity;
using PocketMC.Infrastructure.News;

namespace PocketMC.App.Views;

public partial class NewsPopupWindow : Window
{
    public bool WasAcknowledged { get; private set; }

    public NewsPopupWindow()
    {
        InitializeComponent();
    }

    public NewsPopupWindow(NewsItem item) : this()
    {
        Title = item.Title;
        ArticleView.Display(item);
    }

    private void BtnAcknowledge_Click(object? sender, RoutedEventArgs e)
    {
        WasAcknowledged = true;
        Close(true);
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        WasAcknowledged = false;
        Close(false);
    }
}
