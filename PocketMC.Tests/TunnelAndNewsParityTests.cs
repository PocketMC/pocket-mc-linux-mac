using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using PocketMC.App.ViewModels;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.News;
using PocketMC.Infrastructure.Services;
using PocketMC.RemoteControl.Tunnels;
using Xunit;

namespace PocketMC.Tests;

public class TunnelAndNewsParityTests
{
    [Fact]
    public void CloudflaredQuickTunnelProvider_ResolveDownloadSpec_ReturnsTgzOnMacOsAndBinaryOnLinux()
    {
        var macArm = CloudflaredQuickTunnelProvider.ResolveDownloadSpec(OSPlatform.OSX, Architecture.Arm64);
        Assert.True(macArm.IsTarGz);
        Assert.EndsWith("cloudflared-darwin-arm64.tgz", macArm.Url);

        var macX64 = CloudflaredQuickTunnelProvider.ResolveDownloadSpec(OSPlatform.OSX, Architecture.X64);
        Assert.True(macX64.IsTarGz);
        Assert.EndsWith("cloudflared-darwin-amd64.tgz", macX64.Url);

        var linuxArm = CloudflaredQuickTunnelProvider.ResolveDownloadSpec(OSPlatform.Linux, Architecture.Arm64);
        Assert.False(linuxArm.IsTarGz);
        Assert.EndsWith("cloudflared-linux-arm64", linuxArm.Url);

        var linuxX64 = CloudflaredQuickTunnelProvider.ResolveDownloadSpec(OSPlatform.Linux, Architecture.X64);
        Assert.False(linuxX64.IsTarGz);
        Assert.EndsWith("cloudflared-linux-amd64", linuxX64.Url);
    }

    [Fact]
    public async Task PlayitStatusService_ParsesNetworkMonitorsAndStatusViewModelLoadsThem()
    {
        var http = new HttpClient(new StubStatusHttpHandler());
        var service = new PlayitStatusService(http);
        var vm = new PlayitStatusViewModel(service);

        await vm.LoadStatusAsync();

        Assert.Equal(3, vm.Monitors.Count);
        Assert.Equal("US East", vm.Monitors[0].Name);
        Assert.Equal("Operational", vm.Monitors[0].StatusText);
        Assert.Equal("warning", vm.Monitors[1].StatusClass);
        Assert.Equal("Outage", vm.Monitors[2].StatusText);
        Assert.Contains("Last updated:", vm.LastUpdated);

        Assert.NotNull(typeof(PlayitApiClient).GetMethod(nameof(PlayitApiClient.RenameTunnelAsync)));
    }

    [Fact]
    public void NewsContentRenderer_Build_RendersRichBlocksForListsCodeWarningAndLinks()
    {
        var metadata = new PocketMC.Infrastructure.News.NewsMetadata(
            Id: "release-note",
            Type: PocketMC.Infrastructure.News.NewsType.Announcement,
            Priority: PocketMC.Infrastructure.News.NewsPriority.Important,
            Popup: false,
            PublishedUtc: System.DateTimeOffset.UtcNow,
            ExpiresUtc: null,
            MinimumVersion: null,
            MaximumVersion: null);

        var blocks = new PocketMC.Infrastructure.News.NewsContentBlock[]
        {
            new(PocketMC.Infrastructure.News.NewsBlockType.Title, "Major Update"),
            new(PocketMC.Infrastructure.News.NewsBlockType.Subtitle, "Highlights"),
            new(PocketMC.Infrastructure.News.NewsBlockType.Heading, "Changes"),
            new(PocketMC.Infrastructure.News.NewsBlockType.Paragraph, "Overview paragraph"),
            new(PocketMC.Infrastructure.News.NewsBlockType.BulletList, Items: new[] { "First bullet", "Second bullet" }),
            new(PocketMC.Infrastructure.News.NewsBlockType.NumberedList, Items: new[] { "Step one", "Step two" }),
            new(PocketMC.Infrastructure.News.NewsBlockType.Warning, "Backup before upgrading."),
            new(PocketMC.Infrastructure.News.NewsBlockType.Important, "Restart required."),
            new(PocketMC.Infrastructure.News.NewsBlockType.Code, "dotnet test"),
            new(PocketMC.Infrastructure.News.NewsBlockType.Link, "Release Notes", Link: new System.Uri("https://example.com")),
            new(PocketMC.Infrastructure.News.NewsBlockType.Divider)
        };

        var item = new PocketMC.Infrastructure.News.NewsItem("note.txt", "", metadata, blocks);
        var panel = PocketMC.App.Controls.NewsContentRenderer.Build(item);

        Assert.Equal(11, panel.Children.Count);

        var bodyOnlyPanel = PocketMC.App.Controls.NewsContentRenderer.Build(item, includeTitle: false);
        Assert.Equal(10, bodyOnlyPanel.Children.Count);
    }

    [Fact]
    public void NewsService_And_MainWindowViewModel_FilterPopupAndTrackUnreadBadge()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"pocketmc-news-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TempSettingsService(tempRoot);
            using var newsService = new NewsService(settingsService, new HttpClient());

            File.WriteAllText(Path.Combine(newsService.CacheDirectory, "01-popup.txt"), """
            ---
            id: popup-01
            type: announcement
            priority: important
            popup: true
            published: 2026-01-01T00:00:00Z
            minVersion: 1.0.0
            maxVersion: 3.0.0
            ---

            title: Welcome Beta Users
            paragraph: Read carefully.
            """);

            File.WriteAllText(Path.Combine(newsService.CacheDirectory, "02-expired.txt"), """
            ---
            id: expired-02
            type: update
            priority: normal
            popup: true
            published: 2019-01-01T00:00:00Z
            expires: 2020-01-01T00:00:00Z
            ---

            title: Expired
            paragraph: Expired item.
            """);

            File.WriteAllText(Path.Combine(newsService.CacheDirectory, "03-out-of-range.txt"), """
            ---
            id: future-03
            type: update
            priority: normal
            popup: true
            published: 2026-01-03T00:00:00Z
            minVersion: 9.0.0
            ---

            title: Future Version
            paragraph: Not applicable yet.
            """);

            var pending = newsService.GetPendingPopupNews("2.1.0");
            Assert.Single(pending);
            Assert.Equal("popup-01", pending[0].Metadata.Id);

            var services = new ServiceCollection();
            services.AddSingleton(newsService);
            services.AddSingleton<ISettingsService>(settingsService);
            services.AddSingleton<PocketMC.App.Navigation.IAppNavigationService, PocketMC.App.Navigation.AppNavigationService>();
            using var provider = services.BuildServiceProvider();

            using var mainVm = new MainWindowViewModel(
                provider.GetRequiredService<PocketMC.App.Navigation.IAppNavigationService>(),
                provider,
                settingsService,
                null!);

            Assert.Equal(2, mainVm.UnreadNewsCount);
            Assert.True(mainVm.HasUnreadNews);

            newsService.MarkRead("popup-01");
            Assert.Empty(newsService.GetPendingPopupNews("2.1.0"));
            Assert.Equal(1, mainVm.UnreadNewsCount);
            Assert.True(mainVm.HasUnreadNews);

            newsService.MarkRead("future-03");
            Assert.Equal(0, mainVm.UnreadNewsCount);
            Assert.False(mainVm.HasUnreadNews);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private sealed class TempSettingsService : ISettingsService
    {
        private readonly string _root;
        public TempSettingsService(string root) => _root = root;
        public Settings Settings { get; } = new();
        public void Load() { }
        public void Save() { }
        public string GetSettingsDirectory() => _root;
        public string GetDataRoot() => _root;
        public string GetDefaultDataRoot() => _root;
        public void SetDataRoot(string path) { }
        public string GetInstancesDirectory() => Path.Combine(_root, "instances");
        public string GetBackupsDirectory() => Path.Combine(_root, "backups");
        public string GetDownloadsDirectory() => Path.Combine(_root, "downloads");
        public string GetCacheDirectory() => Path.Combine(_root, "cache");
        public string GetLogsDirectory() => Path.Combine(_root, "logs");
    }

    private sealed class StubStatusHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "status": "ok",
                  "networks": [
                    { "name": "US East", "status": "online" },
                    { "name": "EU West", "status": "re-routed" },
                    { "name": "AP South", "status": "offline" }
                  ]
                }
                """)
            });
        }
    }
}
