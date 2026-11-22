using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.App.Converters;
using PocketMC.App.Services;
using PocketMC.App.ViewModels;

namespace PocketMC.Tests
{
    public class ServerSettingsDecompositionTests
    {
        [Fact]
        public void MinecraftMotdConverter_ConvertsColorCodesToFormattedInlines()
        {
            var converter = new MinecraftMotdConverter();

            // When MOTD is empty, provides default text
            var defaultResult = converter.Convert(string.Empty, typeof(TextBlock), null, System.Globalization.CultureInfo.InvariantCulture) as TextBlock;
            Assert.NotNull(defaultResult);
            Assert.Equal("A Minecraft Server", defaultResult.Text);

            // When MOTD contains legacy color codes §cRed and §aGreen
            var coloredResult = converter.Convert("§cRed §aGreen", typeof(TextBlock), null, System.Globalization.CultureInfo.InvariantCulture) as TextBlock;
            Assert.NotNull(coloredResult);
            Assert.Equal(2, coloredResult.Inlines.Count);

            var run1 = coloredResult.Inlines[0] as Run;
            Assert.NotNull(run1);
            Assert.Equal("Red ", run1.Text);
            var brush1 = run1.Foreground as ISolidColorBrush;
            Assert.NotNull(brush1);
            Assert.Equal(Color.FromRgb(255, 85, 85), brush1.Color);

            var run2 = coloredResult.Inlines[1] as Run;
            Assert.NotNull(run2);
            Assert.Equal("Green", run2.Text);
            var brush2 = run2.Foreground as ISolidColorBrush;
            Assert.NotNull(brush2);
            Assert.Equal(Color.FromRgb(85, 255, 85), brush2.Color);
        }

        [Fact]
        public void ImageProcessingService_CropsAndResizesImageToExact64x64Png()
        {
            var service = new ImageProcessingService();

            // Create a simple 200x200 test image using SkiaSharp
            using var bitmap = new SkiaSharp.SKBitmap(200, 200);
            using var canvas = new SkiaSharp.SKCanvas(bitmap);
            canvas.Clear(SkiaSharp.SKColors.CornflowerBlue);
            using var originalImage = SkiaSharp.SKImage.FromBitmap(bitmap);
            using var originalData = originalImage.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            byte[] inputBytes = originalData.ToArray();

            // Crop a 100x100 center region and scale to 64x64
            byte[] croppedBytes = service.CropAndResizeImage(inputBytes, pxX: 50, pxY: 50, pxSize: 100, targetSize: 64);

            Assert.NotNull(croppedBytes);
            Assert.NotEmpty(croppedBytes);

            // Verify the cropped output is a valid 64x64 PNG
            using var decoded = SkiaSharp.SKBitmap.Decode(croppedBytes);
            Assert.NotNull(decoded);
            Assert.Equal(64, decoded.Width);
            Assert.Equal(64, decoded.Height);
        }

        [Fact]
        public void ServerSettingsViewModel_InitializesAll9ModularSubViewModels()
        {
            var instance = new ServerInstance
            {
                Id = Guid.NewGuid(),
                Name = "Settings Test",
                Slug = "settings-test",
                Path = "/tmp/settings-test",
                EngineType = EngineType.Paper,
                EngineVersion = "1.20.4",
                MaxRamMb = 4096
            };

            var fakeInstanceService = new FakeSettingsInstanceService();
            var fakeSettingsService = new FakeSettingsConfigService();
            var vm = new ServerSettingsViewModel(fakeInstanceService, fakeSettingsService);
            vm.SelectedInstance = instance;

            Assert.NotNull(vm.General);
            Assert.NotNull(vm.World);
            Assert.NotNull(vm.Performance);
            Assert.NotNull(vm.Bedrock);
            Assert.NotNull(vm.Backups);
            Assert.NotNull(vm.Addons);
            Assert.NotNull(vm.VersionUpdates);
            Assert.NotNull(vm.Summaries);
            Assert.NotNull(vm.Advanced);

            Assert.Equal(9, vm.SubViewModelCount);
        }

        private class FakeSettingsInstanceService : IInstanceService
        {
            public Task<ServerInstance> CreateInstanceAsync(string name, EngineType engineType, string version) => throw new NotImplementedException();
            public Task DeleteInstanceAsync(string slug) => throw new NotImplementedException();
            public Task<ServerInstance> RenameInstanceAsync(string slug, string newName) => throw new NotImplementedException();
            public Task<ServerInstance> CloneInstanceAsync(string slug, string newName) => throw new NotImplementedException();
            public Task SaveInstanceAsync(ServerInstance instance) => Task.CompletedTask;
            public Task ExportInstanceAsync(string slug, string targetZipPath) => throw new NotImplementedException();
            public Task<ServerInstance?> GetInstanceAsync(string slug) => Task.FromResult<ServerInstance?>(null);
            public Task<ServerInstance> ImportInstanceAsync(string sourceZipPath, string name) => throw new NotImplementedException();
            public Task<List<ServerInstance>> ListInstancesAsync() => Task.FromResult(new List<ServerInstance>());
        }

        private class FakeSettingsConfigService : ISettingsService
        {
            public Settings Settings { get; set; } = new Settings();
            public void Save() { }
            public void Load() { }
            public string GetSettingsPath() => "/tmp/settings.json";
            public string GetSettingsDirectory() => "/tmp";
            public string GetDataRoot() => "/tmp";
            public string GetDefaultDataRoot() => "/tmp";
            public void SetDataRoot(string path) { }
            public string GetLogsDirectory() => "/tmp/logs";
            public string GetInstancesDirectory() => "/tmp/instances";
            public string GetBackupsDirectory() => "/tmp/backups";
            public string GetDownloadsDirectory() => "/tmp/downloads";
            public string GetCacheDirectory() => "/tmp/cache";
        }
    }
}
