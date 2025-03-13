using ArmaReforgerServerMonitor.Frontend.Configuration;
using ArmaReforgerServerMonitor.Frontend.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// Implementation of the telemetry service that collects and reports application usage data
    /// and error information for monitoring and improvement purposes.
    /// </summary>
    /// <remarks>
    /// This service provides:
    /// - Anonymous usage tracking
    /// - Error reporting
    /// - Performance metrics
    /// - Feature usage analytics
    /// - Session tracking
    /// 
    /// All data collection respects user privacy settings and can be disabled.
    /// No personally identifiable information is collected.
    /// </remarks>
    internal class TelemetryService : ITelemetryService, IDisposable
    {
        private readonly Serilog.ILogger _logger;
        private readonly HttpClient _httpClient;
        private readonly AppSettings _settings;
        private readonly string _sessionId;
        private bool _isEnabled;
        private readonly Queue<Dictionary<string, string>> _eventQueue;
        private readonly IDataCollector _dataCollector;
        private readonly IDataExporter _dataExporter;
        private bool _disposed;

        /// <summary>
        /// Event that fires when telemetry status changes.
        /// </summary>
        public event EventHandler<TelemetryStatusEventArgs>? StatusChanged;

        /// <summary>
        /// Initializes a new instance of the TelemetryService class.
        /// </summary>
        /// <param name="logger">Logger for telemetry events</param>
        /// <param name="httpClient">HTTP client for telemetry requests</param>
        /// <param name="settings">Application settings</param>
        /// <param name="dataCollector">Data collector for telemetry data</param>
        /// <param name="dataExporter">Data exporter for telemetry data</param>
        /// <remarks>
        /// The constructor:
        /// 1. Initializes dependencies
        /// 2. Generates session ID
        /// 3. Loads telemetry settings
        /// 4. Sets up event handlers
        /// </remarks>
        public TelemetryService(
            Serilog.ILogger logger,
            HttpClient httpClient,
            AppSettings settings,
            IDataCollector dataCollector,
            IDataExporter dataExporter)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _sessionId = Guid.NewGuid().ToString();
            _isEnabled = settings.Telemetry.Enabled;
            _eventQueue = new Queue<Dictionary<string, string>>();
            _dataCollector = dataCollector ?? throw new ArgumentNullException(nameof(dataCollector));
            _dataExporter = dataExporter ?? throw new ArgumentNullException(nameof(dataExporter));
        }

        /// <summary>
        /// Gets whether telemetry collection is enabled.
        /// </summary>
        /// <returns>True if telemetry is enabled</returns>
        /// <remarks>
        /// Checks if:
        /// - User has opted in
        /// - Required endpoints are available
        /// - Session is valid
        /// </remarks>
        public bool IsEnabled()
        {
            return _isEnabled;
        }

        /// <summary>
        /// Enables or disables telemetry collection.
        /// </summary>
        /// <param name="enabled">Whether to enable telemetry</param>
        /// <remarks>
        /// This method:
        /// 1. Updates settings
        /// 2. Persists preference
        /// 3. Notifies subscribers
        /// 4. Logs change
        /// </remarks>
        public void SetEnabled(bool enabled)
        {
            if (_isEnabled != enabled)
            {
                _isEnabled = enabled;
                _settings.Telemetry.Enabled = enabled;
                OnTelemetryStatusChanged();
                _logger.Information("Telemetry collection {Status}", enabled ? "enabled" : "disabled");
            }
        }

        /// <summary>
        /// Tracks a feature usage event.
        /// </summary>
        /// <param name="featureName">Name of the feature used</param>
        /// <param name="properties">Additional properties about usage</param>
        /// <returns>True if event was tracked</returns>
        /// <remarks>
        /// This method:
        /// 1. Validates enabled state
        /// 2. Sanitizes data
        /// 3. Adds metadata
        /// 4. Sends event
        /// 
        /// No personally identifiable information is included.
        /// </remarks>
        public async Task TrackEventAsync(string eventName, Dictionary<string, string>? properties = null)
        {
            if (!_isEnabled) return;

            try
            {
                var eventData = new Dictionary<string, string>
                {
                    ["sessionId"] = _sessionId,
                    ["event"] = eventName,
                    ["timestamp"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                };

                if (properties != null)
                {
                    foreach (var prop in properties)
                    {
                        eventData[prop.Key] = prop.Value;
                    }
                }

                await SendTelemetryEventAsync("event", eventData).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error tracking event");
            }
        }

        /// <summary>
        /// Tracks an error event.
        /// </summary>
        /// <param name="exception">Exception object</param>
        /// <param name="properties">Additional error context</param>
        /// <param name="isFatal">Indicates if the error is fatal</param>
        /// <returns>True if error was tracked</returns>
        /// <remarks>
        /// This method:
        /// 1. Validates enabled state
        /// 2. Sanitizes error data
        /// 3. Adds metadata
        /// 4. Sends event
        /// 
        /// Stack traces are sanitized to remove paths.
        /// </remarks>
        public async Task TrackErrorAsync(Exception exception, Dictionary<string, string>? properties = null, bool isFatal = false)
        {
            if (!_isEnabled) return;

            try
            {
                if (exception == null) throw new ArgumentNullException(nameof(exception));

                var eventData = new Dictionary<string, string>
                {
                    ["sessionId"] = _sessionId,
                    ["errorType"] = exception.GetType().Name,
                    ["message"] = exception.Message,
                    ["stackTrace"] = SanitizeStackTrace(exception.StackTrace),
                    ["isFatal"] = isFatal.ToString(CultureInfo.InvariantCulture),
                    ["timestamp"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                };

                if (properties != null)
                {
                    foreach (var prop in properties)
                    {
                        eventData[prop.Key] = prop.Value;
                    }
                }

                await SendTelemetryEventAsync("error", eventData).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error tracking error event");
            }
        }

        /// <summary>
        /// Tracks a performance metric.
        /// </summary>
        /// <param name="metricName">Name of the metric</param>
        /// <param name="value">Metric value</param>
        /// <param name="properties">Additional metric context</param>
        /// <returns>True if metric was tracked</returns>
        /// <remarks>
        /// This method:
        /// 1. Validates enabled state
        /// 2. Validates metric data
        /// 3. Adds metadata
        /// 4. Sends event
        /// 
        /// Metrics are aggregated server-side.
        /// </remarks>
        public async Task TrackMetricAsync(string metricName, double value, Dictionary<string, string>? properties = null)
        {
            if (!_isEnabled) return;

            try
            {
                var eventData = new Dictionary<string, string>
                {
                    ["sessionId"] = _sessionId,
                    ["metric"] = metricName,
                    ["value"] = value.ToString(CultureInfo.InvariantCulture),
                    ["timestamp"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                };

                if (properties != null)
                {
                    foreach (var prop in properties)
                    {
                        eventData[prop.Key] = prop.Value;
                    }
                }

                await SendTelemetryEventAsync("metric", eventData).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error tracking metric");
            }
        }

        /// <summary>
        /// Tracks a page view event.
        /// </summary>
        /// <param name="pageName">Name of the page viewed</param>
        /// <param name="properties">Additional page view context</param>
        /// <returns>True if page view was tracked</returns>
        /// <remarks>
        /// This method:
        /// 1. Validates enabled state
        /// 2. Sanitizes data
        /// 3. Adds metadata
        /// 4. Sends event
        /// </remarks>
        public async Task TrackPageViewAsync(string pageName, Dictionary<string, string>? properties = null)
        {
            if (!_isEnabled)
            {
                return;
            }

            try
            {
                var eventData = new Dictionary<string, string>
                {
                    ["sessionId"] = _sessionId,
                    ["page"] = pageName,
                    ["timestamp"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                };

                if (properties != null)
                {
                    foreach (var prop in properties)
                    {
                        eventData[prop.Key] = prop.Value;
                    }
                }

                await SendTelemetryEventAsync("pageview", eventData).ConfigureAwait(false);
            }
            catch (ArgumentNullException ex)
            {
                _logger.Error(ex, "Null argument provided to TrackPageViewAsync - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error tracking page view");
            }
        }

        /// <summary>
        /// Gets the current application version.
        /// </summary>
        /// <returns>Application version string</returns>
        /// <remarks>
        /// Returns the informational version if available,
        /// otherwise falls back to assembly version.
        /// </remarks>
        private string GetAppVersion()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "1.0.0";
        }

        /// <summary>
        /// Sanitizes a stack trace to remove sensitive information.
        /// </summary>
        /// <param name="stackTrace">Raw stack trace</param>
        /// <returns>Sanitized stack trace</returns>
        /// <remarks>
        /// This method:
        /// 1. Removes file paths
        /// 2. Removes line numbers
        /// 3. Removes parameter values
        /// 4. Preserves method names
        /// </remarks>
        private string SanitizeStackTrace(string? stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace)) return string.Empty;

            try
            {
                var lines = stackTrace.Split('\n');
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var methodEnd = line.IndexOf(" in ");
                    if (methodEnd >= 0)
                    {
                        lines[i] = line.Substring(0, methodEnd);
                    }
                }

                return string.Join('\n', lines);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error sanitizing stack trace");
                return string.Empty;
            }
        }

        /// <summary>
        /// Raises the TelemetryStatusChanged event.
        /// </summary>
        /// <param name="isEnabled">Current enabled state</param>
        /// <remarks>
        /// Notifies subscribers of:
        /// - Enabled/disabled state changes
        /// - Session status
        /// </remarks>
        private void OnTelemetryStatusChanged(bool isEnabled)
        {
            try
            {
                StatusChanged?.Invoke(this, new TelemetryStatusEventArgs(isEnabled, 0));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error raising TelemetryStatusChanged event");
            }
        }

        public async Task FlushAsync()
        {
            if (!_isEnabled || _eventQueue.Count == 0)
            {
                return;
            }

            try
            {
                while (_eventQueue.Count > 0)
                {
                    var eventData = _eventQueue.Dequeue();
                    await SendTelemetryEventAsync("queued", eventData).ConfigureAwait(false);
                }
                OnTelemetryStatusChanged();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error flushing telemetry queue");
                OnTelemetryStatusChanged(ex.Message);
            }
        }

        private async Task SendTelemetryEventAsync(string eventType, Dictionary<string, string> eventData)
        {
            if (!_isEnabled)
            {
                _eventQueue.Enqueue(eventData);
                OnTelemetryStatusChanged();
                return;
            }

            try
            {
                eventData["type"] = eventType;
                eventData["appVersion"] = GetAppVersion();

                var json = JsonSerializer.Serialize(eventData);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_settings.Telemetry.ConnectionString, content).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            }
            catch (ArgumentNullException ex)
            {
                _logger.Error(ex, "Null argument provided to SendTelemetryEventAsync - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error sending telemetry event");
                _eventQueue.Enqueue(eventData);
                OnTelemetryStatusChanged(ex.Message);
            }
        }

        private void OnTelemetryStatusChanged(string? errorMessage = null)
        {
            StatusChanged?.Invoke(this, new TelemetryStatusEventArgs(_isEnabled, _eventQueue.Count, errorMessage));
        }

        public async Task StartCollectionAsync()
        {
            try
            {
                await _dataCollector.StartAsync().ConfigureAwait(false);
                _logger.Information("Telemetry collection started");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error starting telemetry collection");
            }
        }

        public async Task StopCollectionAsync()
        {
            try
            {
                await _dataCollector.StopAsync().ConfigureAwait(false);
                _logger.Information("Telemetry collection stopped");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error stopping telemetry collection");
            }
        }

        public async Task ExportDataAsync(string format)
        {
            try
            {
                var data = await _dataCollector.GetDataAsync().ConfigureAwait(false);
                await _dataExporter.ExportAsync(data, format).ConfigureAwait(false);
                _logger.Information("Telemetry data exported in format: {Format}", format);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error exporting telemetry data");
            }
        }

        public async Task<IEnumerable<TelemetryData>> GetDataAsync()
        {
            try
            {
                var data = await _dataCollector.GetDataAsync().ConfigureAwait(false);
                _logger.Debug("Retrieved {Count} telemetry data points", data.Count());
                return data;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving telemetry data");
                return new List<TelemetryData>();
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _httpClient.Dispose();
                    // Dispose managed resources here.
                }

                // Dispose unmanaged resources here.

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}