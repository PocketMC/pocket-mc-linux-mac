using System.Text;
using PocketMC.RemoteControl.Models;

namespace PocketMC.RemoteControl.Services;

internal static class RemoteTextFileContent
{
    private const int MaximumBytes = 1_048_576;
    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jar", ".zip", ".tar", ".gz", ".7z", ".rar", ".png", ".jpg", ".jpeg", ".gif", ".ico",
        ".dat", ".dat_old", ".mca", ".nbt", ".lock", ".exe", ".dll", ".so", ".dylib", ".bin", ".db", ".sqlite", ".phar"
    };
    public static bool IsKnownBinaryPath(string path) => BinaryExtensions.Contains(Path.GetExtension(path));

    public static RemoteFileContentDto Read(string path, string relativePath)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        byte[] bytes = new byte[MaximumBytes + 1];
        int length = 0;
        while (length < bytes.Length)
        {
            int read = input.Read(bytes, length, bytes.Length - length);
            if (read == 0) break;
            length += read;
        }
        var result = new RemoteFileContentDto { RelativePath = relativePath, SizeBytes = input.Length, IsText = false };
        if (length > MaximumBytes)
        {
            result.IsTruncated = true;
            result.Content = "File exceeds the 1 MB browser editor limit.";
            return result;
        }
        try
        {
            int offset = length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf ? 3 : 0;
            string text = new UTF8Encoding(false, true).GetString(bytes, offset, length - offset);
            if (!IsPlainText(text))
                throw new DecoderFallbackException();
            result.Content = text;
            result.IsText = true;
        }
        catch (DecoderFallbackException)
        {
            result.Content = "Binary or unsupported text encoding; this file cannot be edited in the browser.";
        }
        return result;
    }

    public static bool IsPlainText(string text) => !text.Any(character =>
        char.IsControl(character) && character is not ('\r' or '\n' or '\t' or '\f'));
}
