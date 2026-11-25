using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PocketMC.App.Services;
using PocketMC.App.ViewModels;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests;

public sealed class OllamaAndSettingsParityTests
{
    [Fact]
    public async Task OllamaService_CheckDaemonHealthAndListModels_ParsesApiResponses()
    {
        var handler = new StubHttpHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath == "/api/version")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"version":"0.5.12"}""", Encoding.UTF8, "application/json")
                };
            }

            if (req.RequestUri.AbsolutePath == "/api/tags")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        {
                          "models": [
                            {
                              "name": "llama3.2:3b",
                              "model": "llama3.2:3b",
                              "size": 2147483648,
                              "details": { "parameter_size": "3.2B", "quantization_level": "Q4_K_M", "family": "llama" }
                            }
                          ]
                        }
                        """, Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var http = new HttpClient(handler);
        var service = new OllamaService(http);

        OllamaDaemonStatus health = await service.CheckDaemonHealthAsync("http://localhost:11434/api/tags");
        Assert.True(health.IsRunning);
        Assert.Equal("0.5.12", health.Version);

        var models = await service.GetInstalledModelsAsync("http://localhost:11434");
        Assert.Single(models);
        Assert.Equal("llama3.2:3b", models[0].Name);
        Assert.Equal("2.00 GB", models[0].FormattedSize);
    }

    [Fact]
    public void AppSettingsViewModel_ComputesDiscordPreviewAndSavesExtendedSettings()
    {
        var settingsService = new InMemorySettingsService();
        var vm = new AppSettingsViewModel(
            settingsService,
            new AccentColorService(settingsService),
            new WallpaperBackdropService(settingsService),
            new SettingsBackupService());

        vm.DiscordRpcShowServerName = true;
        vm.DiscordRpcShowServerAddress = true;
        vm.DiscordRpcShowVersion = false;
        vm.EnableTelemetry = false;
        vm.AutoSummarizeOnStop = false;
        vm.AutoSummarizeOnCrash = true;
        vm.OllamaMode = "Cloud";

        Assert.Equal("Survival SMP", vm.PreviewServerTitle);
        Assert.Equal("Playing Minecraft", vm.PreviewActivityLine1);
        Assert.Contains("play.example.ply.gg", vm.PreviewActivityLine2);

        vm.SaveSettingsCommand.Execute(null);
        Assert.False(settingsService.Settings.EnableTelemetry);
        Assert.False(settingsService.Settings.AutoSummarizeOnStop);
        Assert.True(settingsService.Settings.AutoSummarizeOnCrash);
        Assert.Equal("Cloud", settingsService.Settings.OllamaMode);
    }

    private sealed class InMemorySettingsService : ISettingsService
    {
        public Settings Settings { get; } = new();
        public void Load() { }
        public void Save() { }
        public string GetSettingsDirectory() => Path.GetTempPath();
        public string GetDataRoot() => Path.GetTempPath();
        public string GetDefaultDataRoot() => Path.GetTempPath();
        public void SetDataRoot(string path) { }
        public string GetInstancesDirectory() => Path.GetTempPath();
        public string GetBackupsDirectory() => Path.GetTempPath();
        public string GetDownloadsDirectory() => Path.GetTempPath();
        public string GetCacheDirectory() => Path.GetTempPath();
        public string GetLogsDirectory() => Path.GetTempPath();
    }

    private sealed class StubHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }
}
