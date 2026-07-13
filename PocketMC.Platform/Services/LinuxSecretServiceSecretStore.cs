using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using PocketMC.Core.Services;

namespace PocketMC.Platform.Services
{
    public class LinuxSecretServiceSecretStore : ISecretStore
    {
        private const string LibName = "libsecret-1.so.0";

        [DllImport(LibName, EntryPoint = "secret_password_store_sync", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool secret_password_store_sync(
            IntPtr schema,
            string collection,
            string label,
            string password,
            IntPtr cancellable,
            out IntPtr error,
            string attributeKey,
            string attributeValue,
            IntPtr end
        );

        [DllImport(LibName, EntryPoint = "secret_password_lookup_sync", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr secret_password_lookup_sync(
            IntPtr schema,
            IntPtr cancellable,
            out IntPtr error,
            string attributeKey,
            string attributeValue,
            IntPtr end
        );

        [DllImport(LibName, EntryPoint = "secret_password_clear_sync", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool secret_password_clear_sync(
            IntPtr schema,
            IntPtr cancellable,
            out IntPtr error,
            string attributeKey,
            string attributeValue,
            IntPtr end
        );

        [DllImport(LibName, EntryPoint = "secret_password_free", CallingConvention = CallingConvention.Cdecl)]
        private static extern void secret_password_free(IntPtr password);

        public LinuxSecretServiceSecretStore()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                throw new PlatformNotSupportedException("Linux Secret Service is only supported on Linux.");
            }
        }

        public Task<string?> GetAsync(string key)
        {
            IntPtr error = IntPtr.Zero;
            IntPtr resultPtr = secret_password_lookup_sync(IntPtr.Zero, IntPtr.Zero, out error, "account", key, IntPtr.Zero);
            
            if (error != IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to lookup password in Linux Secret Service.");
            }

            if (resultPtr == IntPtr.Zero)
            {
                return Task.FromResult<string?>(null);
            }

            try
            {
                string? password = Marshal.PtrToStringAnsi(resultPtr);
                return Task.FromResult(password);
            }
            finally
            {
                secret_password_free(resultPtr);
            }
        }

        public Task SetAsync(string key, string value)
        {
            IntPtr error = IntPtr.Zero;
            bool success = secret_password_store_sync(
                IntPtr.Zero,
                "login", // Default collection
                $"PocketMC Secret: {key}",
                value,
                IntPtr.Zero,
                out error,
                "account",
                key,
                IntPtr.Zero
            );

            if (error != IntPtr.Zero || !success)
            {
                throw new InvalidOperationException("Failed to store password in Linux Secret Service.");
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(string key)
        {
            IntPtr error = IntPtr.Zero;
            secret_password_clear_sync(IntPtr.Zero, IntPtr.Zero, out error, "account", key, IntPtr.Zero);
            return Task.CompletedTask;
        }
    }
}
