namespace PocketMC.Core.Services;

public interface IPosixFilePermissionService
{
    void EnsureExecutablePermission(string filePath);
    void EnsureDirectoryPermissions(string directoryPath, bool recursive = true);
}
