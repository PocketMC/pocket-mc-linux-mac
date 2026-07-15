using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using PocketMC.App.ViewModels;

namespace PocketMC.App.Views
{
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

        // Parameterless constructor for XAML compiler / designer
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

        private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            BeginMoveDrag(e);
        }
    }
}
