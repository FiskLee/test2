using ArmaReforgerServerMonitor.Frontend.Configuration;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    internal sealed class CacheItem
    {
        public object Value { get; }
        public DateTime ExpirationTime { get; }
        public TimeSpan? SlidingExpiration { get; }

        public CacheItem(object value, DateTime expirationTime, TimeSpan? slidingExpiration = null)
        {
            Value = value;
            ExpirationTime = expirationTime;
            SlidingExpiration = slidingExpiration;
        }

        public bool IsExpired => DateTime.UtcNow >= ExpirationTime;
    }

    public class MemoryCacheService : ICacheService
    {
        private readonly ConcurrentDictionary<string, CacheItem> _cache;
        private readonly ILogger _logger;
        private readonly TimeSpan _defaultExpiration;
        private readonly Timer _cleanupTimer;
        private readonly SemaphoreSlim _lock;

        public event EventHandler<CacheChangeEventArgs>? CacheChanged;

        public MemoryCacheService(IOptions<AppSettings> settings)
        {
            _cache = new ConcurrentDictionary<string, CacheItem>();
            _logger = Log.ForContext<MemoryCacheService>();
            _defaultExpiration = TimeSpan.FromMinutes(5); // Default 5 minute cache
            _lock = new SemaphoreSlim(1, 1);

            // Start cleanup timer
            _cleanupTimer = new Timer(CleanupExpiredItems, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        }

        public async Task<T?> GetOrAddAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class
        {
            if (_cache.TryGetValue(key, out var item) && !item.IsExpired)
            {
                _logger.Debug("Cache hit for key: {Key}", key);
                return (T)item.Value;
            }

            _logger.Debug("Cache miss for key: {Key}", key);
            var value = await factory();
            await SetAsync(key, value, expiration);
            return value;
        }

        public Task<T?> GetAsync<T>(string key) where T : class
        {
            if (_cache.TryGetValue(key, out var item) && !item.IsExpired)
            {
                _logger.Debug("Retrieved value for key: {Key}", key);
                return Task.FromResult((T?)item.Value);
            }

            _logger.Debug("No value found for key: {Key}", key);
            return Task.FromResult<T?>(default);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            var expirationTime = DateTime.UtcNow.Add(expiration ?? _defaultExpiration);
            var cacheItem = new CacheItem(value!, expirationTime);

            var isUpdate = _cache.ContainsKey(key);
            _cache.AddOrUpdate(key, cacheItem, (_, _) => cacheItem);

            OnCacheChanged(key, isUpdate ? CacheChangeType.Updated : CacheChangeType.Added);
            _logger.Debug("Cached value for key: {Key}, expires at: {ExpirationTime}", key, expirationTime);

            return Task.CompletedTask;
        }

        public Task<bool> RemoveAsync(string key)
        {
            var removed = _cache.TryRemove(key, out _);
            if (removed)
            {
                OnCacheChanged(key, CacheChangeType.Removed);
                _logger.Debug("Removed cache entry for key: {Key}", key);
            }
            return Task.FromResult(removed);
        }

        public Task<bool> ExistsAsync(string key)
        {
            var exists = _cache.TryGetValue(key, out var item) && !item.IsExpired;
            _logger.Debug("Cache existence check for key: {Key}, exists: {Exists}", key, exists);
            return Task.FromResult(exists);
        }

        public Task ClearAsync()
        {
            _cache.Clear();
            OnCacheChanged(string.Empty, CacheChangeType.Cleared);
            _logger.Information("Cache cleared");
            return Task.CompletedTask;
        }

        public Task<int> GetCountAsync()
        {
            var count = _cache.Count;
            _logger.Debug("Cache count: {Count}", count);
            return Task.FromResult(count);
        }

        private void OnCacheChanged(string key, CacheChangeType changeType)
        {
            CacheChanged?.Invoke(this, new CacheChangeEventArgs(key, changeType));
        }

        private void CleanupExpiredItems(object? state)
        {
            try
            {
                var expiredKeys = _cache.Where(kvp => kvp.Value.IsExpired).Select(kvp => kvp.Key).ToList();
                foreach (var key in expiredKeys)
                {
                    _cache.TryRemove(key, out _);
                }

                if (expiredKeys.Count > 0)
                {
                    _logger.Information("Cleaned up {Count} expired cache items", expiredKeys.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error cleaning up expired cache items");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cleanupTimer?.Dispose();
                _lock?.Dispose();
            }
        }
    }
}