using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace PocketMC.App.Views
{
    public partial class CurseForgeApiKeyDialogWindow : Window
    {
        public string? ApiKey { get; private set; }

        public CurseForgeApiKeyDialogWindow()
        {
            InitializeComponent();
        }

        private void BtnSave_Click(object? sender, RoutedEventArgs e)
        {
            var txtBox = this.FindControl<TextBox>("TxtApiKey");
            if (txtBox == null || string.IsNullOrWhiteSpace(txtBox.Text))
            {
                var txtError = this.FindControl<TextBlock>("TxtError");
                if (txtError != null) txtError.IsVisible = true;
                return;
            }

            ApiKey = txtBox.Text.Trim();
            Close(true);
        }

        private void BtnCancel_Click(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}
