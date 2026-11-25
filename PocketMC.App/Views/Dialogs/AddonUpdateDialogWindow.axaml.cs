using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PocketMC.App.Views.Dialogs;

public partial class AddonUpdateDialogWindow : Window
{
    public bool IsConfirmed { get; private set; }

    public AddonUpdateDialogWindow()
    {
        InitializeComponent();
    }

    public AddonUpdateDialogWindow(string summary, IEnumerable<string> updateLines) : this()
    {
        TxtSummary.Text = summary;
        UpdatesList.ItemsSource = updateLines;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        IsConfirmed = false;
        Close(false);
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        IsConfirmed = true;
        Close(true);
    }
}
