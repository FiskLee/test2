using Serilog;
using System;
using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    public class NullCacheService : ICacheService
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext<NullCacheService>();

        public event EventHandler<CacheChangeEventArgs>? CacheChanged;

        public NullCacheService()
        {
            _logger.Verbose("Initializing NullCacheService");
        }

        public Task<T?> GetAsync<T>(string key) where T : class
        {
            _logger.Verbose("GetAsync called with key: {Key}, Type: {Type}",
                key,
                typeof(T).Name);

            _logger.Verbose("NullCacheService always returns null");
            return Task.FromResult<T?>(default);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            _logger.Verbose("SetAsync called - Key: {Key}, Type: {Type}, Expiration: {Expiration}",
                key,
                typeof(T).Name,
                expiration?.ToString() ?? "none");

            _logger.Verbose("Raising CacheChanged event for key: {Key}", key);
            CacheChanged?.Invoke(this, new CacheChangeEventArgs(key, CacheChangeType.Added));
            return Task.CompletedTask;
        }

        public Task<bool> RemoveAsync(string key)
        {
            _logger.Verbose("RemoveAsync called with key: {Key}", key);

            _logger.Verbose("Raising CacheChanged event for key: {Key}", key);
            CacheChanged?.Invoke(this, new CacheChangeEventArgs(key, CacheChangeType.Removed));
            return Task.FromResult(true);
        }

        public Task ClearAsync()
        {
            _logger.Verbose("ClearAsync called");

            _logger.Verbose("Raising CacheChanged event for cache clear");
            CacheChanged?.Invoke(this, new CacheChangeEventArgs(string.Empty, CacheChangeType.Cleared));
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key)
        {
            _logger.Verbose("ExistsAsync called with key: {Key}", key);

            _logger.Verbose("NullCacheService always returns false");
            return Task.FromResult(false);
        }

        public Task<int> GetCountAsync()
        {
            _logger.Verbose("GetCountAsync called");

            _logger.Verbose("NullCacheService always returns 0");
            return Task.FromResult(0);
        }
    }
}