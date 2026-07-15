using System.Collections.Generic;

namespace PocketMC.Core.Services
{
    public interface ILogRedactionService
    {
        /// <summary>
        /// Redacts sensitive information (IPs, user folders) from log content.
        /// </summary>
        string RedactLog(string content);

        /// <summary>
        /// Extracts and redacts a relevant tail section of logs for diagnostics,
        /// scanning backwards to capture exception stack traces if present.
        /// </summary>
        string GetRedactedDiagnosticsLog(string slug);
    }
}
