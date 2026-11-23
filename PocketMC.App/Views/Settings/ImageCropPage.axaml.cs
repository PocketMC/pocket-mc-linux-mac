using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using PocketMC.App.Navigation;
using PocketMC.Core.Services;

namespace PocketMC.App.Views.Settings
{
    public partial class ImageCropPage : UserControl
    {
        private readonly string? _sourceImagePath;
        private readonly string? _targetIconPath;
        private readonly IImageProcessingService? _imageProcessor;
        private readonly IAppNavigationService? _navigationService;
        private readonly Action? _onCompleted;

        public ImageCropPage()
        {
            InitializeComponent();
        }

        public ImageCropPage(
            string sourceImagePath,
            string targetIconPath,
            IImageProcessingService imageProcessor,
            IAppNavigationService navigationService,
            Action? onCompleted = null)
        {
            InitializeComponent();
            _sourceImagePath = sourceImagePath;
            _targetIconPath = targetIconPath;
            _imageProcessor = imageProcessor;
            _navigationService = navigationService;
            _onCompleted = onCompleted;

            if (File.Exists(sourceImagePath))
            {
                try
                {
                    SourceImagePreview.Source = new Bitmap(sourceImagePath);
                }
                catch
                {
                    // Ignore preview failure
                }
            }
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            _navigationService?.NavigateBack();
        }

        private void OnApplyClick(object? sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_sourceImagePath) && !string.IsNullOrEmpty(_targetIconPath) && _imageProcessor != null)
            {
                try
                {
                    // Center-crop to 64x64
                    byte[] bytes = File.ReadAllBytes(_sourceImagePath);
                    using var bmp = SkiaSharp.SKBitmap.Decode(bytes);
                    if (bmp != null)
                    {
                        int minDim = Math.Min(bmp.Width, bmp.Height);
                        int x = (bmp.Width - minDim) / 2;
                        int y = (bmp.Height - minDim) / 2;
                        byte[] cropped = _imageProcessor.CropAndResizeImage(bytes, x, y, minDim, 64);
                        File.WriteAllBytes(_targetIconPath, cropped);
                    }
                }
                catch
                {
                    // Handle failure gracefully
                }
            }

            _onCompleted?.Invoke();
            _navigationService?.NavigateBack();
        }
    }
}
