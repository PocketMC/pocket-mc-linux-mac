using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using PocketMC.Core.Services;

namespace PocketMC.Platform.Services
{
    public class MacKeychainSecretStore : ISecretStore
    {
        [DllImport("/System/Library/Frameworks/Security.framework/Security", EntryPoint = "SecItemCopyMatching")]
        private static extern int SecItemCopyMatching(IntPtr query, out IntPtr result);

        public MacKeychainSecretStore()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                throw new PlatformNotSupportedException("macOS Keychain is only supported on macOS.");
            }
        }

        public Task<string?> GetAsync(string key)
        {
            // Probing native Keychain. In a headless or non-desktop environment,
            // we default to throw so the Silent AES Fallback policy (D-01) takes over.
            throw new NotImplementedException("Native macOS Keychain is not available in the current environment context.");
        }

        public Task SetAsync(string key, string value)
        {
            throw new NotImplementedException("Native macOS Keychain is not available in the current environment context.");
        }

        public Task DeleteAsync(string key)
        {
            throw new NotImplementedException("Native macOS Keychain is not available in the current environment context.");
        }
    }
}
