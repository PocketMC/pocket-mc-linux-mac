using System;
using System.Runtime.InteropServices;
using PocketMC.Core.Services;

namespace PocketMC.Platform.Services
{
    public static class SecretStoreFactory
    {
        public static ISecretStore Create(ISettingsService settingsService)
        {
            var fallback = new AesFallbackSecretStore(settingsService);

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    var native = new MacKeychainSecretStore();
                    return new SafeSecretStore(native, fallback);
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    var native = new LinuxSecretServiceSecretStore();
                    return new SafeSecretStore(native, fallback);
                }
            }
            catch
            {
                // Silent fallback on instantiation issues (D-01)
            }

            return fallback;
        }
    }
}
