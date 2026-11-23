using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PocketMC.Core.Models;
using PocketMC.Core.Services;
using PocketMC.App.Services;

namespace PocketMC.App.ViewModels.Settings
{
    public partial class SettingsGeneralVM : ObservableObject
    {
        private string _serverDir = string.Empty;
        private readonly IDialogService _dialogService;
        private readonly IImageProcessingService _imageProcessingService;
        private readonly Action _markDirty;

        [ObservableProperty]
        private string _motd = "A Minecraft Server";

        [ObservableProperty]
        private string _instanceName = string.Empty;

        [ObservableProperty]
        private string _instanceDescription = string.Empty;

        [ObservableProperty]
        private bool _autoStartWithApp;

        [ObservableProperty]
        private int _serverPort = 25565;

        [ObservableProperty]
        private string _geyserBedrockPort = "19132";

        [ObservableProperty]
        private string _pumpkinBedrockPort = "19132";

        [ObservableProperty]
        private string _serverIp = string.Empty;

        [ObservableProperty]
        private Bitmap? _serverIcon;

        public IAsyncRelayCommand BrowseIconCommand { get; }

        public SettingsGeneralVM(
            string serverDir,
            IDialogService dialogService,
            IImageProcessingService imageProcessingService,
            Action markDirty)
        {
            _serverDir = serverDir;
            _dialogService = dialogService;
            _imageProcessingService = imageProcessingService;
            _markDirty = markDirty;

            BrowseIconCommand = new AsyncRelayCommand(BrowseIconAsync);
        }

        public void UpdateServerDir(string newDir)
        {
            _serverDir = newDir;
            LoadIcon();
        }

        public void LoadIcon()
        {
            if (string.IsNullOrEmpty(_serverDir))
            {
                ServerIcon = null;
                return;
            }

            var iconPath = Path.Combine(_serverDir, "server-icon.png");
            if (File.Exists(iconPath))
            {
                try
                {
                    using var stream = File.OpenRead(iconPath);
                    ServerIcon = new Bitmap(stream);
                }
                catch
                {
                    ServerIcon = null;
                }
            }
            else
            {
                ServerIcon = null;
            }
        }

        public async Task BrowseIconAsync()
        {
            var file = await _dialogService.OpenFileDialogAsync("Select Server Icon", "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp");
            if (!string.IsNullOrEmpty(file) && File.Exists(file))
            {
                try
                {
                    byte[] fileBytes = await File.ReadAllBytesAsync(file);
                    // Crop and resize to 64x64 standard server icon
                    byte[] croppedBytes = _imageProcessingService.CropAndResizeImage(fileBytes, 0, 0, 0, 64);
                    
                    if (!string.IsNullOrEmpty(_serverDir))
                    {
                        var targetPath = Path.Combine(_serverDir, "server-icon.png");
                        await File.WriteAllBytesAsync(targetPath, croppedBytes);
                    }

                    using var ms = new MemoryStream(croppedBytes);
                    ServerIcon = new Bitmap(ms);
                    _markDirty();
                }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage("Image Error", $"Failed to process image: {ex.Message}", DialogType.Error);
                }
            }
        }

        partial void OnMotdChanged(string value) => _markDirty();
        partial void OnInstanceNameChanged(string value) => _markDirty();
        partial void OnInstanceDescriptionChanged(string value) => _markDirty();
        partial void OnAutoStartWithAppChanged(bool value) => _markDirty();
        partial void OnServerPortChanged(int value) => _markDirty();
        partial void OnGeyserBedrockPortChanged(string value) => _markDirty();
        partial void OnPumpkinBedrockPortChanged(string value) => _markDirty();
        partial void OnServerIpChanged(string value) => _markDirty();
    }
}
