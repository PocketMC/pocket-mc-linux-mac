using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Lucide.Avalonia;
using PocketMC.Core.Services;

namespace PocketMC.App.Views.Dialogs;

public partial class AppDialogWindow : Window
{
    private string? _linkUrl;

    public DialogResult Result { get; private set; } = DialogResult.Dismiss;

    public AppDialogWindow()
    {
        InitializeComponent();
    }

    public void Configure(
        string title,
        string message,
        DialogType type = DialogType.Information,
        bool showCancel = false,
        string? primaryButtonText = null,
        string? secondaryButtonText = null,
        string? cancelButtonText = null,
        string? linkText = null,
        string? linkUrl = null)
    {
        Title = title;
        TxtTitle.Text = title;
        TxtMessage.Text = message;

        _linkUrl = linkUrl;
        if (!string.IsNullOrEmpty(linkText) && !string.IsNullOrEmpty(linkUrl))
        {
            TxtLink.Text = linkText;
            BtnLink.IsVisible = true;
        }
        else
        {
            BtnLink.IsVisible = false;
        }

        // Configure Icon and badge colors according to DialogType
        switch (type)
        {
            case DialogType.Warning:
                DialogIcon.Kind = LucideIconKind.TriangleAlert;
                DialogIcon.Foreground = new SolidColorBrush(Color.FromRgb(0xF3, 0x9C, 0x12)); // Warning Yellow
                IconBadge.Background = new SolidColorBrush(Color.FromArgb(0x26, 0xF3, 0x9C, 0x12));
                break;

            case DialogType.Error:
                DialogIcon.Kind = LucideIconKind.CircleAlert;
                DialogIcon.Foreground = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)); // Danger Red
                IconBadge.Background = new SolidColorBrush(Color.FromArgb(0x26, 0xE7, 0x4C, 0x3C));
                break;

            case DialogType.Question:
                DialogIcon.Kind = LucideIconKind.CircleQuestionMark;
                DialogIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xDB)); // Question Blue
                IconBadge.Background = new SolidColorBrush(Color.FromArgb(0x26, 0x34, 0x98, 0xDB));
                break;

            case DialogType.Information:
            default:
                DialogIcon.Kind = LucideIconKind.Info;
                DialogIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71)); // Emerald Info Green
                IconBadge.Background = new SolidColorBrush(Color.FromArgb(0x26, 0x2E, 0xCC, 0x71));
                break;
        }

        // Configure buttons
        var (defaultPrimary, defaultSecondary) = DialogButtonResolver.Resolve(type, showCancel, primaryButtonText, secondaryButtonText);
        BtnPrimary.Content = defaultPrimary;

        if (defaultSecondary != null)
        {
            BtnSecondary.Content = defaultSecondary;
            BtnSecondary.IsVisible = true;
        }
        else
        {
            BtnSecondary.IsVisible = false;
        }

        if (showCancel && cancelButtonText != null)
        {
            BtnTertiary.Content = cancelButtonText;
            BtnTertiary.IsVisible = true;
        }
        else
        {
            BtnTertiary.IsVisible = false;
        }
    }

    private void BtnPrimary_Click(object? sender, RoutedEventArgs e)
    {
        Result = DialogResult.Yes; // or Ok
        Close(DialogResult.Yes);
    }

    private void BtnSecondary_Click(object? sender, RoutedEventArgs e)
    {
        Result = DialogResult.No;
        Close(DialogResult.No);
    }

    private void BtnTertiary_Click(object? sender, RoutedEventArgs e)
    {
        Result = DialogResult.Cancel;
        Close(DialogResult.Cancel);
    }

    private void BtnLink_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_linkUrl)) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _linkUrl,
                UseShellExecute = true
            });
        }
        catch
        {
            // Best effort
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Result = BtnTertiary.IsVisible ? DialogResult.Cancel :
                     BtnSecondary.IsVisible ? DialogResult.No :
                     DialogResult.Dismiss;
            Close(Result);
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
