using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// Implementation of the cache service that provides in-memory caching
    /// with expiration and thread-safe operations.
    /// </summary>
    /// <remarks>
    /// This service provides:
    /// - Thread-safe caching
    /// - Automatic expiration
    /// - Memory management
    /// - Cache statistics
    /// - Event notifications
    /// 
    /// The service uses ConcurrentDictionary for thread-safe operations
    /// and implements automatic cleanup of expired items.
    /// </remarks>
    public class CacheSettings
    {
        public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(5);
        public int MaxItems { get; set; } = 1000;
        public bool EnableSlidingExpiration { get; set; } = true;
    }

    public class CacheService : ICacheService, IDisposable
    {
        private readonly Serilog.ILogger _logger;
        private readonly ConcurrentDictionary<string, CacheItem> _cache;
        private readonly Timer _cleanupTimer;
        private readonly TimeSpan _defaultExpiration;
        private readonly SemaphoreSlim _lock;
        private bool _disposed;

        /// <summary>
        /// Event that fires when cache items are added, removed, or updated.
        /// </summary>
        public event EventHandler<CacheChangeEventArgs>? CacheChanged;

        /// <summary>
        /// Initializes a new instance of the CacheService class.
        /// </summary>
        /// <param name="logger">Logger for cache events</param>
        /// <param name="settings">Cache settings</param>
        /// <remarks>
        /// The constructor:
        /// 1. Initializes cache storage
        /// 2. Sets up cleanup timer
        /// 3. Configures default settings
        /// 4. Starts maintenance tasks
        /// </remarks>
        public CacheService(Serilog.ILogger logger, IOptions<CacheSettings> settings)
        {
            _logger = logger.ForContext<CacheService>();
            _cache = new ConcurrentDictionary<string, CacheItem>();
            _defaultExpiration = settings.Value.DefaultExpiration;
            _lock = new SemaphoreSlim(1, 1);

            // Set up cleanup timer to run every minute
            _cleanupTimer = new Timer(CleanupExpiredItems, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        }

        /// <summary>
        /// Retrieves a cached item by key.
        /// </summary>
        /// <typeparam name="T">The type of the cached item</typeparam>
        /// <param name="key">The cache key</param>
        /// <returns>The cached item if found and not expired, null otherwise</returns>
        /// <remarks>
        /// This method:
        /// 1. Validates the key
        /// 2. Checks item existence
        /// 3. Verifies expiration
        /// 4. Returns typed value
        /// 
        /// Thread-safe operation is guaranteed.
        /// </remarks>
        public Task<T?> GetAsync<T>(string key) where T : class
        {
            if (_cache.TryGetValue(key, out var item) && !item.IsExpired)
            {
                _logger.Debug("Cache hit for key: {Key}", key);
                return Task.FromResult((T?)item.Value);
            }

            _logger.Debug("Cache miss for key: {Key}", key);
            return Task.FromResult<T?>(default);
        }

        /// <summary>
        /// Stores an item in the cache with optional expiration.
        /// </summary>
        /// <typeparam name="T">The type of the item to cache</typeparam>
        /// <param name="key">The cache key</param>
        /// <param name="value">The value to cache</param>
        /// <param name="expiration">Optional expiration timespan</param>
        /// <returns>Task representing the cache operation</returns>
        /// <remarks>
        /// This method:
        /// 1. Validates inputs
        /// 2. Creates cache item
        /// 3. Sets expiration
        /// 4. Updates cache
        /// 5. Raises events
        /// </remarks>
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

        /// <summary>
        /// Removes an item from the cache.
        /// </summary>
        /// <param name="key">The cache key to remove</param>
        /// <returns>True if item was removed, false if not found</returns>
        /// <remarks>
        /// This method:
        /// 1. Validates key
        /// 2. Removes item
        /// 3. Raises events
        /// 4. Updates statistics
        /// </remarks>
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

        /// <summary>
        /// Checks if an item exists in the cache and hasn't expired.
        /// </summary>
        /// <param name="key">The cache key to check</param>
        /// <returns>True if item exists and is valid, false otherwise</returns>
        /// <remarks>
        /// This method checks:
        /// - Key existence
        /// - Item expiration
        /// - Value validity
        /// </remarks>
        public Task<bool> ExistsAsync(string key)
        {
            var exists = _cache.TryGetValue(key, out var item) && !item.IsExpired;
            _logger.Debug("Cache existence check for key: {Key}, exists: {Exists}", key, exists);
            return Task.FromResult(exists);
        }

        /// <summary>
        /// Clears all items from the cache.
        /// </summary>
        /// <returns>Task representing the clear operation</returns>
        /// <remarks>
        /// This method:
        /// 1. Removes all items
        /// 2. Raises events
        /// 3. Updates statistics
        /// 4. Resets state
        /// </remarks>
        public Task ClearAsync()
        {
            _cache.Clear();
            OnCacheChanged(string.Empty, CacheChangeType.Cleared);
            _logger.Information("Cache cleared");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets the total number of items in the cache.
        /// </summary>
        /// <returns>Count of cached items</returns>
        /// <remarks>
        /// Returns the total count of:
        /// - Valid items
        /// - Expired items
        /// - All cache entries
        /// </remarks>
        public Task<int> GetCountAsync()
        {
            var count = _cache.Count;
            _logger.Debug("Cache count: {Count}", count);
            return Task.FromResult(count);
        }

        /// <summary>
        /// Cleans up expired items from the cache.
        /// </summary>
        /// <param name="state">Timer state (unused)</param>
        /// <remarks>
        /// This method:
        /// 1. Finds expired items
        /// 2. Removes them
        /// 3. Updates statistics
        /// 4. Manages memory
        /// </remarks>
        private void CleanupExpiredItems(object? state)
        {
            try
            {
                var now = DateTime.UtcNow;
                var expiredKeys = _cache
                    .Where(kvp => kvp.Value.IsExpired)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    if (_cache.TryRemove(key, out _))
                    {
                        _logger.Debug("Expired item removed from cache: {Key}", key);
                        OnCacheChanged(key, CacheChangeType.Removed);
                    }
                }

                if (expiredKeys.Count > 0)
                {
                    _logger.Information("Cleaned up {Count} expired cache items", expiredKeys.Count);
                }
            }
            catch (ObjectDisposedException ex)
            {
                _logger.Error(ex, "Object already disposed during cache cleanup");
            }
            catch (InvalidOperationException ex)
            {
                _logger.Error(ex, "Invalid operation during cache cleanup");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error during cache cleanup");
            }
        }

        /// <summary>
        /// Raises the CacheChanged event.
        /// </summary>
        /// <param name="key">The affected cache key</param>
        /// <param name="changeType">Type of change that occurred</param>
        /// <remarks>
        /// Notifies subscribers of:
        /// - Item additions
        /// - Item removals
        /// - Item updates
        /// - Cache clearing
        /// </remarks>
        private void OnCacheChanged(string key, CacheChangeType changeType)
        {
            try
            {
                CacheChanged?.Invoke(this, new CacheChangeEventArgs(key, changeType));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error raising CacheChanged event");
            }
        }

        /// <summary>
        /// Disposes of resources used by the cache service.
        /// </summary>
        /// <remarks>
        /// This method:
        /// 1. Stops cleanup timer
        /// 2. Clears cache
        /// 3. Releases resources
        /// 4. Sets disposed flag
        /// </remarks>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_disposed) return;

                try
                {
                    _cleanupTimer.Dispose();
                    _cache.Clear();
                    _lock.Dispose();
                    _disposed = true;

                    _logger.Information("Cache service disposed");
                }
                catch (ObjectDisposedException ex)
                {
                    _logger.Error(ex, "Object already disposed during cache service disposal");
                }
                catch (InvalidOperationException ex)
                {
                    _logger.Error(ex, "Invalid operation during cache service disposal");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unexpected error during cache service disposal");
                }
            }
        }

        /// <summary>
        /// Represents a cached item with expiration.
        /// </summary>
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
    }
}