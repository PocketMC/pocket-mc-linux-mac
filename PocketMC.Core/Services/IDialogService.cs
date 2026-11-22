using System;
using System.Threading.Tasks;

namespace PocketMC.Core.Services;

public enum DialogResult
{
    Ok,
    Cancel,
    Yes,
    No,
    Dismiss
}

public enum DialogType
{
    Information,
    Warning,
    Error,
    Question
}

public struct ProgressDialogUpdate
{
    public double Percentage { get; set; }
    public string? Message { get; set; }

    public ProgressDialogUpdate(double percentage, string? message = null)
    {
        Percentage = percentage;
        Message = message;
    }
}

public static class DialogButtonResolver
{
    public static (string Primary, string? Secondary) Resolve(
        DialogType type,
        bool showCancel,
        string? customPrimary,
        string? customSecondary)
    {
        if (customPrimary != null)
        {
            return (customPrimary, customSecondary);
        }

        switch (type)
        {
            case DialogType.Question:
                return ("Yes", customSecondary ?? "No");

            case DialogType.Warning:
            case DialogType.Error:
                return ("OK", showCancel ? (customSecondary ?? "Cancel") : null);

            case DialogType.Information:
            default:
                return ("OK", showCancel ? (customSecondary ?? "Cancel") : null);
        }
    }
}

public interface IDialogService
{
    Task<DialogResult> ShowDialogAsync(
        string title,
        string message,
        DialogType type = DialogType.Information,
        bool showCancel = false,
        string? primaryButtonText = null,
        string? secondaryButtonText = null,
        string? cancelButtonText = null,
        string? linkText = null,
        string? linkUrl = null);

    void ShowMessage(string title, string message, DialogType type = DialogType.Information);

    Task<string?> OpenFolderDialogAsync(string title);

    Task<string?> OpenFileDialogAsync(string title, string filter = "All Files (*.*)|*.*");

    Task<string[]> OpenFilesDialogAsync(string title, string filter = "All Files (*.*)|*.*");

    Task<(string? Username, string? Password)> PromptCredentialsAsync(
        string title,
        string message,
        bool askUsername,
        bool askPassword);

    Task ShowProgressDialogAsync(
        string title,
        string message,
        Func<IProgress<double>, Task> action);

    Task ShowProgressDialogAsync(
        string title,
        string message,
        Func<IProgress<ProgressDialogUpdate>, Task> action);
}
