using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;
using PocketMC.Platform.Services;

namespace PocketMC.App
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            using (var host = CreateHostBuilder(args).Build())
            {
                var settingsService = host.Services.GetRequiredService<ISettingsService>();
                Console.WriteLine($"PocketMC config directory: {settingsService.GetSettingsDirectory()}");
                Console.WriteLine($"PocketMC instances directory: {settingsService.GetInstancesDirectory()}");

                var secretStore = host.Services.GetRequiredService<ISecretStore>();
                Console.WriteLine($"Resolved Secret Store type: {secretStore.GetType().Name}");

                // Make a test write/read to show it works
                await secretStore.SetAsync("bootstrap_test_key", "TestSecretValue123");
                var secretVal = await secretStore.GetAsync("bootstrap_test_key");
                Console.WriteLine($"Secret Store test retrieve: {secretVal}");

                await secretStore.DeleteAsync("bootstrap_test_key");
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
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
                });
    }
}
