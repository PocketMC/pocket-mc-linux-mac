using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace PocketMC.App.Services;

public static class AppClipboard
{
    public static Func<string, Task<bool>>? OverrideHandler { get; set; }

    public static async Task<bool> SetTextAsync(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (OverrideHandler != null)
        {
            return await OverrideHandler(text);
        }

        // 1. Try Avalonia Window Clipboard via UI thread dispatcher
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.Clipboard != null)
            {
                var clipboard = desktop.MainWindow.Clipboard;
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await clipboard.SetTextAsync(text);
                });
                return true;
            }
        }
        catch
        {
            // Fall through to OS native fallback
        }

        // 2. Linux fallback: wl-copy (Wayland), xclip or xsel (X11)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (await TryRunClipboardCommandAsync("wl-copy", string.Empty, text)) return true;
            if (await TryRunClipboardCommandAsync("xclip", "-selection clipboard", text)) return true;
            if (await TryRunClipboardCommandAsync("xsel", "-b -i", text)) return true;
        }
        // 3. macOS fallback: pbcopy
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if (await TryRunClipboardCommandAsync("pbcopy", string.Empty, text)) return true;
        }

        return false;
    }

    private static async Task<bool> TryRunClipboardCommandAsync(string command, string arguments, string input)
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            if (!proc.Start())
            {
                return false;
            }

            await proc.StandardInput.WriteAsync(input);
            await proc.StandardInput.FlushAsync();
            proc.StandardInput.Close();

            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
