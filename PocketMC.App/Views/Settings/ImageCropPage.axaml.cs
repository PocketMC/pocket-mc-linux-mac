using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using PocketMC.App.Navigation;
using PocketMC.App.Services;
using PocketMC.Core.Services;

namespace PocketMC.App.Views.Settings;

public partial class ImageCropPage : UserControl
{
    private readonly string _inputImagePath;
    private readonly IAppNavigationService _navigationService;
    private readonly IImageProcessingService _imageProcessingService;
    private readonly Func<byte[], Task> _onCropCompleteAsync;

    private byte[] _sourceBytes = Array.Empty<byte>();
    private Bitmap? _originalBitmap;
    private double _imageScale = 1.0;
    private double _imgLeft;
    private double _imgTop;
    private double _imgWidth;
    private double _imgHeight;
    private Rect _cropRect;
    private bool _isDragging;
    private Point _dragStart;
    private Rect _dragStartRect;

    public ImageCropPage()
    {
        InitializeComponent();
        _inputImagePath = string.Empty;
        _navigationService = null!;
        _imageProcessingService = new ImageProcessingService();
        _onCropCompleteAsync = _ => Task.CompletedTask;
    }

    public ImageCropPage(
        string inputImagePath,
        IAppNavigationService navigationService,
        IImageProcessingService imageProcessingService,
        Func<byte[], Task> onCropCompleteAsync)
    {
        InitializeComponent();
        _inputImagePath = inputImagePath;
        _navigationService = navigationService;
        _imageProcessingService = imageProcessingService;
        _onCropCompleteAsync = onCropCompleteAsync;

        LoadImage(inputImagePath);
    }

    private void LoadImage(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

        try
        {
            _sourceBytes = File.ReadAllBytes(path);
            using var ms = new MemoryStream(_sourceBytes);
            _originalBitmap = new Bitmap(ms);
            SourceImage.Source = _originalBitmap;
        }
        catch
        {
            _navigationService?.NavigateBack();
        }
    }

    private void DrawCanvas_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_originalBitmap == null || DrawCanvas.Bounds.Width <= 0 || DrawCanvas.Bounds.Height <= 0) return;

        double canvasW = DrawCanvas.Bounds.Width;
        double canvasH = DrawCanvas.Bounds.Height;
        double pxW = _originalBitmap.PixelSize.Width;
        double pxH = _originalBitmap.PixelSize.Height;
        if (pxW <= 0 || pxH <= 0) return;

        _imageScale = Math.Min(canvasW / pxW, canvasH / pxH);
        _imgWidth = pxW * _imageScale;
        _imgHeight = pxH * _imageScale;
        _imgLeft = (canvasW - _imgWidth) / 2.0;
        _imgTop = (canvasH - _imgHeight) / 2.0;

        SourceImage.Width = _imgWidth;
        SourceImage.Height = _imgHeight;
        Canvas.SetLeft(SourceImage, _imgLeft);
        Canvas.SetTop(SourceImage, _imgTop);

        if (_cropRect.Width <= 0)
        {
            double side = Math.Min(_imgWidth, _imgHeight) * 0.75;
            _cropRect = new Rect(
                _imgLeft + (_imgWidth - side) / 2.0,
                _imgTop + (_imgHeight - side) / 2.0,
                side,
                side);
        }

        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        Canvas.SetLeft(CropRectBorder, _cropRect.X);
        Canvas.SetTop(CropRectBorder, _cropRect.Y);
        CropRectBorder.Width = _cropRect.Width;
        CropRectBorder.Height = _cropRect.Height;

        Canvas.SetLeft(ResizeThumbBR, _cropRect.Right - 8);
        Canvas.SetTop(ResizeThumbBR, _cropRect.Bottom - 8);

        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (_sourceBytes.Length == 0 || _imageScale <= 0) return;

        try
        {
            (int pxX, int pxY, int pxSize) = ComputePixelCrop();
            byte[] previewBytes = _imageProcessingService.CropAndResizeImage(_sourceBytes, pxX, pxY, pxSize, 64);
            using var ms = new MemoryStream(previewBytes);
            PreviewImage.Source = new Bitmap(ms);
        }
        catch
        {
        }
    }

    private (int PxX, int PxY, int PxSize) ComputePixelCrop()
    {
        if (_originalBitmap == null || _imageScale <= 0)
        {
            return (0, 0, 64);
        }

        int pxX = Math.Max(0, (int)Math.Round((_cropRect.X - _imgLeft) / _imageScale));
        int pxY = Math.Max(0, (int)Math.Round((_cropRect.Y - _imgTop) / _imageScale));
        int pxSize = Math.Max(1, (int)Math.Round(_cropRect.Width / _imageScale));
        pxSize = Math.Min(pxSize, Math.Min(_originalBitmap.PixelSize.Width - pxX, _originalBitmap.PixelSize.Height - pxY));
        return (pxX, pxY, Math.Max(1, pxSize));
    }

    private void Canvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Point pos = e.GetPosition(DrawCanvas);
        if (_cropRect.Contains(pos))
        {
            _isDragging = true;
            _dragStart = pos;
            _dragStartRect = _cropRect;
            e.Pointer.Capture(DrawCanvas);
        }
    }

    private void Canvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging) return;
        Point pos = e.GetPosition(DrawCanvas);
        double dx = pos.X - _dragStart.X;
        double dy = pos.Y - _dragStart.Y;

        double newX = Math.Clamp(_dragStartRect.X + dx, _imgLeft, _imgLeft + _imgWidth - _cropRect.Width);
        double newY = Math.Clamp(_dragStartRect.Y + dy, _imgTop, _imgTop + _imgHeight - _cropRect.Height);
        _cropRect = new Rect(newX, newY, _cropRect.Width, _cropRect.Height);
        UpdateVisuals();
    }

    private void Canvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            e.Pointer.Capture(null);
        }
    }

    private void ResizeThumb_DragDelta(object? sender, VectorEventArgs e)
    {
        double delta = Math.Abs(e.Vector.X) > Math.Abs(e.Vector.Y) ? e.Vector.X : e.Vector.Y;
        double maxSide = Math.Min(_imgLeft + _imgWidth - _cropRect.X, _imgTop + _imgHeight - _cropRect.Y);
        double newSide = Math.Clamp(_cropRect.Width + delta, 24.0, Math.Max(24.0, maxSide));
        _cropRect = new Rect(_cropRect.X, _cropRect.Y, newSide, newSide);
        UpdateVisuals();
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        _navigationService?.NavigateBack();
    }

    private async void BtnSave_Click(object? sender, RoutedEventArgs e)
    {
        if (_sourceBytes.Length > 0)
        {
            (int pxX, int pxY, int pxSize) = ComputePixelCrop();
            byte[] croppedBytes = _imageProcessingService.CropAndResizeImage(_sourceBytes, pxX, pxY, pxSize, 64);
            await _onCropCompleteAsync(croppedBytes);
        }

        _navigationService?.NavigateBack();
    }
}
