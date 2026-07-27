using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PocketMC.App.Views;

public partial class ConfirmationDialogWindow : Window
{
    public ConfirmationDialogWindow()
    {
        InitializeComponent();
    }

    public ConfirmationDialogWindow(string title, string message, string confirmButtonText = "Delete") : this()
    {
        Title = title;
        if (TxtTitle != null) TxtTitle.Text = title;
        if (TxtMessage != null) TxtMessage.Text = message;
        if (BtnConfirm != null) BtnConfirm.Content = confirmButtonText;
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void BtnConfirm_Click(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    public static async Task<bool> ShowAsync(string title, string message, string confirmButtonText = "Delete")
    {
        var window = new ConfirmationDialogWindow(title, message, confirmButtonText);
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            return await window.ShowDialog<bool>(desktop.MainWindow);
        }
        return false;
    }
}
