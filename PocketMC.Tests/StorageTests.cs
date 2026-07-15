using System;
using System.IO;
using PocketMC.Core.Models;
using PocketMC.Infrastructure.Services;
using Xunit;

namespace PocketMC.Tests
{
    public class StorageTests
    {
        [Fact]
        public void TestDefaultDirectoriesCreated()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "PocketMC_Test_Default_" + Guid.NewGuid());
            try
            {
                var service = new SettingsService(tempRoot);
                var settingsDir = service.GetSettingsDirectory();
                Assert.True(Directory.Exists(settingsDir));

                Assert.True(Directory.Exists(service.GetInstancesDirectory()));
                Assert.True(Directory.Exists(service.GetBackupsDirectory()));
                Assert.True(Directory.Exists(service.GetDownloadsDirectory()));
                Assert.True(Directory.Exists(service.GetCacheDirectory()));
                Assert.True(Directory.Exists(service.GetLogsDirectory()));

                Assert.True(File.Exists(Path.Combine(settingsDir, "settings.json")));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void TestCustomDataRootPathOverride()
        {
            var tempConfigRoot = Path.Combine(Path.GetTempPath(), "PocketMC_Test_Config_" + Guid.NewGuid());
            var tempRoot = Path.Combine(Path.GetTempPath(), "PocketMC_Test_CustomRoot_" + Guid.NewGuid());
            try
            {
                var service = new SettingsService(tempConfigRoot);
                service.Settings.CustomDataRoot = tempRoot;
                service.Save();
                service.Load();

                var instancesDir = service.GetInstancesDirectory();
                Assert.StartsWith(tempRoot, instancesDir);
                Assert.True(Directory.Exists(instancesDir));

                var backupsDir = service.GetBackupsDirectory();
                Assert.StartsWith(tempRoot, backupsDir);
                Assert.True(Directory.Exists(backupsDir));
            }
            finally
            {
                if (Directory.Exists(tempConfigRoot))
                {
                    Directory.Delete(tempConfigRoot, true);
                }
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }
    }
}
