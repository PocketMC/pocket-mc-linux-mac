using System;
using System.IO;

namespace PocketMC.RemoteControl.Services;

public class RemoteAuditLogService
{
    private readonly string _logFilePath;
    private static readonly object _lock = new();

    public RemoteAuditLogService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "PocketMC", "logs"))
    {
    }

    public RemoteAuditLogService(string logsDirectory)
    {
        Directory.CreateDirectory(logsDirectory);
        _logFilePath = Path.Combine(logsDirectory, "remote-actions.log");
        
        if (!File.Exists(_logFilePath))
        {
            File.WriteAllText(_logFilePath, "timestamp\tdeviceId\taction\tinstanceId\ttarget\tsuccess\terrorMsg\n");
        }
    }

    public void Log(string? deviceId, string action, Guid? instanceId = null, string? target = null, bool success = true, string? message = null)
    {
        string line = string.Join('\t', new[]
        {
            DateTimeOffset.UtcNow.ToString("O"),
            Sanitize(deviceId ?? "unknown"),
            Sanitize(action),
            instanceId?.ToString("D") ?? "",
            Sanitize(target ?? ""),
            success ? "success" : "failed",
            Sanitize(message ?? "")
        });

        lock (_lock)
        {
            var dir = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.AppendAllText(_logFilePath, line + "\n");
        }
    }

    public void LogAction(string deviceId, string action, string instanceId, bool success, string errorMsg = "")
    {
        var timestamp = DateTime.UtcNow.ToString("O");
        var logLine = $"{timestamp}\t{deviceId}\t{action}\t{instanceId}\t\t{success}\t{errorMsg}\n";
        
        lock (_lock)
        {
            File.AppendAllText(_logFilePath, logLine);
        }
    }

    private static string Sanitize(string value) =>
        value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\t", " ", StringComparison.Ordinal)
            .Trim();
}

