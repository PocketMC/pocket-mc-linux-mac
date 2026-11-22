using System;

namespace PocketMC.Core.Services
{
    public interface IImageProcessingService
    {
        byte[] CropAndResizeImage(byte[] imageBytes, int pxX, int pxY, int pxSize, int targetSize = 64);
        void CropAndSaveToFile(string inputImagePath, string outputImagePath, int pxX, int pxY, int pxSize, int targetSize = 64);
    }
}
