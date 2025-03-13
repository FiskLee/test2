using ArmaReforgerServerMonitor.Frontend.Configuration;
using ArmaReforgerServerMonitor.Frontend.Models;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// Implementation of the metrics service that retrieves and manages
    /// server performance metrics and statistics.
    /// </summary>
    /// <remarks>
    /// This service provides:
    /// - Real-time performance metrics
    /// - Console log statistics
    /// - Raw server data access
    /// - Backend log retrieval
    /// - Metrics caching
    /// 
    /// The service handles data retrieval, caching, and error handling
    /// for all server metrics and performance data.
    /// </remarks>
    internal class MetricsService : IMetricsService, IDisposable
    {
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;
        private readonly ICacheService _cacheService;
        private readonly AppSettings _settings;
        private readonly string _baseUrl;
        private readonly ConcurrentDictionary<string, MetricValue> _metrics;
        private readonly ConcurrentDictionary<string, DateTime> _lastUpdateTimes;
        private bool _isDisposed;
        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        /// <summary>
        /// Initializes a new instance of the MetricsService class.
        /// </summary>
        /// <param name="httpClient">HTTP client for API requests</param>
        /// <param name="cacheService">Cache service for metrics data</param>
        /// <param name="settings">Application settings</param>
        /// <param name="baseUrl">The base URL of the backend server</param>
        /// <remarks>
        /// The constructor initializes:
        /// - HTTP client configuration
        /// - Caching settings
        /// - Logging services
        /// - Retry policies
        /// </remarks>
        public MetricsService(
            HttpClient httpClient,
            ICacheService cacheService,
            IOptions<AppSettings> settings,
            string baseUrl)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
            _metrics = new ConcurrentDictionary<string, MetricValue>();
            _lastUpdateTimes = new ConcurrentDictionary<string, DateTime>();
            _logger = Log.ForContext<MetricsService>();

            _logger.Verbose("MetricsService initialized with base URL: {BaseUrl}", _baseUrl);
            _logger.Debug("Metrics cache initialized with {Count} entries", _metrics.Count);
        }

        /// <summary>
        /// Retrieves operating system metrics from the server.
        /// </summary>
        /// <param name="serverUri">The URI of the server</param>
        /// <returns>OS metrics data or null if retrieval fails</returns>
        /// <remarks>
        /// This method:
        /// 1. Checks cache for recent data
        /// 2. Makes API request if needed
        /// 3. Processes and validates response
        /// 4. Updates cache with new data
        /// 5. Handles errors and timeouts
        /// 
        /// The data is cached based on the configured polling interval.
        /// </remarks>
        public async Task<OSDataDTO?> GetOSMetricsAsync(Uri serverUri)
        {
            if (serverUri == null)
            {
                _logger.Warning("Attempted to get OS metrics with null server URI");
                throw new ArgumentNullException(nameof(serverUri));
            }

            try
            {
                _logger.Verbose("Retrieving OS metrics for server {ServerUri}", serverUri);
                var endpoint = new Uri(serverUri, "/api/metrics/os");
                _logger.Debug("Making HTTP request to {Url}", endpoint);

                using var response = await _httpClient.GetAsync(endpoint).ConfigureAwait(false);
                _logger.Verbose("Received response with status code {StatusCode}", response.StatusCode);

                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.Debug("Received metrics data: {Content}", content);

                var metrics = ParseMetrics(content);
                if (metrics == null)
                {
                    _logger.Warning("Failed to parse metrics data for server {ServerUri}", serverUri);
                    return null;
                }

                _logger.Information("Successfully retrieved OS metrics for server {ServerUri}: CPU={Cpu}%, Memory={Memory}%, Disk={Disk}%, Network={Network}%",
                    serverUri, metrics.CpuUsage, metrics.MemoryUsage, metrics.DiskUsage, metrics.NetworkUsage);

                return metrics;
            }
            catch (HttpRequestException ex)
            {
                _logger.Error(ex, "HTTP request failed for server {ServerUri} - Status: {StatusCode}, Message: {Message}",
                    serverUri, ex.StatusCode, ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error retrieving OS metrics for server {ServerUri} - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    serverUri, ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        private OSDataDTO? ParseMetrics(string content)
        {
            try
            {
                _logger.Verbose("Parsing metrics content: {Content}", content);
                var metrics = new Dictionary<string, double>();
                var lines = content.Split('\n');

                foreach (var line in lines)
                {
                    var parts = line.Split('=');
                    if (parts.Length == 2 && double.TryParse(parts[1].Trim(), NumberStyles.Any, InvariantCulture, out double value))
                    {
                        metrics[parts[0].Trim()] = value;
                        _logger.Verbose("Parsed metric: {Key} = {Value}", parts[0].Trim(), value);
                    }
                }

                double cpuUsage = 0, memoryUsage = 0, diskUsage = 0, networkUsage = 0;

                metrics.TryGetValue("cpu_usage", out cpuUsage);
                metrics.TryGetValue("memory_usage", out memoryUsage);
                metrics.TryGetValue("disk_usage", out diskUsage);
                metrics.TryGetValue("network_usage", out networkUsage);

                _logger.Debug("Parsed metrics values - CPU: {Cpu}%, Memory: {Memory}%, Disk: {Disk}%, Network: {Network}%",
                    cpuUsage, memoryUsage, diskUsage, networkUsage);

                return new OSDataDTO
                {
                    CpuUsage = cpuUsage,
                    MemoryUsage = memoryUsage,
                    DiskUsage = diskUsage,
                    NetworkUsage = networkUsage,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to parse metrics data - Content: {Content}, Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    content, ex.GetType().Name, ex.Message, ex.StackTrace);
                return null;
            }
        }

        private static bool ValidateServerId(string serverId)
        {
            return !string.IsNullOrWhiteSpace(serverId);
        }

        /// <summary>
        /// Retrieves console log statistics from the server.
        /// </summary>
        /// <param name="baseUrl">The base URL of the backend server</param>
        /// <returns>Formatted console log statistics</returns>
        /// <remarks>
        /// This method retrieves and formats:
        /// - Log entry counts
        /// - Error statistics
        /// - Recent log entries
        /// - Performance impact data
        /// 
        /// The data is cached briefly to prevent excessive requests.
        /// </remarks>
        public async Task<string> GetConsoleLogStatsAsync(Uri baseUrl)
        {
            try
            {
                _logger.Debug("Retrieving console log statistics from {BaseUrl}", baseUrl);

                var cacheKey = $"consolestats_{baseUrl}";
                var cachedStats = await _cacheService.GetAsync<string>(cacheKey);
                if (cachedStats != null)
                {
                    return cachedStats;
                }

                var response = await _httpClient.GetAsync($"{baseUrl}/api/data/consolelogstats");
                if (response.IsSuccessStatusCode)
                {
                    var stats = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(stats))
                    {
                        await _cacheService.SetAsync(cacheKey, stats,
                            TimeSpan.FromSeconds(_settings.PollIntervalSeconds));

                        _logger.Debug("Successfully retrieved console log statistics");
                        return stats;
                    }
                }

                var error = $"Failed to retrieve console log statistics: {response.StatusCode}";
                _logger.Warning(error);
                return error;
            }
            catch (Exception ex)
            {
                var error = $"Error retrieving console log statistics: {ex.Message}";
                _logger.Error(ex, error);
                return error;
            }
        }

        /// <summary>
        /// Retrieves raw server data for debugging and analysis.
        /// </summary>
        /// <param name="baseUrl">The base URL of the backend server</param>
        /// <returns>Raw server data in JSON format</returns>
        /// <remarks>
        /// This method provides access to:
        /// - Unprocessed metrics
        /// - Debug information
        /// - Server state data
        /// - Configuration details
        /// 
        /// The data is not cached due to its debug nature.
        /// </remarks>
        public async Task<string> GetRawDataAsync(Uri baseUrl)
        {
            try
            {
                _logger.Debug("Retrieving raw server data from {BaseUrl}", baseUrl);

                var response = await _httpClient.GetAsync($"{baseUrl}/api/data/rawdata");
                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadAsStringAsync();
                    _logger.Debug("Successfully retrieved raw server data");
                    return data;
                }

                var error = $"Failed to retrieve raw data: {response.StatusCode}";
                _logger.Warning(error);
                return error;
            }
            catch (Exception ex)
            {
                var error = $"Error retrieving raw data: {ex.Message}";
                _logger.Error(ex, error);
                return error;
            }
        }

        /// <summary>
        /// Retrieves backend server logs for troubleshooting.
        /// </summary>
        /// <param name="baseUrl">The base URL of the backend server</param>
        /// <returns>Formatted backend logs</returns>
        /// <remarks>
        /// This method retrieves:
        /// - Server error logs
        /// - System events
        /// - Performance logs
        /// - Debug information
        /// 
        /// The logs are not cached to ensure fresh data.
        /// </remarks>
        public async Task<string> GetBackendLogsAsync(Uri baseUrl)
        {
            try
            {
                _logger.Debug("Retrieving backend logs from {BaseUrl}", baseUrl);

                var response = await _httpClient.GetAsync($"{baseUrl}/api/data/backendlogs");
                if (response.IsSuccessStatusCode)
                {
                    var logs = await response.Content.ReadAsStringAsync();
                    _logger.Debug("Successfully retrieved backend logs");
                    return logs;
                }

                var error = $"Failed to retrieve backend logs: {response.StatusCode}";
                _logger.Warning(error);
                return error;
            }
            catch (Exception ex)
            {
                var error = $"Error retrieving backend logs: {ex.Message}";
                _logger.Error(ex, error);
                return error;
            }
        }

        private static string FormatMetricValue(double value)
        {
            return value.ToString("F2", InvariantCulture);
        }

        public void UpdateMetric(string name, double value, string? unit = null)
        {
            try
            {
                _logger.Verbose("Updating metric {Name} to {Value}{Unit}", name, value, unit ?? string.Empty);
                var metric = new MetricValue
                {
                    Name = name,
                    Value = value,
                    Unit = unit ?? string.Empty,
                    Timestamp = DateTime.UtcNow
                };

                var wasAdded = _metrics.AddOrUpdate(name, metric, (_, __) => metric);
                _lastUpdateTimes.AddOrUpdate(name, DateTime.UtcNow, (_, __) => DateTime.UtcNow);

                _logger.Debug("Metric {Name} updated successfully - New value: {Value}{Unit}, Timestamp: {Timestamp}",
                    name, value, unit ?? string.Empty, metric.Timestamp);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update metric {Name} - Value: {Value}, Unit: {Unit}, Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    name, value, unit, ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        public MetricValue? GetMetric(string name)
        {
            try
            {
                _logger.Verbose("Retrieving metric {Name}", name);
                if (_metrics.TryGetValue(name, out var metric))
                {
                    _logger.Debug("Metric {Name} retrieved successfully - Value: {Value}{Unit}, Timestamp: {Timestamp}",
                        name, metric.Value, metric.Unit ?? string.Empty, metric.Timestamp);
                    return metric;
                }

                _logger.Warning("Metric {Name} not found in cache", name);
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve metric {Name} - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    name, ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        public Task<bool> IsMetricStaleAsync(string name, TimeSpan? maxAge = null)
        {
            try
            {
                _logger.Verbose("Checking if metric {Name} is stale", name);
                if (!_lastUpdateTimes.TryGetValue(name, out var lastUpdate))
                {
                    _logger.Warning("Metric {Name} has never been updated", name);
                    return Task.FromResult(true);
                }

                var age = DateTime.UtcNow - lastUpdate;
                var threshold = maxAge ?? TimeSpan.FromMinutes(5);
                var isStale = age > threshold;

                _logger.Debug("Metric {Name} age check - Age: {Age}, Threshold: {Threshold}, IsStale: {IsStale}",
                    name, age.TotalMinutes, threshold.TotalMinutes, isStale);
                return Task.FromResult(isStale);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to check if metric {Name} is stale - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    name, ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        public void ClearMetrics()
        {
            try
            {
                _logger.Verbose("Clearing all metrics");
                var count = _metrics.Count;
                _metrics.Clear();
                _lastUpdateTimes.Clear();
                _logger.Information("Cleared {Count} metrics from cache", count);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to clear metrics - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _logger.Verbose("Disposing MetricsService");
                    _httpClient?.Dispose();
                    _metrics.Clear();
                    _lastUpdateTimes.Clear();
                    _logger.Information("MetricsService disposed successfully");
                }
                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}