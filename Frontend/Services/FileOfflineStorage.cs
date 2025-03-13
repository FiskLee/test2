using ArmaReforgerServerMonitor.Frontend.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// File-based implementation of offline storage
    /// </summary>
    public class FileOfflineStorage : IOfflineStorage, IDisposable
    {
        private readonly ILogger<FileOfflineStorage> _logger;
        private readonly AppSettings _settings;
        private readonly string _storageDirectory;
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the FileOfflineStorage class
        /// </summary>
        public FileOfflineStorage(ILogger<FileOfflineStorage> logger, IOptions<AppSettings> settings)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _storageDirectory = Path.Combine(AppContext.BaseDirectory, "OfflineStorage");
            _semaphore = new SemaphoreSlim(1, 1);

            Directory.CreateDirectory(_storageDirectory);
            _logger.LogInformation("FileOfflineStorage initialized at {Directory}", _storageDirectory);
        }

        /// <inheritdoc/>
        public async Task SaveAsync<T>(string key, T data, TimeSpan? expirationTime = null)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (data == null) throw new ArgumentNullException(nameof(data));

            await _semaphore.WaitAsync();
            try
            {
                var storageItem = new StorageItem<T>
                {
                    Data = data,
                    ExpirationTime = expirationTime.HasValue ? DateTime.UtcNow.Add(expirationTime.Value) : null
                };

                var filePath = GetFilePath(key);
                var json = JsonSerializer.Serialize(storageItem);
                await File.WriteAllTextAsync(filePath, json);

                _logger.LogDebug("Saved data for key {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save data for key {Key}", key);
                throw;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            await _semaphore.WaitAsync();
            try
            {
                var filePath = GetFilePath(key);
                if (!File.Exists(filePath))
                {
                    _logger.LogDebug("No data found for key {Key}", key);
                    return null;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var storageItem = JsonSerializer.Deserialize<StorageItem<T>>(json);

                if (storageItem == null || IsExpired(storageItem))
                {
                    await RemoveAsync(key);
                    return null;
                }

                _logger.LogDebug("Retrieved data for key {Key}", key);
                return storageItem.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve data for key {Key}", key);
                return null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <inheritdoc/>
        public async Task RemoveAsync(string key)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            await _semaphore.WaitAsync();
            try
            {
                var filePath = GetFilePath(key);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogDebug("Removed data for key {Key}", key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove data for key {Key}", key);
                throw;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<bool> ExistsAsync(string key)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            await _semaphore.WaitAsync();
            try
            {
                var filePath = GetFilePath(key);
                if (!File.Exists(filePath))
                {
                    return false;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var storageItem = JsonSerializer.Deserialize<StorageItemBase>(json);

                if (storageItem == null || IsExpired(storageItem))
                {
                    await RemoveAsync(key);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check existence for key {Key}", key);
                return false;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<string>> GetAllKeysAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                var files = Directory.GetFiles(_storageDirectory, "*.json");
                return files.Select(f => Path.GetFileNameWithoutExtension(f));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get all keys");
                return Enumerable.Empty<string>();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <inheritdoc/>
        public async Task ClearAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                var files = Directory.GetFiles(_storageDirectory, "*.json");
                foreach (var file in files)
                {
                    File.Delete(file);
                }
                _logger.LogInformation("Cleared all offline storage data");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear offline storage");
                throw;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<long> GetStorageSizeAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                var files = Directory.GetFiles(_storageDirectory, "*.json");
                return files.Sum(f => new FileInfo(f).Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get storage size");
                return 0;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<int> CleanupExpiredItemsAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                var files = Directory.GetFiles(_storageDirectory, "*.json");
                var removedCount = 0;

                foreach (var file in files)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(file);
                        var storageItem = JsonSerializer.Deserialize<StorageItemBase>(json);

                        if (storageItem == null || IsExpired(storageItem))
                        {
                            File.Delete(file);
                            removedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing file {File} during cleanup", file);
                        // Continue with other files even if one fails
                    }
                }

                _logger.LogInformation("Cleaned up {Count} expired items", removedCount);
                return removedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup expired items");
                return 0;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private string GetFilePath(string key)
        {
            return Path.Combine(_storageDirectory, $"{key}.json");
        }

        private static bool IsExpired(StorageItemBase item)
        {
            return item.ExpirationTime.HasValue && item.ExpirationTime.Value < DateTime.UtcNow;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _semaphore.Dispose();
                _disposed = true;
            }
        }

        private class StorageItemBase
        {
            public DateTime? ExpirationTime { get; set; }
        }

        private class StorageItem<T> : StorageItemBase
        {
            public T? Data { get; set; }
        }
    }
}