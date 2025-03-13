using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// Interface for offline storage functionality to support offline mode operations
    /// </summary>
    public interface IOfflineStorage
    {
        /// <summary>
        /// Saves data to offline storage
        /// </summary>
        /// <typeparam name="T">Type of data to save</typeparam>
        /// <param name="key">Unique identifier for the data</param>
        /// <param name="data">Data to save</param>
        /// <param name="expirationTime">Optional expiration time for the data</param>
        Task SaveAsync<T>(string key, T data, TimeSpan? expirationTime = null);

        /// <summary>
        /// Retrieves data from offline storage
        /// </summary>
        /// <typeparam name="T">Type of data to retrieve</typeparam>
        /// <param name="key">Unique identifier for the data</param>
        /// <returns>Retrieved data or default if not found</returns>
        Task<T?> GetAsync<T>(string key) where T : class;

        /// <summary>
        /// Removes data from offline storage
        /// </summary>
        /// <param name="key">Unique identifier for the data to remove</param>
        Task RemoveAsync(string key);

        /// <summary>
        /// Checks if data exists in offline storage
        /// </summary>
        /// <param name="key">Unique identifier to check</param>
        /// <returns>True if data exists and is not expired</returns>
        Task<bool> ExistsAsync(string key);

        /// <summary>
        /// Gets all keys in offline storage
        /// </summary>
        /// <returns>List of all keys</returns>
        Task<IEnumerable<string>> GetAllKeysAsync();

        /// <summary>
        /// Clears all data from offline storage
        /// </summary>
        Task ClearAsync();

        /// <summary>
        /// Gets the total size of stored data in bytes
        /// </summary>
        Task<long> GetStorageSizeAsync();

        /// <summary>
        /// Removes expired items from storage
        /// </summary>
        /// <returns>Number of items removed</returns>
        Task<int> CleanupExpiredItemsAsync();
    }
}