using System;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;
using PocketMC.Platform.Services;
using PocketMC.App.ViewModels;
using PocketMC.App.Views;

using System.Threading.Tasks;

namespace PocketMC.App
{
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // 1. Initialize and configure the Generic Host
            using var host = CreateHostBuilder(args).Build();

            // 2. Start the hosted background services (non-blocking)
            host.Start();

            // 3. Expose the service provider to Avalonia
            App.Services = host.Services;

            try
            {
                // 4. Run the Avalonia Application Lifecycle
                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
            finally
            {
                // 5. Gracefully stop and clean up Generic Host services
                try
                {
                    var stopTask = Task.Run(async () =>
                    {
                        using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(2)))
                        {
                            await host.StopAsync(cts.Token);
                        }
                    });
                    
                    stopTask.Wait(TimeSpan.FromSeconds(3));
                }
                catch {}

                // Force terminate the process to release all sockets and stdout handles
                Environment.Exit(0);
            }
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    // Existing infrastructure services
                    services.AddSingleton<ISettingsService, SettingsService>();
                    services.AddSingleton<ISecretStore>(sp => 
                        SecretStoreFactory.Create(sp.GetRequiredService<ISettingsService>()));
                    services.AddSingleton<IJavaService, JavaService>();
                    services.AddSingleton<IPHPService, PHPService>();
                    services.AddSingleton<IPreLaunchVerifier, PreLaunchVerifier>();
                    services.AddSingleton<IInstanceService, InstanceService>();
                    services.AddSingleton<IConsoleLogService, ConsoleLogService>();
                    services.AddSingleton<IProcessRunner, ProcessRunner>();
                    services.AddSingleton<IPlayerService, PlayerService>();

                    // New Phase 3 infrastructure services
                    services.AddSingleton<ProcessMetricsTracker>();
                    services.AddSingleton<ThemeManager>();

                    // New Phase 4 Infrastructure services (Backups & Marketplace)
                    services.AddSingleton<System.Net.Http.HttpClient>();
                    services.AddSingleton<ICurseForgeApiKeyDialogService, PocketMC.App.Services.CurseForgeApiKeyDialogService>();
                    services.AddSingleton<CurseForgeService>();
                    services.AddSingleton<ModrinthService>();
                    services.AddSingleton<AddonManifestService>();
                    services.AddSingleton<DependencyResolverService>();
                    services.AddSingleton<CloudBackupUploadHistoryStore>();
                    services.AddSingleton<ICloudBackupProvider, GoogleDriveBackupProvider>();
                    services.AddSingleton<ICloudBackupProvider, OneDriveBackupProvider>();
                    services.AddSingleton<ICloudBackupProvider, DropboxBackupProvider>();
                    services.AddSingleton<CloudBackupService>();
                    services.AddSingleton<IBackupService, BackupService>();

                    // New Phase 4 AI Diagnostics
                    services.AddSingleton<ILogRedactionService, LogRedactionService>();
                    services.AddSingleton<LlmProviderFactory>();
                    services.AddSingleton<IAiDiagnosticService, AiDiagnosticService>();

                    // Phase 5 Remote Control & Tunnels
                    services.AddSingleton<PocketMC.RemoteControl.Services.RemoteAuthenticationService>();
                    services.AddSingleton<PocketMC.RemoteControl.Services.RemoteRequestLimiter>();
                    services.AddSingleton<PocketMC.RemoteControl.Services.RemoteAuditLogService>(sp => 
                        new PocketMC.RemoteControl.Services.RemoteAuditLogService(sp.GetRequiredService<ISettingsService>().GetLogsDirectory()));
                    services.AddSingleton<PocketMC.RemoteControl.Hosting.RemoteConsoleWebSocketHandler>();
                    services.AddSingleton<PocketMC.RemoteControl.Hosting.RemoteDashboardHost>();
                    services.AddHostedService<PocketMC.RemoteControl.Hosting.RemoteDashboardHost>(sp => sp.GetRequiredService<PocketMC.RemoteControl.Hosting.RemoteDashboardHost>());
                    services.AddSingleton<PocketMC.RemoteControl.Services.LocalNetworkAddressService>();
                    
                    services.AddSingleton<PocketMC.RemoteControl.Tunnels.IRemoteTunnelProvider, PocketMC.RemoteControl.Tunnels.CloudflaredQuickTunnelProvider>();
                    services.AddSingleton<PocketMC.RemoteControl.Tunnels.IRemoteTunnelProvider, PocketMC.RemoteControl.Tunnels.PlayitHttpsTunnelProvider>();
                    services.AddSingleton<PocketMC.RemoteControl.Tunnels.PlayitApiClient>();
                    services.AddSingleton<PocketMC.RemoteControl.Tunnels.RemoteTunnelManager>();

                    // ViewModels & Views
                    services.AddSingleton<MainWindowViewModel>();
                    services.AddTransient<MainWindow>();
                    services.AddTransient<DashboardViewModel>();
                    services.AddTransient<DashboardView>();
                    services.AddTransient<NewInstanceViewModel>();
                    services.AddTransient<NewInstancePage>();
                    services.AddTransient<ServerConsoleViewModel>();
                    services.AddTransient<ServerConsolePage>();
                    services.AddTransient<ServerSettingsViewModel>();
                    services.AddTransient<ServerSettingsPage>();
                    services.AddTransient<PlayerManagementViewModel>();
                    services.AddTransient<PlayerManagementPage>();
                    services.AddTransient<ServerBackupsViewModel>();
                    services.AddTransient<ServerBackupsPage>();
                    services.AddTransient<MarketplaceViewModel>();
                    services.AddTransient<MarketplacePage>();
                    services.AddTransient<AiDiagnosticViewModel>();
                    services.AddTransient<AiDiagnosticPage>();
                    services.AddTransient<PocketMC.App.ViewModels.RemoteControlViewModel>();
                    services.AddTransient<PocketMC.App.Views.RemoteControlPage>();
                    services.AddTransient<PocketMC.App.ViewModels.TunnelViewModel>();
                    services.AddTransient<PocketMC.App.Views.TunnelPage>();
                });
    }
}

