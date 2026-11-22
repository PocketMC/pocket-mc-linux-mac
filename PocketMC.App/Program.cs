using System;
using System.Linq;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;
using PocketMC.Platform.Services;
using PocketMC.Platform.Lifecycle;
using PocketMC.Platform.Notifications;
using PocketMC.Platform.Integrations;
using PocketMC.Infrastructure.Power;
using PocketMC.App.Navigation;
using PocketMC.App.Services;
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
                    services.AddSingleton<IRebootScheduleService, RebootScheduleService>();
                    services.AddSingleton<ServerRebootSchedulerService>();
                    services.AddSingleton<IConsoleLogService, ConsoleLogService>();
                    services.AddSingleton<InstanceOperationCoordinator>();
                    services.AddSingleton<IProcessRunner, ProcessRunner>();
                    services.AddSingleton<IPlayerService, PlayerService>();

                    // New Phase 3 infrastructure services
                    services.AddSingleton<ProcessMetricsTracker>();
                    services.AddSingleton<ThemeManager>();

                    // New Phase 4 Infrastructure services (Backups & Marketplace)
                    services.AddSingleton<System.Net.Http.HttpClient>();
                    services.AddSingleton<DownloaderService>();
                    services.AddSingleton<IGeyserDetector, GeyserDetector>();
                    services.AddSingleton<GeyserProvisioningService>();
                    services.AddSingleton<ISimpleVoiceChatDetector, SimpleVoiceChatDetector>();
                    services.AddSingleton<BanSidecarService>();
                    services.AddSingleton<ICurseForgeApiKeyDialogService, PocketMC.App.Services.CurseForgeApiKeyDialogService>();
                    services.AddSingleton<CurseForgeService>();
                    services.AddSingleton<ModrinthService>();
                    services.AddSingleton<AddonManifestService>();
                    services.AddSingleton<MarketplaceFileInstaller>();
                    services.AddSingleton<ModpackParser>();
                    services.AddSingleton<AddonUpdateService>();
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

                    // Server Version Updates (Transactional Upgrades & Rollbacks)
                    services.AddSingleton<InstanceVersionTargetService>();
                    services.AddSingleton<InstanceUpdatePlanner>();
                    services.AddSingleton<InstanceArtifactStager>();
                    services.AddSingleton<InstanceUpdateApplier>();
                    services.AddSingleton<IInstanceUpdateService, InstanceUpdateService>();

                    // Phase 5 Remote Control & Tunnels
                    services.AddSingleton<PocketMC.RemoteControl.Services.RemoteAuthenticationService>();
                    services.AddSingleton<PocketMC.RemoteControl.Services.RemoteRequestLimiter>();
                    services.AddSingleton<PocketMC.RemoteControl.Services.RemoteAuditLogService>(sp => 
                        new PocketMC.RemoteControl.Services.RemoteAuditLogService(sp.GetRequiredService<ISettingsService>().GetLogsDirectory()));
                    services.AddSingleton<PocketMC.Infrastructure.Services.ProcessMetricsTracker>();
                    services.AddSingleton<PocketMC.RemoteControl.Services.RemoteStatusService>();
                    services.AddSingleton<PocketMC.RemoteControl.Services.RemoteInstanceControlService>();
                    services.AddSingleton<PocketMC.RemoteControl.Services.RemotePlayerActionService>();
                    services.AddSingleton<PocketMC.RemoteControl.Hosting.RemoteConsoleWebSocketHandler>();
                    services.AddSingleton<PocketMC.RemoteControl.Hosting.RemoteDashboardHost>();
                    services.AddHostedService<PocketMC.RemoteControl.Hosting.RemoteDashboardHost>(sp => sp.GetRequiredService<PocketMC.RemoteControl.Hosting.RemoteDashboardHost>());
                    services.AddSingleton<PocketMC.RemoteControl.Services.LocalNetworkAddressService>();
                    
                    services.AddSingleton<PocketMC.RemoteControl.Tunnels.IRemoteTunnelProvider, PocketMC.RemoteControl.Tunnels.CloudflaredQuickTunnelProvider>();
                    services.AddSingleton<PocketMC.RemoteControl.Tunnels.IRemoteTunnelProvider, PocketMC.RemoteControl.Tunnels.PlayitHttpsTunnelProvider>();
                    services.AddSingleton<PocketMC.RemoteControl.Tunnels.PlayitApiClient>();
                    services.AddSingleton<PocketMC.RemoteControl.Tunnels.RemoteTunnelManager>();

                    services.AddSingleton<INativeMetricsProvider, PocketMC.Platform.Metrics.NativeMetricsProvider>();
                    services.AddSingleton<DashboardMetricsViewModel>();
                    services.AddSingleton<DashboardInstanceListViewModel>();
                    services.AddSingleton<DashboardActionsViewModel>(sp => new DashboardActionsViewModel(
                        sp.GetRequiredService<IProcessRunner>(),
                        sp.GetRequiredService<IInstanceService>(),
                        sp.GetRequiredService<IDialogService>()));

                    // ViewModels & Views
                    services.AddSingleton<IAppNavigationService, AppNavigationService>();
                    services.AddSingleton<IDialogService, AvaloniaDialogService>();
                    services.AddSingleton<AccentColorService>();
                    services.AddSingleton<WallpaperBackdropService>();
                    services.AddSingleton<ISleepPreventionService, SleepPreventionService>();
                    services.AddSingleton<ServerSleepPreventionCoordinator>(sp =>
                        new ServerSleepPreventionCoordinator(
                            sp.GetRequiredService<ISleepPreventionService>(),
                            () => sp.GetRequiredService<ISettingsService>().Settings,
                            () => sp.GetRequiredService<IProcessRunner>().GetRunningSlugs().Count,
                            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ServerSleepPreventionCoordinator>>(),
                            sp.GetService<IProcessRunner>()));
                    services.AddSingleton<ISingleInstanceService, SingleInstanceService>();
                    services.AddSingleton<INotificationService, NativeNotificationService>();
                    services.AddSingleton<IDiscordRpcService, DiscordRpcService>();
                    services.AddSingleton<SettingsBackupService>();
                    services.AddSingleton<PocketMC.Core.Services.IPosixPortOwnerResolver, PocketMC.Platform.Networking.PosixPortOwnerResolver>();
                    services.AddSingleton<PocketMC.Core.Services.IPosixFilePermissionService, PocketMC.Platform.Services.PosixFilePermissionService>();
                    services.AddSingleton<PocketMC.Infrastructure.Networking.PortLeaseRegistry>();
                    services.AddSingleton<PocketMC.Infrastructure.Networking.PortProbeService>();
                    services.AddSingleton<PocketMC.Infrastructure.Networking.PortRecoveryService>();

                    services.AddSingleton<MainWindowViewModel>();
                    services.AddTransient<MainWindow>();
                    services.AddTransient<AppSettingsViewModel>();
                    services.AddTransient<AppSettingsPage>();
                    services.AddSingleton<DashboardViewModel>();
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
                    services.AddTransient<PocketMC.App.ViewModels.PortsMapViewModel>();
                    services.AddTransient<PocketMC.App.Views.PortsMapPage>();
                    services.AddSingleton<PocketMC.Infrastructure.WhatsNew.WhatsNewService>();
                    services.AddTransient<PocketMC.App.Views.WhatsNewWindow>();
                    services.AddTransient<JavaManagementViewModel>();
                    services.AddTransient<JavaManagementPage>();
                    services.AddTransient<AboutViewModel>();
                    services.AddTransient<AboutPage>();
                    services.AddTransient<PocketMC.App.ViewModels.Instances.InstanceImportViewModel>();
                    services.AddTransient<PocketMC.App.Views.Instances.InstanceExportPage>();
                    services.AddTransient<PocketMC.App.Views.Instances.InstanceImportPage>();
                    services.AddTransient<PocketMC.App.Views.Marketplace.PluginBrowserPage>();
                    services.AddTransient<PocketMC.App.Views.Marketplace.MapBrowserPage>();

                    services.AddTransient<RootDirectorySetupViewModel>();
                    services.AddTransient<RootDirectorySetupWindow>();
                });
    }
}

