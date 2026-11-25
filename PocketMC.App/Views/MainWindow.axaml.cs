using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using PocketMC.App.Services;
using PocketMC.App.ViewModels;
using PocketMC.Core.Services;

namespace PocketMC.App.Views;

public partial class MainWindow : Window
{
    public static readonly DirectProperty<MainWindow, Thickness> TitleBarContentMarginProperty =
        AvaloniaProperty.RegisterDirect<MainWindow, Thickness>(
            nameof(TitleBarContentMargin),
            o => o.TitleBarContentMargin);

    private Thickness _titleBarContentMargin;
    private MainWindowViewModel? _subscribedVm;

    public Thickness TitleBarContentMargin
    {
        get => _titleBarContentMargin;
        private set => SetAndRaise(TitleBarContentMarginProperty, ref _titleBarContentMargin, value);
    }

    public MainWindow()
    {
        InitializeComponent();
        TitleBarContentMargin = GetDefaultTitleBarMargin();
    }

    public MainWindow(MainWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    public static Thickness GetDefaultTitleBarMargin(OSPlatform? platform = null)
    {
        if (platform.HasValue)
        {
            if (platform.Value == OSPlatform.OSX) return new Thickness(80, 0, 0, 0);
            if (platform.Value == OSPlatform.Linux) return new Thickness(0, 0, 0, 0);
            return new Thickness(0, 0, 140, 0);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new Thickness(80, 0, 0, 0);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new Thickness(0, 0, 0, 0);
        }

        return new Thickness(0, 0, 140, 0);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_subscribedVm != null)
        {
            _subscribedVm.BackdropRefreshRequested -= ApplyBackdropFromSettings;
            _subscribedVm = null;
        }

        if (DataContext is MainWindowViewModel vm)
        {
            _subscribedVm = vm;
            _subscribedVm.BackdropRefreshRequested += ApplyBackdropFromSettings;
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        try
        {
            App.Services?.GetService<AccentColorService>()?.ApplyCurrentAccent();
            ApplyBackdropFromSettings();
        }
        catch
        {
            // Non-critical visual fallback
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_subscribedVm != null)
        {
            _subscribedVm.BackdropRefreshRequested -= ApplyBackdropFromSettings;
            _subscribedVm.Dispose();
            _subscribedVm = null;
        }

        base.OnClosed(e);
    }

    public void ApplyBackdropFromSettings()
    {
        try
        {
            var wallpaperService = App.Services?.GetService<WallpaperBackdropService>();
            var settings = App.Services?.GetService<ISettingsService>()?.Settings;
            var vm = DataContext as MainWindowViewModel;

            if (wallpaperService == null)
            {
                if (vm != null)
                {
                    vm.IsWallpaperActive = false;
                }
                return;
            }

            if (settings != null && string.Equals(settings.WindowBackdrop, "None", StringComparison.OrdinalIgnoreCase))
            {
                wallpaperService.Detach(WallpaperBlurLayer, WallpaperTintOverlay);
                if (vm != null)
                {
                    vm.IsWallpaperActive = false;
                }
                return;
            }

            double blur = settings?.WallpaperBlurRadius ?? 80.0;
            double tint = settings?.WallpaperTintOpacity ?? 0.72;
            string? customPath = settings?.CustomBackgroundImagePath;

            wallpaperService.Apply(WallpaperBlurLayer, WallpaperTintOverlay, blur, tint, customPath);
            if (vm != null)
            {
                vm.IsWallpaperActive = WallpaperBlurLayer.IsVisible;
            }
        }
        catch
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.IsWallpaperActive = false;
            }
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
        // Ctrl+B: Toggle navigation rail
        else if (e.Key == Key.B && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            vm?.TogglePaneCommand.Execute(null);
            e.Handled = true;
            return;
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
        // Ctrl+4: Runtimes
        else if (e.Key == Key.D4 && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            vm?.NavigateRuntimesCommand.Execute(null);
            e.Handled = true;
            return;
        }
        // Ctrl+5 or Ctrl+,: Settings
        else if ((e.Key == Key.D5 || e.Key == Key.OemComma) && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            vm?.NavigateSettingsCommand.Execute(null);
            e.Handled = true;
            return;
        }
        // Ctrl+6: About
        else if (e.Key == Key.D6 && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            vm?.NavigateAboutCommand.Execute(null);
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
