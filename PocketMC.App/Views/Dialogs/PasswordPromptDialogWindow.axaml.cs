using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace PocketMC.App.Views.Dialogs;

public partial class PasswordPromptDialogWindow : Window
{
    public bool Confirmed { get; private set; }
    public string? Username => TxtUsernameInput.Text;
    public string? Password => PwdInput.Text;

    public PasswordPromptDialogWindow()
    {
        InitializeComponent();
    }

    public void Configure(string title, string message, bool askUsername, bool askPassword)
    {
        Title = title;
        TxtTitle.Text = title;
        TxtMessage.Text = message;

        TxtUsernameInput.IsVisible = askUsername;
        PwdInput.IsVisible = askPassword;

        if (askUsername)
        {
            TxtUsernameInput.Focus();
        }
        else if (askPassword)
        {
            PwdInput.Focus();
        }
    }

    private void BtnPrimary_Click(object? sender, RoutedEventArgs e)
    {
        Confirmed = true;
        Close((Username, Password));
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close(((string?)null, (string?)null));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            BtnCancel_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            BtnPrimary_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }
}
