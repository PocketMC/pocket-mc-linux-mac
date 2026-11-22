using System;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PocketMC.Core.Services;

namespace PocketMC.App.Views.Dialogs;

public partial class ProgressDialogWindow : Window
{
    private CancellationTokenSource? _cts;

    public ProgressDialogWindow()
    {
        InitializeComponent();
    }

    public void Configure(string title, string message, CancellationTokenSource? cts = null)
    {
        Title = title;
        TxtMessage.Text = message;
        TxtDetail.Text = string.Empty;
        _cts = cts;

        if (_cts != null)
        {
            BtnCancel.IsVisible = true;
        }
    }

    public void UpdateProgress(double percentage, string? detail = null)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (percentage < 0)
            {
                DialogProgressBar.IsIndeterminate = true;
            }
            else
            {
                DialogProgressBar.IsIndeterminate = false;
                DialogProgressBar.Value = Math.Clamp(percentage, 0, 100);
            }

            if (!string.IsNullOrEmpty(detail))
            {
                TxtDetail.Text = detail;
            }
        });
    }

    public void UpdateProgress(ProgressDialogUpdate update)
    {
        UpdateProgress(update.Percentage, update.Message);
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        BtnCancel.IsEnabled = false;
        TxtDetail.Text = "Cancelling...";
    }
}
