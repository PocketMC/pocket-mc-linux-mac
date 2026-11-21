using System;
using System.IO;
using System.Linq;

namespace PocketMC.RemoteControl.Services;

public static class RemoteFileAccessPolicy
{
    public static bool CanAccess(string relativePath, bool authenticatedAdmin)
    {
        string[] parts = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.TrimEnd(' ', '.')).ToArray();

        // Reject DOS short-name aliases, which can refer to a protected long name.
        if (parts.Any(part =>
        {
            int tilde = part.LastIndexOf('~');
            return tilde >= 0 && int.TryParse(part[(tilde + 1)..].Split('.')[0], out int index) && index > 0;
        })) return false;

        // Completion bridge discovery and credentials never belong in the web file manager.
        if (parts.Any(p => p.Equals(".pocketmc-bridge", StringComparison.OrdinalIgnoreCase))) return false;
        if (parts.Any(p => p.Equals(".pocketmc-pending-addons", StringComparison.OrdinalIgnoreCase) ||
            p.StartsWith(".pocketmc-addons-stage-", StringComparison.OrdinalIgnoreCase))) return false;

        string name = parts.LastOrDefault() ?? string.Empty;
        bool secret = name.Equals("forwarding.secret", StringComparison.OrdinalIgnoreCase) ||
            name.Equals(".env", StringComparison.OrdinalIgnoreCase) || name.StartsWith(".env.", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".env", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("id_rsa", StringComparison.OrdinalIgnoreCase) || name.Equals("id_ed25519", StringComparison.OrdinalIgnoreCase) ||
            new[] { ".pem", ".key", ".pfx", ".p12", ".jks", ".keystore" }.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase);

        return !secret || authenticatedAdmin;
    }
}
