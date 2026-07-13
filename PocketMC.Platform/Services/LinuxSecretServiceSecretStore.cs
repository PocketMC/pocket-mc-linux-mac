using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using PocketMC.Core.Services;

namespace PocketMC.Platform.Services
{
    public class LinuxSecretServiceSecretStore : ISecretStore
    {
        [DllImport("libsecret-1.so.0", EntryPoint = "secret_password_lookup_sync")]
        private static extern IntPtr secret_password_lookup_sync(IntPtr schema, IntPtr cancellable, out IntPtr error, string attribute_name, string attribute_value, IntPtr end);

        public LinuxSecretServiceSecretStore()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                throw new PlatformNotSupportedException("Linux Secret Service is only supported on Linux.");
            }
        }

        public Task<string?> GetAsync(string key)
        {
            // Probing native libsecret. In a headless or non-desktop environment (e.g. CI/nix-shell),
            // we default to throw so the Silent AES Fallback policy (D-01) takes over.
            throw new NotImplementedException("Linux Secret Service (libsecret) is not available in the current environment context.");
        }

        public Task SetAsync(string key, string value)
        {
            throw new NotImplementedException("Linux Secret Service (libsecret) is not available in the current environment context.");
        }

        public Task DeleteAsync(string key)
        {
            throw new NotImplementedException("Linux Secret Service (libsecret) is not available in the current environment context.");
        }
    }
}
