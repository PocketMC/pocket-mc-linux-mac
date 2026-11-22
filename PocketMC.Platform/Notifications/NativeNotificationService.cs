using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using PocketMC.Core.Services;

namespace PocketMC.Platform.Notifications;

public sealed class NativeNotificationService : INotificationService
{
    private readonly ILogger<NativeNotificationService>? _logger;
    private readonly string? _iconPath;

    public static event Action<Guid>? OnSummaryNotificationClicked;

    public NativeNotificationService(ILogger<NativeNotificationService>? logger = null, string? iconPath = null)
    {
        _logger = logger;
        _iconPath = iconPath;
    }

    public static (string FileName, string Arguments) BuildLinuxCommand(string title, string message, string? iconPath = null)
    {
        string escapedTitle = title.Replace("\"", "\\\"");
        string escapedMessage = message.Replace("\"", "\\\"");

        string iconArg = !string.IsNullOrWhiteSpace(iconPath) ? $"-i \"{iconPath}\" " : string.Empty;
        string args = $"-a \"PocketMC\" {iconArg}\"{escapedTitle}\" \"{escapedMessage}\"";

        return ("notify-send", args);
    }

    public static string BuildMacScript(string title, string message)
    {
        string escapedTitle = title.Replace("\"", "\\\"");
        string escapedMessage = message.Replace("\"", "\\\"");
        return $"-e \"display notification \\\"{escapedMessage}\\\" with title \\\"{escapedTitle}\\\" subtitle \\\"PocketMC\\\"\"";
    }

    public void ShowInformation(string title, string message)
    {
        SendNotification(title, message);
    }

    public void ShowServerOnline(string serverName, string version, string loaderType)
    {
        SendNotification("Server Online", $"{serverName} ({loaderType} {version}) is now online.");
    }

    public void ShowSummaryComplete(string instanceId, string serverName)
    {
        SendNotification("AI Summary Complete", $"Session summary saved for '{serverName}'.");
        if (Guid.TryParse(instanceId, out var id))
        {
            // Allow programmatic handler dispatch
            OnSummaryNotificationClicked?.Invoke(id);
        }
    }

    public void ShowAgentConnected()
    {
        SendNotification("Agent Connected", "Your Playit Agent is ready.");
    }

    public void ShowTunnelCreated(int serverPort, string address)
    {
        SendNotification("Tunnel Created", $"Port {serverPort} is now accessible at {address}.");
    }

    public void ShowRemoteControlStarted()
    {
        SendNotification("Remote Control", "Your remote control web panel is running.");
    }

    private void SendNotification(string title, string message)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var (fileName, args) = BuildLinuxCommand(title, message, _iconPath);
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(2000);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string script = BuildMacScript(title, message);
                var psi = new ProcessStartInfo
                {
                    FileName = "osascript",
                    Arguments = script,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(2000);
            }
            else
            {
                _logger?.LogInformation("Notification: [{Title}] {Message}", title, message);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to deliver native notification [{Title}].", title);
        }
    }
}
