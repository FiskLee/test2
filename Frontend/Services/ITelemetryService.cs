using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// Interface for managing application telemetry and usage analytics.
    /// Provides methods for tracking events, errors, and performance metrics.
    /// </summary>
    /// <remarks>
    /// Key responsibilities:
    /// - Tracking application usage
    /// - Collecting performance metrics
    /// - Error reporting
    /// - User behavior analytics
    /// 
    /// This service helps improve the application by collecting anonymous usage data
    /// and error reports. It respects user privacy settings and provides opt-out options.
    /// Data collection adheres to GDPR and privacy best practices.
    /// 
    /// Usage example:
    /// ```csharp
    /// await _telemetryService.TrackEventAsync("ServerConnection", 
    ///     new Dictionary<string, string> { { "Status", "Success" } });
    /// ```
    /// </remarks>
    public interface ITelemetryService
    {
        /// <summary>
        /// Tracks a custom event with optional properties.
        /// </summary>
        /// <param name="eventName">Name of the event to track</param>
        /// <param name="properties">Optional properties to include with the event</param>
        /// <returns>Task representing the tracking operation</returns>
        /// <remarks>
        /// This method:
        /// 1. Validates the event name and properties
        /// 2. Adds standard properties (app version, OS, etc.)
        /// 3. Respects privacy settings
        /// 4. Queues event for transmission
        /// 
        /// Events are used to track user actions and application milestones.
        /// </remarks>
        Task TrackEventAsync(string eventName, Dictionary<string, string>? properties = null);

        /// <summary>
        /// Tracks a performance metric.
        /// </summary>
        /// <param name="metricName">Name of the metric</param>
        /// <param name="value">Metric value</param>
        /// <param name="properties">Optional properties to include</param>
        /// <returns>Task representing the tracking operation</returns>
        /// <remarks>
        /// Used for tracking:
        /// - Response times
        /// - Resource usage
        /// - Operation durations
        /// - Custom performance indicators
        /// </remarks>
        Task TrackMetricAsync(string metricName, double value, Dictionary<string, string>? properties = null);

        /// <summary>
        /// Tracks a page or view navigation.
        /// </summary>
        /// <param name="pageName">Name of the page or view</param>
        /// <param name="properties">Optional properties to include</param>
        /// <returns>Task representing the tracking operation</returns>
        /// <remarks>
        /// This method tracks:
        /// - Screen navigation
        /// - View transitions
        /// - Time spent on pages
        /// - Navigation patterns
        /// </remarks>
        Task TrackPageViewAsync(string pageName, Dictionary<string, string>? properties = null);

        /// <summary>
        /// Enables or disables telemetry collection.
        /// </summary>
        /// <param name="enabled">Whether telemetry should be enabled</param>
        /// <remarks>
        /// This setting:
        /// - Persists between sessions
        /// - Affects all telemetry types
        /// - Can be changed at runtime
        /// - Respects user preferences
        /// </remarks>
        void SetEnabled(bool enabled);

        /// <summary>
        /// Gets whether telemetry collection is currently enabled.
        /// </summary>
        /// <returns>True if telemetry is enabled, false otherwise</returns>
        /// <remarks>
        /// Checks both:
        /// - User preference setting
        /// - System capability
        /// </remarks>
        bool IsEnabled();

        /// <summary>
        /// Flushes any queued telemetry data.
        /// </summary>
        /// <returns>Task representing the flush operation</returns>
        /// <remarks>
        /// This method:
        /// 1. Sends any queued events
        /// 2. Ensures data transmission
        /// 3. Clears local queue
        /// 
        /// Useful before application shutdown.
        /// </remarks>
        Task FlushAsync();

        /// <summary>
        /// Event that fires when telemetry status changes.
        /// </summary>
        /// <remarks>
        /// Notifies subscribers of:
        /// - Enable/disable changes
        /// - Transmission status
        /// - Error conditions
        /// </remarks>
        event EventHandler<TelemetryStatusEventArgs> StatusChanged;

        /// <summary>
        /// Tracks an error or exception asynchronously.
        /// </summary>
        /// <param name="exception">The exception to track</param>
        /// <param name="properties">Optional properties to include</param>
        /// <param name="isFatal">Whether this is a fatal/crash error</param>
        /// <returns>Task representing the tracking operation</returns>
        /// <remarks>
        /// This method:
        /// 1. Captures exception details
        /// 2. Adds contextual information
        /// 3. Includes stack trace if available
        /// 4. Tags error severity
        /// 
        /// Used for error reporting and crash analytics.
        /// </remarks>
        Task TrackErrorAsync(Exception exception, Dictionary<string, string>? properties = null, bool isFatal = false);
    }

    /// <summary>
    /// Event arguments for telemetry status changes.
    /// </summary>
    public class TelemetryStatusEventArgs : EventArgs
    {
        /// <summary>
        /// Whether telemetry is currently enabled
        /// </summary>
        public bool IsEnabled { get; }

        /// <summary>
        /// Optional error message if telemetry failed
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Number of events in queue
        /// </summary>
        public int QueuedEventCount { get; }

        /// <summary>
        /// Creates a new TelemetryStatusEventArgs instance.
        /// </summary>
        public TelemetryStatusEventArgs(bool isEnabled, int queuedEventCount, string? errorMessage = null)
        {
            IsEnabled = isEnabled;
            QueuedEventCount = queuedEventCount;
            ErrorMessage = errorMessage;
        }
    }
}