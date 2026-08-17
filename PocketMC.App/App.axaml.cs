using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using PocketMC.App.Views;

namespace PocketMC.App
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; set; } = null!;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var settingsService = Services.GetRequiredService<PocketMC.Core.Services.ISettingsService>();

                if (!settingsService.Settings.HasCompletedInitialSetup)
                {
                    var setupWindow = Services.GetRequiredService<RootDirectorySetupWindow>();
                    var setupVm = Services.GetRequiredService<PocketMC.App.ViewModels.RootDirectorySetupViewModel>();
                    setupWindow.DataContext = setupVm;
                    desktop.MainWindow = setupWindow;

                    setupVm.SetupCompleted += () =>
                    {
                        var mainWindow = Services.GetRequiredService<MainWindow>();
                        desktop.MainWindow = mainWindow;
                        mainWindow.Show();
                        setupWindow.Close();
                    };
                }
                else
                {
                    desktop.MainWindow = Services.GetRequiredService<MainWindow>();
                }

                try
                {
                    var playitClient = Services.GetRequiredService<PocketMC.RemoteControl.Tunnels.PlayitApiClient>();
                    var tunnelManager = Services.GetRequiredService<PocketMC.RemoteControl.Tunnels.RemoteTunnelManager>();
                    if (playitClient.HasPartnerConnection())
                    {
                        _ = tunnelManager.StartTunnelAsync("playit-https");
                    }
                }
                catch { }
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
