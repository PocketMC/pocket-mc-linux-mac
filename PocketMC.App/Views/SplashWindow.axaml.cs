using Avalonia.Controls;
using Avalonia.Threading;
using PocketMC.Infrastructure.Configuration;

namespace PocketMC.App.Views;

public partial class SplashWindow : Window
{
    private static SplashWindow? _instance;

    public SplashWindow()
    {
        InitializeComponent();
        Title = AppConfig.AppName;
    }

    public static void ShowSplash()
    {
        if (_instance != null)
        {
            return;
        }

        _instance = new SplashWindow();
        _instance.Show();
    }

    public static void CloseSplash()
    {
        var window = _instance;
        if (window == null)
        {
            return;
        }

        _instance = null;
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                window.Close();
            }
            catch
            {
                // Window may already be closing
            }
        });
    }
}
