using System;
using System.Collections.Generic;

namespace PocketMC.Core.Services
{
    public interface IConsoleLogService
    {
        void WriteLog(string slug, string line);
        IReadOnlyList<string> GetLogs(string slug);
        IReadOnlyList<string> SearchLogs(string slug, string query);
        void ClearLogs(string slug);
        event Action<string, string> LogReceived; // Instance slug, log line
        event Action<string> LogsCleared; // Instance slug
    }
}
