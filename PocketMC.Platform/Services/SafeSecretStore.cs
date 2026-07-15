using System;
using System.Threading.Tasks;
using PocketMC.Core.Services;

namespace PocketMC.Platform.Services
{
    public class SafeSecretStore : ISecretStore
    {
        private readonly ISecretStore _primaryStore;
        private readonly ISecretStore _fallbackStore;
        private bool _useFallback;

        public SafeSecretStore(ISecretStore primaryStore, ISecretStore fallbackStore)
        {
            _primaryStore = primaryStore;
            _fallbackStore = fallbackStore;
            _useFallback = false;
        }

        public async Task<string?> GetAsync(string key)
        {
            if (_useFallback)
            {
                return await _fallbackStore.GetAsync(key);
            }

            try
            {
                return await _primaryStore.GetAsync(key);
            }
            catch
            {
                _useFallback = true;
                return await _fallbackStore.GetAsync(key);
            }
        }

        public async Task SetAsync(string key, string value)
        {
            if (_useFallback)
            {
                await _fallbackStore.SetAsync(key, value);
                return;
            }

            try
            {
                await _primaryStore.SetAsync(key, value);
            }
            catch
            {
                _useFallback = true;
                await _fallbackStore.SetAsync(key, value);
            }
        }

        public async Task DeleteAsync(string key)
        {
            if (_useFallback)
            {
                await _fallbackStore.DeleteAsync(key);
                return;
            }

            try
            {
                await _primaryStore.DeleteAsync(key);
            }
            catch
            {
                _useFallback = true;
                await _fallbackStore.DeleteAsync(key);
            }
        }
    }
}
