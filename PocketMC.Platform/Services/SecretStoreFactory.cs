using System;
using System.Runtime.InteropServices;
using PocketMC.Core.Services;

namespace PocketMC.Platform.Services
{
    public static class SecretStoreFactory
    {
        public static ISecretStore Create(ISettingsService settingsService)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return new MacKeychainSecretStore();
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return new LinuxSecretServiceSecretStore();
                }
            }
            catch
            {
                // Silent fallback (D-01)
            }

            return new AesFallbackSecretStore(settingsService);
        }
    }
}
