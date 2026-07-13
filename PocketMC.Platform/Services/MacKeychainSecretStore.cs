using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using PocketMC.Core.Services;

namespace PocketMC.Platform.Services
{
    public class MacKeychainSecretStore : ISecretStore
    {
        private const string SecurityLibrary = "/System/Library/Frameworks/Security.framework/Security";
        private const string CoreFoundationLibrary = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        private const string ServiceName = "PocketMC";

        [DllImport(SecurityLibrary, EntryPoint = "SecKeychainAddGenericPassword")]
        private static extern int SecKeychainAddGenericPassword(
            IntPtr keychain,
            uint serviceNameLength,
            string serviceName,
            uint accountNameLength,
            string accountName,
            uint passwordLength,
            byte[] passwordData,
            out IntPtr itemRef
        );

        [DllImport(SecurityLibrary, EntryPoint = "SecKeychainFindGenericPassword")]
        private static extern int SecKeychainFindGenericPassword(
            IntPtr keychain,
            uint serviceNameLength,
            string serviceName,
            uint accountNameLength,
            string accountName,
            out uint passwordLength,
            out IntPtr passwordData,
            out IntPtr itemRef
        );

        [DllImport(SecurityLibrary, EntryPoint = "SecKeychainItemFreeContent")]
        private static extern int SecKeychainItemFreeContent(IntPtr attrList, IntPtr data);

        [DllImport(SecurityLibrary, EntryPoint = "SecKeychainItemDelete")]
        private static extern int SecKeychainItemDelete(IntPtr itemRef);

        [DllImport(CoreFoundationLibrary, EntryPoint = "CFRelease")]
        private static extern void CFRelease(IntPtr cfTypeRef);

        public MacKeychainSecretStore()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                throw new PlatformNotSupportedException("macOS Keychain is only supported on macOS.");
            }
        }

        public Task<string?> GetAsync(string key)
        {
            uint passwordLength;
            IntPtr passwordData;
            IntPtr itemRef = IntPtr.Zero;

            int status = SecKeychainFindGenericPassword(
                IntPtr.Zero,
                (uint)ServiceName.Length,
                ServiceName,
                (uint)key.Length,
                key,
                out passwordLength,
                out passwordData,
                out itemRef
            );

            try
            {
                if (status == 0) // errSecSuccess
                {
                    if (passwordData != IntPtr.Zero && passwordLength > 0)
                    {
                        byte[] buffer = new byte[passwordLength];
                        Marshal.Copy(passwordData, buffer, 0, (int)passwordLength);
                        string password = Encoding.UTF8.GetString(buffer);
                        return Task.FromResult<string?>(password);
                    }
                }

                if (status == -25308 || status == -25300) // errSecItemNotFound / errSecNoSuchKeychain
                {
                    return Task.FromResult<string?>(null);
                }

                throw new InvalidOperationException($"Failed to find generic password in macOS Keychain. Status: {status}");
            }
            finally
            {
                if (passwordData != IntPtr.Zero)
                {
                    SecKeychainItemFreeContent(IntPtr.Zero, passwordData);
                }
                if (itemRef != IntPtr.Zero)
                {
                    CFRelease(itemRef);
                }
            }
        }

        public async Task SetAsync(string key, string value)
        {
            // Try to delete existing if any (avoid duplicate item error -25299)
            await DeleteAsync(key);

            byte[] passwordBytes = Encoding.UTF8.GetBytes(value);
            IntPtr itemRef;

            int status = SecKeychainAddGenericPassword(
                IntPtr.Zero,
                (uint)ServiceName.Length,
                ServiceName,
                (uint)key.Length,
                key,
                (uint)passwordBytes.Length,
                passwordBytes,
                out itemRef
            );

            if (status != 0)
            {
                throw new InvalidOperationException($"Failed to add generic password to macOS Keychain. Status: {status}");
            }

            if (itemRef != IntPtr.Zero)
            {
                CFRelease(itemRef);
            }
        }

        public Task DeleteAsync(string key)
        {
            uint passwordLength;
            IntPtr passwordData;
            IntPtr itemRef = IntPtr.Zero;

            int status = SecKeychainFindGenericPassword(
                IntPtr.Zero,
                (uint)ServiceName.Length,
                ServiceName,
                (uint)key.Length,
                key,
                out passwordLength,
                out passwordData,
                out itemRef
            );

            try
            {
                if (status == 0 && itemRef != IntPtr.Zero)
                {
                    SecKeychainItemDelete(itemRef);
                }
                return Task.CompletedTask;
            }
            finally
            {
                if (passwordData != IntPtr.Zero)
                {
                    SecKeychainItemFreeContent(IntPtr.Zero, passwordData);
                }
                if (itemRef != IntPtr.Zero)
                {
                    CFRelease(itemRef);
                }
            }
        }
    }
}
