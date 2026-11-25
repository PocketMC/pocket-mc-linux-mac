using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PocketMC.App.Views.Dialogs;

public partial class AddonUpdateCheckDialogWindow : Window
{
    public AddonUpdateCheckDialogWindow()
    {
        InitializeComponent();
    }

    public AddonUpdateCheckDialogWindow(string title, string summary, IEnumerable<string> lines, double progressPercent = 100) : this()
    {
        TxtTitle.Text = title;
        TxtSummary.Text = summary;
        ItemsList.ItemsSource = lines;
        OverallProgressBar.Value = progressPercent;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
