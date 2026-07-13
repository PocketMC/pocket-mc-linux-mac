using System;
using System.IO;
using System.Threading.Tasks;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;
using PocketMC.Platform.Services;
using Xunit;

namespace PocketMC.Tests
{
    public class SecureStorageTests
    {
        [Fact]
        public async Task TestAesFallbackSecretStoreOperations()
        {
            var settingsService = new SettingsService();
            var secretStore = new AesFallbackSecretStore(settingsService);

            string key = "test_key_" + Guid.NewGuid();
            string val = "secret_value_xyz_123";

            // Get non-existent
            var nonExistent = await secretStore.GetAsync(key);
            Assert.Null(nonExistent);

            // Set and Get
            await secretStore.SetAsync(key, val);
            var retrieved = await secretStore.GetAsync(key);
            Assert.Equal(val, retrieved);

            // Delete
            await secretStore.DeleteAsync(key);
            var deleted = await secretStore.GetAsync(key);
            Assert.Null(deleted);
        }

        [Fact]
        public void TestSecretStoreFactoryFallback()
        {
            var settingsService = new SettingsService();
            var store = SecretStoreFactory.Create(settingsService);
            Assert.NotNull(store);
        }
    }
}
