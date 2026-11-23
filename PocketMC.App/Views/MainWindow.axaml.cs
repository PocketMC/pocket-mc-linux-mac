using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using PocketMC.App.Services;
using PocketMC.App.ViewModels;

namespace PocketMC.App.Views;

public partial class MainWindow : Window
{
    public static readonly DirectProperty<MainWindow, Thickness> TitleBarContentMarginProperty =
        AvaloniaProperty.RegisterDirect<MainWindow, Thickness>(
            nameof(TitleBarContentMargin),
            o => o.TitleBarContentMargin);

    private Thickness _titleBarContentMargin;

    public Thickness TitleBarContentMargin
    {
        get => _titleBarContentMargin;
        private set => SetAndRaise(TitleBarContentMarginProperty, ref _titleBarContentMargin, value);
    }

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainWindowViewModel viewModel) : this()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            TitleBarContentMargin = new Thickness(80, 0, 0, 0);
        }
        else
        {
            TitleBarContentMargin = new Thickness(0, 0, 140, 0);
        }

        DataContext = viewModel;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        try
        {
            if (App.Services != null)
            {
                var accentService = App.Services.GetService<AccentColorService>();
                accentService?.ApplyCurrentAccent();

                var wallpaperService = App.Services.GetService<WallpaperBackdropService>();
                wallpaperService?.Apply(WallpaperBlurLayer, WallpaperTintOverlay);
            }
        }
        catch
        {
            // Non-critical visual fallback
        }
    }

    private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var vm = DataContext as MainWindowViewModel;

        // Escape: Back navigation
        if (e.Key == Key.Escape)
        {
            if (vm != null && vm.CanNavigateBack)
            {
                vm.NavigateBackCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }
        // Alt+Left: Back navigation
        else if (e.Key == Key.Left && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            if (vm != null && vm.CanNavigateBack)
            {
                vm.NavigateBackCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }
        // Alt+Right: Forward navigation
        else if (e.Key == Key.Right && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            if (vm != null && vm.CanNavigateForward)
            {
                vm.NavigateForwardCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }
        // Ctrl+1: Dashboard
        else if (e.Key == Key.D1 && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            vm?.NavigateDashboardCommand.Execute(null);
            e.Handled = true;
            return;
        }
        // Ctrl+2: Tunnel
        else if (e.Key == Key.D2 && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            vm?.NavigateTunnelCommand.Execute(null);
            e.Handled = true;
            return;
        }
        // Ctrl+3: Remote Control
        else if (e.Key == Key.D3 && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            vm?.NavigateRemoteControlCommand.Execute(null);
            e.Handled = true;
            return;
        }
        // Ctrl+7: News
        else if (e.Key == Key.D7 && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            vm?.NavigateNewsCommand.Execute(null);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
