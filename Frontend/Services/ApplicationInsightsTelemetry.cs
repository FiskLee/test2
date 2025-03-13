using ArmaReforgerServerMonitor.Frontend.Configuration;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    public class ApplicationInsightsTelemetry : ITelemetryService, IDisposable
    {
        private readonly TelemetryClient _telemetryClient;
        private readonly ILogger _logger;
        private readonly AppSettings _settings;
        private bool _disposed;
        private bool _isEnabled;

        public event EventHandler<TelemetryStatusEventArgs>? StatusChanged;

        public ApplicationInsightsTelemetry(IOptions<AppSettings> settings)
        {
            _settings = settings.Value;
            var config = TelemetryConfiguration.CreateDefault();
            config.ConnectionString = _settings.Telemetry.ConnectionString;

            _telemetryClient = new TelemetryClient(config);
            _telemetryClient.Context.Component.Version = GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0";
            _telemetryClient.Context.Device.OperatingSystem = Environment.OSVersion.ToString();

            _logger = Log.ForContext<ApplicationInsightsTelemetry>();
            _isEnabled = _settings.Telemetry.Enabled;

            _logger.Information("Telemetry service initialized");
        }

        public bool IsEnabled()
        {
            return _isEnabled;
        }

        public void SetEnabled(bool enabled)
        {
            if (_isEnabled != enabled)
            {
                _isEnabled = enabled;
                _settings.Telemetry.Enabled = enabled;
                OnStatusChanged();
                _logger.Information("Telemetry collection {Status}", enabled ? "enabled" : "disabled");
            }
        }

        public async Task TrackEventAsync(string eventName, Dictionary<string, string>? properties = null)
        {
            if (!_isEnabled) return;

            try
            {
                await Task.Run(() => _telemetryClient.TrackEvent(eventName, properties));
                _logger.Debug("Tracked event: {EventName}", eventName);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to track event: {EventName}", eventName);
                OnStatusChanged(ex.Message);
            }
        }

        public async Task TrackErrorAsync(Exception exception, Dictionary<string, string>? properties = null, bool isFatal = false)
        {
            if (!_isEnabled) return;

            try
            {
                var props = new Dictionary<string, string>(properties ?? new Dictionary<string, string>())
                {
                    ["IsFatal"] = isFatal.ToString(),
                    ["ExceptionType"] = exception.GetType().Name
                };

                await Task.Run(() => _telemetryClient.TrackException(exception, props));
                _logger.Debug("Tracked exception: {ExceptionType}", exception.GetType().Name);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to track exception");
                OnStatusChanged(ex.Message);
            }
        }

        public async Task TrackMetricAsync(string metricName, double value, Dictionary<string, string>? properties = null)
        {
            if (!_isEnabled) return;

            try
            {
                await Task.Run(() => _telemetryClient.TrackMetric(metricName, value, properties));
                _logger.Debug("Tracked metric: {MetricName} = {MetricValue}", metricName, value);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to track metric: {MetricName}", metricName);
                OnStatusChanged(ex.Message);
            }
        }

        public async Task TrackPageViewAsync(string pageName, Dictionary<string, string>? properties = null)
        {
            if (!_isEnabled) return;

            try
            {
                await Task.Run(() =>
                {
                    _telemetryClient.TrackPageView(pageName);
                    if (properties != null)
                    {
                        foreach (var prop in properties)
                        {
                            _telemetryClient.Context.GlobalProperties[prop.Key] = prop.Value;
                        }
                    }
                });
                _logger.Debug("Tracked page view: {PageName}", pageName);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to track page view: {PageName}", pageName);
                OnStatusChanged(ex.Message);
            }
        }

        public async Task FlushAsync()
        {
            try
            {
                await Task.Run(() => _telemetryClient.Flush());
                _logger.Debug("Telemetry flushed");
            }
            catch (InvalidOperationException ex)
            {
                _logger.Error(ex, "Invalid operation during telemetry flush");
                OnStatusChanged(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error during telemetry flush");
                OnStatusChanged(ex.Message);
            }
        }

        private void OnStatusChanged(string? errorMessage = null)
        {
            StatusChanged?.Invoke(this, new TelemetryStatusEventArgs(_isEnabled, 0, errorMessage));
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                _telemetryClient.Flush();
                _logger.Information("Telemetry service disposed");
            }
            catch (InvalidOperationException ex)
            {
                _logger.Error(ex, "Invalid operation during telemetry service disposal");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error during telemetry service disposal");
            }
            finally
            {
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}