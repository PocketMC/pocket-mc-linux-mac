using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using PocketMC.Core.Services;

namespace PocketMC.Platform.Services;

public class PosixFilePermissionService : IPosixFilePermissionService
{
    private static readonly UnixFileMode ExecutableMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    public void EnsureExecutablePermission(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(filePath, ExecutableMode);
        }
        catch
        {
            try
            {
                var psi = new ProcessStartInfo("chmod", $"+x \"{filePath}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(psi)?.WaitForExit(3000);
            }
            catch
            {
            }
        }
    }

    public void EnsureDirectoryPermissions(string directoryPath, bool recursive = true)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        try
        {
            var files = Directory.GetFiles(directoryPath, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                string fileName = Path.GetFileName(file);
                if (string.IsNullOrEmpty(ext) || ext == ".sh" || ext == ".bin" ||
                    fileName.Contains("bedrock_server") || fileName.Contains("cloudflared") || fileName.Contains("playit"))
                {
                    EnsureExecutablePermission(file);
                }
            }
        }
        catch
        {
        }
    }
}
