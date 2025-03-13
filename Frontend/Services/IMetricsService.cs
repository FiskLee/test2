using ArmaReforgerServerMonitor.Frontend.Models;
using System;
using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// Interface for retrieving and managing server metrics and performance data.
    /// Provides access to operating system metrics, console logs, and raw server data.
    /// </summary>
    /// <remarks>
    /// Key responsibilities:
    /// - Fetching real-time server performance metrics
    /// - Retrieving and parsing console logs
    /// - Accessing raw server data
    /// - Managing backend logs
    /// 
    /// This service is critical for monitoring server health and performance.
    /// All methods require a valid authentication token (handled by AuthService).
    /// 
    /// Usage example:
    /// ```csharp
    /// var metrics = await _metricsService.GetOSMetricsAsync("http://server");
    /// Console.WriteLine($"CPU Usage: {metrics.CpuUsage}%");
    /// ```
    /// </remarks>
    public interface IMetricsService
    {
        /// <summary>
        /// Retrieves operating system metrics from the server.
        /// </summary>
        /// <param name="baseUrl">The base URL of the backend server</param>
        /// <returns>
        /// An OSDataDTO object containing:
        /// - CPU usage statistics
        /// - Memory usage information
        /// - Disk space metrics
        /// - Network statistics
        /// Returns null if the request fails.
        /// </returns>
        /// <remarks>
        /// This method:
        /// 1. Makes an authenticated GET request to the metrics endpoint
        /// 2. Deserializes the response into an OSDataDTO object
        /// 3. Handles any errors or connection issues
        /// 
        /// The data is typically refreshed every PollIntervalSeconds (configured in AppSettings).
        /// </remarks>
        Task<OSDataDTO?> GetOSMetricsAsync(Uri baseUrl);

        /// <summary>
        /// Retrieves console log statistics and recent entries.
        /// </summary>
        /// <param name="baseUrl">The base URL of the backend server</param>
        /// <returns>
        /// A string containing formatted console log data, including:
        /// - Log entry counts by severity
        /// - Recent log entries
        /// - Error summaries
        /// </returns>
        /// <remarks>
        /// This method provides a summary of console activity and recent events.
        /// The logs are formatted for display in the UI and include timestamps.
        /// Useful for monitoring server events and troubleshooting issues.
        /// </remarks>
        Task<string> GetConsoleLogStatsAsync(Uri baseUrl);

        /// <summary>
        /// Retrieves raw server data for detailed analysis.
        /// </summary>
        /// <param name="baseUrl">The base URL of the backend server</param>
        /// <returns>
        /// A string containing raw server data in JSON format.
        /// Includes detailed metrics and state information not processed by other methods.
        /// </returns>
        /// <remarks>
        /// This method provides access to unprocessed server data.
        /// Useful for debugging or when specific raw data is needed.
        /// The data format depends on the server configuration.
        /// </remarks>
        Task<string> GetRawDataAsync(Uri baseUrl);

        /// <summary>
        /// Retrieves backend server logs for troubleshooting.
        /// </summary>
        /// <param name="baseUrl">The base URL of the backend server</param>
        /// <returns>
        /// A string containing formatted backend logs.
        /// Includes system events, errors, and diagnostic information.
        /// </returns>
        /// <remarks>
        /// This method provides access to server-side logs.
        /// Useful for:
        /// - Debugging server issues
        /// - Monitoring system health
        /// - Tracking error patterns
        /// 
        /// The logs are typically rotated based on the server's configuration.
        /// </remarks>
        Task<string> GetBackendLogsAsync(Uri baseUrl);
    }
}