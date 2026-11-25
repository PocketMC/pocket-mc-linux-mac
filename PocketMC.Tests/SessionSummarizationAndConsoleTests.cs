using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PocketMC.App.ViewModels;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests;

public class SessionSummarizationAndConsoleTests
{
    [Fact]
    public void SessionLogPreprocessor_FiltersNoiseRedactsIpsAndDeduplicates()
    {
        string rawLog = string.Join(Environment.NewLine, new[]
        {
            "[12:00:00 INFO]: Starting minecraft server version 1.21.4",
            "[12:00:01 INFO]: Loading properties",
            "[12:00:02 INFO]: Preparing spawn area: 0%",
            "[12:00:02 INFO]: Preparing spawn area: 50%",
            "[12:00:03 INFO]: Done (2.1s)! For help, type \"help\"",
            "[12:00:10 INFO]: Steve[/192.168.1.55:54321] logged in with entity id 12 at (10, 64, -5)",
            "[12:00:11 WARN]: Can't keep up! Is the server overloaded? Running 2050ms behind",
            "[12:00:12 WARN]: Can't keep up! Is the server overloaded? Running 2050ms behind",
            "[12:00:15 INFO]: Steve lost connection: Disconnected",
            "[12:00:20 INFO]: Stopping server"
        });

        string? processed = SessionLogPreprocessor.Preprocess(rawLog);

        Assert.NotNull(processed);
        Assert.DoesNotContain("192.168.1.55", processed);
        Assert.Contains("[REDACTED_IP]", processed);
        Assert.DoesNotContain("Preparing spawn area", processed);
        Assert.Contains("Starting minecraft server version 1.21.4", processed);
        Assert.Contains("[x2]", processed);
    }

    [Fact]
    public void SummaryEmojiFormatter_DecoratesHeadingsWithoutDuplicating()
    {
        string input = "## Minecraft Server Session Summary\n\n### Important Events\n- Started cleanly\n\n**Recommendations**\n- Keep TPS high";
        string formatted = SummaryEmojiFormatter.Apply(input);
        string secondPass = SummaryEmojiFormatter.Apply(formatted);

        Assert.Contains("## 🎮 Minecraft Server Session Summary", formatted);
        Assert.Contains("### 🔑 Important Events", formatted);
        Assert.Contains("**✅ Recommendations**", formatted);
        Assert.Equal(formatted, secondPass);
    }

    [Fact]
    public async Task SessionSummarizationService_GeneratesFormatsAndSavesSummary()
    {
        string serverDir = Path.Combine(Path.GetTempPath(), "pmc_summary_" + Guid.NewGuid().ToString("N"));
        string logsDir = Path.Combine(serverDir, "logs");
        Directory.CreateDirectory(logsDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(logsDir, "pocketmc-latest.log"), string.Join(Environment.NewLine, new[]
            {
                "[12:00:00 INFO]: Starting minecraft server version 1.21.4",
                "[12:00:01 INFO]: Loading properties",
                "[12:00:02 INFO]: Done (1.2s)! For help, type \"help\"",
                "[12:00:05 INFO]: Alex joined the game",
                "[12:00:10 INFO]: Alex left the game",
                "[12:00:15 INFO]: Stopping server"
            }));

            var handler = new InlineStubHttpHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"## Minecraft Server Session Summary\\n### Important Events\\n- Normal run\"}}]}")
            }));

            var llmFactory = new LlmProviderFactory(new HttpClient(handler), new MemorySecretStore());
            var storage = new SummaryStorageService();
            var service = new SessionSummarizationService(llmFactory, storage);

            var settings = new Settings
            {
                AiProvider = "OpenAI",
                EnableAiSummarization = true,
                AiApiKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["OpenAI"] = "test-key" }
            };

            string? startedDir = null;
            SummarizationResult? completedResult = null;
            service.SummarizationStarted += dir => startedDir = dir;
            service.SummarizationCompleted += (_, res) => completedResult = res;

            SummarizationResult result = await service.SummarizeAsync(
                serverDir,
                "SurvivalBox",
                settings,
                DateTime.UtcNow.AddMinutes(-15),
                DateTime.UtcNow);

            Assert.True(result.Success, result.Error);
            Assert.Equal(serverDir, startedDir);
            Assert.NotNull(completedResult);
            Assert.Contains("🎮 Minecraft Server Session Summary", result.Summary!.Content);
            Assert.Contains("🔑 Important Events", result.Summary.Content);

            SessionSummary? latest = service.GetLatestSummary(serverDir);
            Assert.NotNull(latest);
            Assert.Equal("SurvivalBox", latest!.ServerName);
        }
        finally
        {
            if (Directory.Exists(serverDir))
            {
                Directory.Delete(serverDir, true);
            }
        }
    }

    private sealed class InlineStubHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _fn;
        public InlineStubHttpHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> fn) => _fn = fn;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => _fn(request);
    }

    private sealed class MemorySecretStore : ISecretStore
    {
        private readonly Dictionary<string, string> _map = new();
        public Task<string?> GetAsync(string key) => Task.FromResult(_map.TryGetValue(key, out string? v) ? v : null);
        public Task SetAsync(string key, string value) { _map[key] = value; return Task.CompletedTask; }
        public Task DeleteAsync(string key) { _map.Remove(key); return Task.CompletedTask; }
    }

    [Fact]
    public async Task ServerConsoleViewModel_AiDrawerOpensAndAnalyzesLogSnippet()
    {
        var instService = new FakeInstanceService();
        var instance = new ServerInstance { Name = "Surv", Slug = "surv", Path = Path.GetTempPath(), MaxPlayers = 20 };
        instService.Instances.Add(instance);

        var logService = new FakeConsoleLogService();
        var processRunner = new FakeProcessRunner();
        var handler = new InlineStubHttpHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"### Analysis\\nPort already in use.\"}}]}")
        }));

        var settingsService = new FakeSettingsServiceForConsole();
        settingsService.Settings.AiProvider = "OpenAI";
        settingsService.Settings.AiApiKeys["OpenAI"] = "secret-key";

        var llmFactory = new LlmProviderFactory(new HttpClient(handler), new MemorySecretStore());

        using var vm = new ServerConsoleViewModel(
            instService,
            logService,
            processRunner,
            settingsService: settingsService,
            llmProviderFactory: llmFactory);
        vm.Initialize(instance);

        Assert.False(vm.IsAiPanelOpen);
        Assert.True(vm.IsReadOnlySessionLog);

        await vm.AnalyzeSnippetCommand.ExecuteAsync("java.net.BindException: Address already in use");
        try { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); } catch { }

        Assert.True(vm.IsAiPanelOpen);
        Assert.Contains("Port already in use", vm.AiMarkdown);

        vm.CloseAiPanelCommand.Execute(null);
        Assert.False(vm.IsAiPanelOpen);
    }

    private sealed class FakeSettingsServiceForConsole : ISettingsService
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
}
