using System;
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

public class MarketplaceParityTests
{
    [Fact]
    public async Task MarketplaceViewModel_InitializeForMaps_SetsMapModeFiltersAndCategories()
    {
        var settingsService = new TestMarketplaceSettingsService();
        var cfDialog = new StubCurseForgeKeyDialog();
        var httpClient = new HttpClient(new StubEmptyHttpHandler());
        var curseForge = new CurseForgeService(settingsService, httpClient, cfDialog);
        var modrinth = new ModrinthService(httpClient);
        var manifest = new AddonManifestService();
        var resolver = new DependencyResolverService(manifest);

        var vm = new MarketplaceViewModel(curseForge, modrinth, manifest, resolver, httpClient, settingsService);
        var instance = new ServerInstance
        {
            Name = "SurvivalSMP",
            Slug = "survivalsmp",
            Path = System.IO.Path.GetTempPath(),
            EngineType = EngineType.Paper,
            EngineVersion = "1.21.4"
        };

        await vm.InitializeForMapsAsync(instance);

        Assert.True(vm.IsMapMode);
        Assert.False(vm.IsModpackMode);
        Assert.Equal("CurseForge", vm.SelectedProvider);
        Assert.Contains("Popularity", vm.MapSortOptions);
        Assert.Contains("Adventure", vm.MapCategories);
        Assert.Equal("CurseForge API Key Required", vm.CurseForgeKeyTitle);
        Assert.True(vm.ShowCurseForgeKeyPanel);
    }

    [Fact]
    public async Task MarketplaceViewModel_ModpacksSortResetAndDependencyConfirmation_BehaveAccurately()
    {
        var settingsService = new TestMarketplaceSettingsService();
        var cfDialog = new StubCurseForgeKeyDialog();
        var httpClient = new HttpClient(new StubEmptyHttpHandler());
        var curseForge = new CurseForgeService(settingsService, httpClient, cfDialog);
        var modrinth = new ModrinthService(httpClient);
        var manifest = new AddonManifestService();
        var resolver = new DependencyResolverService(manifest);

        var vm = new MarketplaceViewModel(curseForge, modrinth, manifest, resolver, httpClient, settingsService);
        await vm.InitializeForModpacksAsync();

        Assert.True(vm.IsModpackMode);
        Assert.Contains("Fabric", vm.ModpackLoaders);
        Assert.Equal("Modpacks", vm.SelectedType);

        vm.SearchQuery = "better";
        vm.SelectedSort = "Top Downloads";
        await vm.ResetFiltersCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, vm.SearchQuery);
        Assert.Equal("Trending", vm.SelectedSort);
        Assert.Equal("Any", vm.SelectedModpackLoader);

        var requiredOk = new ResolvedDependency
        {
            ProjectId = "mod1",
            ProjectTitle = "Mod 1",
            Type = DependencyType.Required,
            IsSelected = true
        };
        var confOk = new PocketMC.App.ViewModels.Marketplace.DependencyConfirmationViewModel(new[] { requiredOk });
        Assert.True(confOk.CanInstall);

        requiredOk.IsSelected = false;
        Assert.False(confOk.CanInstall);

        var requiredErr = new ResolvedDependency
        {
            ProjectId = "mod2",
            ProjectTitle = "Mod 2",
            Type = DependencyType.Required,
            IsSelected = true,
            Error = "Incompatible version"
        };
        var confErr = new PocketMC.App.ViewModels.Marketplace.DependencyConfirmationViewModel(new[] { requiredErr });
        Assert.True(confErr.HasFailedRequiredDependency);
        Assert.True(confErr.HasErrors);
        Assert.False(confErr.CanInstall);
    }

    private sealed class StubCurseForgeKeyDialog : ICurseForgeApiKeyDialogService
    {
        public Task<string?> PromptForApiKeyAsync() => Task.FromResult<string?>(null);
    }

    private sealed class StubEmptyHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"hits\":[],\"data\":[]}")
            });
        }
    }

    private sealed class TestMarketplaceSettingsService : ISettingsService
    {
        public Settings Settings { get; } = new Settings();
        public string GetSettingsDirectory() => System.IO.Path.GetTempPath();
        public string GetDataRoot() => System.IO.Path.GetTempPath();
        public string GetDefaultDataRoot() => System.IO.Path.GetTempPath();
        public void SetDataRoot(string path) => Settings.CustomDataRoot = path;
        public string GetInstancesDirectory() => System.IO.Path.GetTempPath();
        public string GetBackupsDirectory() => System.IO.Path.GetTempPath();
        public string GetDownloadsDirectory() => System.IO.Path.GetTempPath();
        public string GetCacheDirectory() => System.IO.Path.GetTempPath();
        public string GetLogsDirectory() => System.IO.Path.GetTempPath();
        public void Load() { }
        public void Save() { }
    }
}
