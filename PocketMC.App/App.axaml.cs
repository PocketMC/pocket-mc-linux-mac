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

                if (desktop.MainWindow != null)
                {
                    desktop.MainWindow.Loaded += async (s, e) =>
                    {
                        try
                        {
                            var whatsNewService = Services.GetRequiredService<PocketMC.Infrastructure.WhatsNew.WhatsNewService>();
                            if (whatsNewService.ShouldShow())
                            {
                                var whatsNewWin = new WhatsNewWindow(whatsNewService);
                                await whatsNewWin.ShowDialog(desktop.MainWindow);
                            }
                        }
                        catch { }
                    };
                }

                try
                {
                    var rebootScheduler = Services.GetService<PocketMC.Infrastructure.Services.ServerRebootSchedulerService>();
                    rebootScheduler?.Start();

                    var playitClient = Services.GetRequiredService<PocketMC.RemoteControl.Tunnels.PlayitApiClient>();
                    var tunnelManager = Services.GetRequiredService<PocketMC.RemoteControl.Tunnels.RemoteTunnelManager>();
                    if (playitClient.HasPartnerConnection())
                    {
                        _ = tunnelManager.StartTunnelAsync("playit-https");
                    }
                }
                catch { }

                desktop.Exit += async (sender, args) =>
                {
                    try
                    {
                        var rebootScheduler = Services.GetService<PocketMC.Infrastructure.Services.ServerRebootSchedulerService>();
                        rebootScheduler?.Stop();

                        var procRunner = Services.GetService<PocketMC.Core.Services.IProcessRunner>();
                        if (procRunner != null)
                        {
                            await procRunner.StopAllAsync();
                        }

                        var tunnelManager = Services.GetService<PocketMC.RemoteControl.Tunnels.RemoteTunnelManager>();
                        if (tunnelManager != null)
                        {
                            await tunnelManager.StopAllTunnelsAsync();
                        }
                    }
                    catch { }
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void TrayIcon_Clicked(object? sender, EventArgs e)
        {
            RestoreMainWindow();
        }

        private void TrayOpen_Clicked(object? sender, EventArgs e)
        {
            RestoreMainWindow();
        }

        private void TrayQuit_Clicked(object? sender, EventArgs e)
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }

        private void RestoreMainWindow()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                if (!desktop.MainWindow.IsVisible)
                {
                    desktop.MainWindow.Show();
                }
                if (desktop.MainWindow.WindowState == Avalonia.Controls.WindowState.Minimized)
                {
                    desktop.MainWindow.WindowState = Avalonia.Controls.WindowState.Normal;
                }
                desktop.MainWindow.Activate();
            }
        }
    }
}
