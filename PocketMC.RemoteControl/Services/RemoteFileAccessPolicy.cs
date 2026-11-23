using System;
using System.IO;
using System.Linq;

namespace PocketMC.RemoteControl.Services;

public static class RemoteFileAccessPolicy
{
    private static readonly string[] SensitiveExtensions =
    {
        ".pem", ".key", ".pfx", ".p12", ".jks", ".keystore", ".secret", ".token"
    };

    private static readonly string[] SensitiveExactNames =
    {
        "forwarding.secret", "id_rsa", "id_ed25519", "id_ecdsa", "id_dsa",
        "credentials.json", "token.json", "auth.json"
    };

    public static bool CanAccess(string relativePath, bool authenticatedAdmin)
    {
        // Remote file access requires authenticated administrator permissions
        if (!authenticatedAdmin) return false;

        if (string.IsNullOrWhiteSpace(relativePath)) return true;

        string normalized = relativePath.Replace('\\', '/');

        // Path traversal checks: never allow parent directory traversal or absolute/scheme roots
        if (normalized.StartsWith('/') || normalized.Contains(':') || normalized.Contains("..")) return false;

        string[] parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.TrimEnd(' ', '.')).ToArray();

        if (parts.Length == 0) return true;

        // Reject directory traversal steps
        if (parts.Any(p => p == ".." || p == ".")) return false;

        // Reject DOS short-name aliases, which can refer to a protected long name.
        if (parts.Any(part =>
        {
            int tilde = part.LastIndexOf('~');
            return tilde >= 0 && int.TryParse(part[(tilde + 1)..].Split('.')[0], out int index) && index > 0;
        })) return false;

        // Internal engine artifacts, completion bridges, and transaction journals must never be accessible remotely
        if (parts.Any(p => p.StartsWith(".pocketmc-", StringComparison.OrdinalIgnoreCase) ||
                           p.StartsWith(".restore-", StringComparison.OrdinalIgnoreCase) ||
                           p.Equals(".pocketmc-bridge", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        string name = parts.LastOrDefault() ?? string.Empty;

        // Sensitive credentials and private keys are strictly forbidden even for authenticated web sessions
        if (IsSensitive(name))
        {
            return false;
        }

        return true;
    }

    public static bool IsSensitive(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;

        if (fileName.Equals(".env", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith(".env.", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".env", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (SensitiveExactNames.Contains(fileName, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        string ext = Path.GetExtension(fileName);
        if (SensitiveExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
