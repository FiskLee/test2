using System;
using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// Interface for managing application-wide caching of frequently accessed data.
    /// Provides methods for storing, retrieving, and managing cached items with expiration.
    /// </summary>
    /// <remarks>
    /// Key responsibilities:
    /// - Caching frequently accessed data
    /// - Managing cache expiration
    /// - Memory usage optimization
    /// - Thread-safe cache operations
    /// 
    /// This service helps improve application performance by reducing
    /// unnecessary network calls and database queries. It implements
    /// a thread-safe caching mechanism with configurable expiration policies.
    /// 
    /// Usage example:
    /// ```csharp
    /// await _cacheService.SetAsync("serverMetrics", metrics, TimeSpan.FromMinutes(5));
    /// var cachedMetrics = await _cacheService.GetAsync<OSDataDTO>("serverMetrics");
    /// ```
    /// </remarks>
    public interface ICacheService
    {
        /// <summary>
        /// Retrieves a cached item by key.
        /// </summary>
        /// <typeparam name="T">The type of the cached item</typeparam>
        /// <param name="key">The cache key</param>
        /// <returns>
        /// The cached item if found and not expired, default(T) otherwise
        /// </returns>
        /// <remarks>
        /// This method:
        /// 1. Checks if the key exists in cache
        /// 2. Verifies the item hasn't expired
        /// 3. Returns the typed value if valid
        /// 4. Handles type conversion safely
        /// </remarks>
        Task<T?> GetAsync<T>(string key) where T : class;

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
        /// 1. Validates the key and value
        /// 2. Sets expiration if provided
        /// 3. Overwrites existing items with same key
        /// 4. Manages memory usage
        /// </remarks>
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;

        /// <summary>
        /// Removes an item from the cache.
        /// </summary>
        /// <param name="key">The cache key to remove</param>
        /// <returns>True if item was removed, false if not found</returns>
        /// <remarks>
        /// This method safely removes items from cache,
        /// handling cases where the item might not exist
        /// or might have already expired.
        /// </remarks>
        Task<bool> RemoveAsync(string key);

        /// <summary>
        /// Checks if an item exists in the cache and hasn't expired.
        /// </summary>
        /// <param name="key">The cache key to check</param>
        /// <returns>True if item exists and is valid, false otherwise</returns>
        /// <remarks>
        /// This method:
        /// 1. Verifies key existence
        /// 2. Checks expiration status
        /// 3. Returns validity status
        /// 
        /// Use this to check cache state without retrieving the actual value.
        /// </remarks>
        Task<bool> ExistsAsync(string key);

        /// <summary>
        /// Clears all items from the cache.
        /// </summary>
        /// <returns>Task representing the clear operation</returns>
        /// <remarks>
        /// Use this method to:
        /// - Reset application state
        /// - Clear memory during low memory conditions
        /// - Ensure fresh data fetching
        /// 
        /// This operation is irreversible and affects all cached items.
        /// </remarks>
        Task ClearAsync();

        /// <summary>
        /// Gets the total number of items in the cache.
        /// </summary>
        /// <returns>Count of cached items</returns>
        /// <remarks>
        /// This count includes:
        /// - Both expired and non-expired items
        /// - All data types in cache
        /// 
        /// Useful for monitoring cache usage and debugging.
        /// </remarks>
        Task<int> GetCountAsync();

        /// <summary>
        /// Event that fires when items are added to or removed from cache.
        /// </summary>
        /// <remarks>
        /// This event helps track:
        /// - Cache population
        /// - Item expiration
        /// - Manual removals
        /// - Cache clearing operations
        /// </remarks>
        event EventHandler<CacheChangeEventArgs> CacheChanged;
    }

    /// <summary>
    /// Event arguments for cache change events.
    /// </summary>
    public class CacheChangeEventArgs : EventArgs
    {
        /// <summary>
        /// The key of the affected cache item
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// The type of change that occurred
        /// </summary>
        public CacheChangeType ChangeType { get; }

        /// <summary>
        /// Creates a new CacheChangeEventArgs instance.
        /// </summary>
        public CacheChangeEventArgs(string key, CacheChangeType changeType)
        {
            Key = key;
            ChangeType = changeType;
        }
    }

    /// <summary>
    /// Enumeration of possible cache change types.
    /// </summary>
    public enum CacheChangeType
    {
        /// <summary>
        /// Item was added to cache
        /// </summary>
        Added,

        /// <summary>
        /// Item was removed from cache
        /// </summary>
        Removed,

        /// <summary>
        /// Item was updated in cache
        /// </summary>
        Updated,

        /// <summary>
        /// Cache was cleared entirely
        /// </summary>
        Cleared
    }
}