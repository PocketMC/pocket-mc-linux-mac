using System;
using System.IO;
using PocketMC.Core.Services;
using SkiaSharp;

namespace PocketMC.App.Services
{
    public class ImageProcessingService : IImageProcessingService
    {
        public byte[] CropAndResizeImage(byte[] imageBytes, int pxX, int pxY, int pxSize, int targetSize = 64)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                throw new ArgumentException("Image bytes cannot be empty.", nameof(imageBytes));

            using var original = SKBitmap.Decode(imageBytes);
            if (original == null)
                throw new InvalidOperationException("Failed to decode image data.");

            pxX = Math.Clamp(pxX, 0, original.Width);
            pxY = Math.Clamp(pxY, 0, original.Height);
            if (pxX + pxSize > original.Width) pxSize = original.Width - pxX;
            if (pxY + pxSize > original.Height) pxSize = original.Height - pxY;

            if (pxSize <= 0)
                throw new InvalidOperationException("Invalid crop region dimensions.");

            var subsetRect = new SKRectI(pxX, pxY, pxX + pxSize, pxY + pxSize);
            using var cropped = new SKBitmap(pxSize, pxSize);
            if (!original.ExtractSubset(cropped, subsetRect))
            {
                throw new InvalidOperationException("Failed to extract image crop subset.");
            }

            var resizeInfo = new SKImageInfo(targetSize, targetSize, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var resized = cropped.Resize(resizeInfo, SKFilterQuality.High);
            if (resized == null)
            {
                throw new InvalidOperationException("Failed to resize cropped image.");
            }

            using var image = SKImage.FromBitmap(resized);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        public void CropAndSaveToFile(string inputImagePath, string outputImagePath, int pxX, int pxY, int pxSize, int targetSize = 64)
        {
            byte[] inputBytes = File.ReadAllBytes(inputImagePath);
            byte[] croppedBytes = CropAndResizeImage(inputBytes, pxX, pxY, pxSize, targetSize);
            File.WriteAllBytes(outputImagePath, croppedBytes);
        }
    }
}
