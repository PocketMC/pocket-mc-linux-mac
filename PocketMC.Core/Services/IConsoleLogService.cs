using System;
using System.Collections.Generic;

namespace PocketMC.Core.Services
{
    public interface IConsoleLogService
    {
        void WriteLog(string slug, string line);
        IReadOnlyList<string> GetLogs(string slug);
        IReadOnlyList<string> SearchLogs(string slug, string query);
        event Action<string, string> LogReceived; // Instance slug, log line
    }
}
