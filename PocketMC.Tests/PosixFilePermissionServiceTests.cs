using System;
using System.IO;
using System.Runtime.InteropServices;
using PocketMC.Core.Services;
using PocketMC.Platform.Services;
using Xunit;

namespace PocketMC.Tests;

public class PosixFilePermissionServiceTests
{
    [Fact]
    public void EnsureExecutablePermission_WhenFileExists_DoesNotThrow()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "#!/bin/sh\necho hello");
            IPosixFilePermissionService service = new PosixFilePermissionService();

            service.EnsureExecutablePermission(tempFile);

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var mode = File.GetUnixFileMode(tempFile);
                Assert.True(mode.HasFlag(UnixFileMode.UserExecute));
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void EnsureExecutablePermission_WhenFileDoesNotExist_HandlesGracefully()
    {
        IPosixFilePermissionService service = new PosixFilePermissionService();
        service.EnsureExecutablePermission("/non/existent/path/binary");
        // Should not throw exception
    }
}
