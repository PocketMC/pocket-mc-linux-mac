using System;
using System.IO;

namespace PocketMC.Infrastructure.Services
{
    public static class PathSafety
    {
        private static readonly char[] DirectorySeparators = { '\\', '/' };

        public static bool ContainsTraversal(string relativePath)
        {
            if (IsUnsafeRelativePath(relativePath))
            {
                return true;
            }

            try
            {
                _ = Path.GetFullPath(relativePath);
                return false;
            }
            catch
            {
                return true;
            }
        }

        public static string? ValidateContainedPath(string rootDirectory, string relativePath)
        {
            if (IsUnsafeRelativePath(relativePath))
            {
                return null;
            }

            string root = EnsureTrailingDirectorySeparator(Path.GetFullPath(rootDirectory));
            string resolved = Path.GetFullPath(Path.Combine(rootDirectory, relativePath));
            return resolved.StartsWith(root, GetPathComparison()) ? resolved : null;
        }

        private static bool IsUnsafeRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return true;
            }

            if (Path.IsPathRooted(relativePath) || IsWindowsRootedPath(relativePath))
            {
                return true;
            }

            foreach (string segment in relativePath.Split(DirectorySeparators, StringSplitOptions.RemoveEmptyEntries))
            {
                if (segment == "." || segment == "..")
                {
                    return true;
                }

                if (segment.Contains(':', StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsWindowsRootedPath(string path)
        {
            if (path.StartsWith(@"\\", StringComparison.Ordinal) ||
                path.StartsWith("//", StringComparison.Ordinal))
            {
                return true;
            }

            return path.Length >= 3 &&
                   IsAsciiLetter(path[0]) &&
                   path[1] == ':' &&
                   (path[2] == '\\' || path[2] == '/');
        }

        private static bool IsAsciiLetter(char value)
        {
            return (value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z');
        }

        private static string EnsureTrailingDirectorySeparator(string path)
        {
            return path.EndsWith('\\') || path.EndsWith('/')
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        private static StringComparison GetPathComparison()
        {
            return OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        }
    }
}
