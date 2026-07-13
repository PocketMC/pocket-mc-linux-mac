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
            var service = new SettingsService();
            var settingsDir = service.GetSettingsDirectory();
            Assert.True(Directory.Exists(settingsDir));

            Assert.True(Directory.Exists(service.GetInstancesDirectory()));
            Assert.True(Directory.Exists(service.GetBackupsDirectory()));
            Assert.True(Directory.Exists(service.GetDownloadsDirectory()));
            Assert.True(Directory.Exists(service.GetCacheDirectory()));
            Assert.True(Directory.Exists(service.GetLogsDirectory()));

            Assert.True(File.Exists(Path.Combine(settingsDir, "settings.json")));
        }

        [Fact]
        public void TestCustomDataRootPathOverride()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "PocketMC_Test_CustomRoot_" + Guid.NewGuid());
            try
            {
                var service = new SettingsService();
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
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }
    }
}
