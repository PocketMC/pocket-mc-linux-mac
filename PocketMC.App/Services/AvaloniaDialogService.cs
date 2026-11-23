using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PocketMC.App.Views.Dialogs;
using PocketMC.Core.Services;

namespace PocketMC.App.Services;

public sealed class AvaloniaDialogService : IDialogService
{
    private static Window? GetActiveWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.Windows.FirstOrDefault(w => w.IsActive) ?? desktop.MainWindow;
        }

        return null;
    }

    public async Task<DialogResult> ShowDialogAsync(
        string title,
        string message,
        DialogType type = DialogType.Information,
        bool showCancel = false,
        string? primaryButtonText = null,
        string? secondaryButtonText = null,
        string? cancelButtonText = null,
        string? linkText = null,
        string? linkUrl = null)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var owner = GetActiveWindow();
            var dialog = new AppDialogWindow();
            dialog.Configure(
                title,
                message,
                type,
                showCancel,
                primaryButtonText,
                secondaryButtonText,
                cancelButtonText,
                linkText,
                linkUrl);

            if (owner != null)
            {
                return await dialog.ShowDialog<DialogResult>(owner);
            }

            dialog.Show();
            return dialog.Result;
        });
    }

    public void ShowMessage(string title, string message, DialogType type = DialogType.Information)
    {
        _ = ShowDialogAsync(title, message, type);
    }

    public async Task<string?> OpenFolderDialogAsync(string title)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var owner = GetActiveWindow();
            if (owner == null) return null;

            var topLevel = TopLevel.GetTopLevel(owner);
            if (topLevel?.StorageProvider == null) return null;

            var result = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            });

            return result.Count > 0 ? result[0].Path.LocalPath : null;
        });
    }

    public async Task<string?> OpenFileDialogAsync(string title, string filter = "All Files (*.*)|*.*")
    {
        var files = await OpenFilesDialogAsync(title, filter);
        return files.Length > 0 ? files[0] : null;
    }

    public async Task<string[]> OpenFilesDialogAsync(string title, string filter = "All Files (*.*)|*.*")
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var owner = GetActiveWindow();
            if (owner == null) return Array.Empty<string>();

            var topLevel = TopLevel.GetTopLevel(owner);
            if (topLevel?.StorageProvider == null) return Array.Empty<string>();

            var fileTypes = ParseFilterToFileTypeChoices(filter);

            var result = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = true,
                FileTypeFilter = fileTypes
            });

            return result.Select(f => f.Path.LocalPath).ToArray();
        });
    }

    public async Task<(string? Username, string? Password)> PromptCredentialsAsync(
        string title,
        string message,
        bool askUsername,
        bool askPassword)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var owner = GetActiveWindow();
            var dialog = new PasswordPromptDialogWindow();
            dialog.Configure(title, message, askUsername, askPassword);

            if (owner != null)
            {
                return await dialog.ShowDialog<(string?, string?)>(owner);
            }

            return (null, null);
        });
    }

    public async Task ShowProgressDialogAsync(string title, string message, Func<IProgress<double>, Task> action)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var owner = GetActiveWindow();
            var cts = new CancellationTokenSource();
            var dialog = new ProgressDialogWindow();
            dialog.Configure(title, message, cts);

            var progress = new Progress<double>(value => dialog.UpdateProgress(value));

            var workerTask = Task.Run(async () =>
            {
                try
                {
                    await action(progress);
                }
                finally
                {
                    await Dispatcher.UIThread.InvokeAsync(dialog.Close);
                }
            });

            if (owner != null)
            {
                await dialog.ShowDialog(owner);
            }
            else
            {
                dialog.Show();
            }

            await workerTask;
        });
    }

    public async Task ShowProgressDialogAsync(string title, string message, Func<IProgress<ProgressDialogUpdate>, Task> action)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var owner = GetActiveWindow();
            var cts = new CancellationTokenSource();
            var dialog = new ProgressDialogWindow();
            dialog.Configure(title, message, cts);

            var progress = new Progress<ProgressDialogUpdate>(update => dialog.UpdateProgress(update));

            var workerTask = Task.Run(async () =>
            {
                try
                {
                    await action(progress);
                }
                finally
                {
                    await Dispatcher.UIThread.InvokeAsync(dialog.Close);
                }
            });

            if (owner != null)
            {
                await dialog.ShowDialog(owner);
            }
            else
            {
                dialog.Show();
            }

            await workerTask;
        });
    }

    private static List<FilePickerFileType> ParseFilterToFileTypeChoices(string filter)
    {
        var list = new List<FilePickerFileType>();
        if (string.IsNullOrWhiteSpace(filter))
        {
            return list;
        }

        var parts = filter.Split('|', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length - 1; i += 2)
        {
            string name = parts[i].Trim();
            string pattern = parts[i + 1].Trim();

            var extensions = pattern
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim().TrimStart('*', '.'))
                .Where(ext => !string.IsNullOrEmpty(ext) && ext != "*")
                .Select(ext => $"*.{ext}")
                .ToList();

            if (extensions.Count > 0)
            {
                list.Add(new FilePickerFileType(name) { Patterns = extensions });
            }
        }

        return list;
    }
}
