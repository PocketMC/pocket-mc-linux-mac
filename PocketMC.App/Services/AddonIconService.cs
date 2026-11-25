using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace PocketMC.App.Services;

public static class AddonIconService
{
    private static readonly ConcurrentDictionary<string, Bitmap> RemoteIconCache = new();
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static Bitmap? GetIconFromBytes(byte[]? imageBytes)
    {
        if (imageBytes == null || imageBytes.Length == 0)
        {
            return null;
        }

        try
        {
            using var ms = new MemoryStream(imageBytes);
            return new Bitmap(ms);
        }
        catch
        {
            return null;
        }
    }

    public static async Task<Bitmap?> GetLocalIconAsync(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(filePath);
            return GetIconFromBytes(bytes);
        }
        catch
        {
            return null;
        }
    }

    public static async Task<Bitmap?> GetCachedRemoteIconAsync(string? iconUrl, string? cacheDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(iconUrl))
        {
            return null;
        }

        if (RemoteIconCache.TryGetValue(iconUrl, out Bitmap? cached))
        {
            return cached;
        }

        try
        {
            string dir = cacheDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PocketMC", "Cache", "AddonIcons");
            Directory.CreateDirectory(dir);

            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(iconUrl))).ToLowerInvariant();
            string ext = Path.GetExtension(iconUrl);
            int queryIndex = ext.IndexOf('?');
            if (queryIndex > 0) ext = ext.Substring(0, queryIndex);
            if (string.IsNullOrWhiteSpace(ext) || ext.Length > 6) ext = ".png";

            string localPath = Path.Combine(dir, hash + ext);
            if (!File.Exists(localPath))
            {
                byte[] downloaded = await HttpClient.GetByteArrayAsync(iconUrl);
                await File.WriteAllBytesAsync(localPath, downloaded);
            }

            byte[] bytes = await File.ReadAllBytesAsync(localPath);
            Bitmap? icon = GetIconFromBytes(bytes);
            if (icon != null)
            {
                RemoteIconCache[iconUrl] = icon;
            }
            return icon;
        }
        catch
        {
            return null;
        }
    }
}
