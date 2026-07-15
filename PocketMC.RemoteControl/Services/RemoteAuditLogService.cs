using System;
using System.IO;

namespace PocketMC.RemoteControl.Services;

public class RemoteAuditLogService
{
    private readonly string _logFilePath;
    private static readonly object _lock = new object();

    public RemoteAuditLogService(string logsDirectory)
    {
        Directory.CreateDirectory(logsDirectory);
        _logFilePath = Path.Combine(logsDirectory, "remote-actions.log");
        
        if (!File.Exists(_logFilePath))
        {
            File.WriteAllText(_logFilePath, "timestamp\tdeviceId\taction\tinstanceId\tsuccess\terrorMsg\n");
        }
    }

    public void LogAction(string deviceId, string action, string instanceId, bool success, string errorMsg = "")
    {
        var timestamp = DateTime.UtcNow.ToString("O");
        var logLine = $"{timestamp}\t{deviceId}\t{action}\t{instanceId}\t{success}\t{errorMsg}\n";
        
        lock (_lock)
        {
            File.AppendAllText(_logFilePath, logLine);
        }
    }
}
