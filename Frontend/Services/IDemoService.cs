using ArmaReforgerServerMonitor.Frontend.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// Interface for managing demo mode and sample data generation.
    /// Provides methods for controlling and customizing demo data display.
    /// </summary>
    /// <remarks>
    /// This service enables users to:
    /// - Toggle demo mode
    /// - Customize demo data parameters
    /// - Control data generation
    /// - Simulate various scenarios
    /// - Test UI responsiveness
    /// 
    /// The demo mode helps users understand the application's features
    /// and test different visualization options without a real server.
    /// </remarks>
    public interface IDemoService
    {
        /// <summary>
        /// Gets whether demo mode is currently active.
        /// </summary>
        /// <returns>True if demo mode is enabled</returns>
        bool IsDemoMode { get; }

        /// <summary>
        /// Enables or disables demo mode.
        /// </summary>
        /// <param name="enabled">Whether to enable demo mode</param>
        /// <remarks>
        /// When enabled, the application will use generated sample data
        /// instead of real server data for all displays and charts.
        /// </remarks>
        void SetDemoMode(bool enabled);

        /// <summary>
        /// Gets demo OS metrics data.
        /// </summary>
        /// <param name="parameters">Optional parameters to customize the generated data</param>
        /// <returns>Generated OS metrics data</returns>
        /// <remarks>
        /// Generates sample system metrics including:
        /// - CPU usage
        /// - Memory usage
        /// - Disk statistics
        /// - Network statistics
        /// </remarks>
        Task<OSDataDTO> GetDemoMetricsAsync(DemoParameters? parameters = null);

        /// <summary>
        /// Gets demo server status data.
        /// </summary>
        /// <param name="parameters">Optional parameters to customize the generated data</param>
        /// <returns>Generated server status data</returns>
        /// <remarks>
        /// Generates sample server data including:
        /// - Player information
        /// - Performance metrics
        /// - Game settings
        /// - Server configuration
        /// </remarks>
        Task<ServerStatus> GetDemoServerStatusAsync(DemoParameters? parameters = null);

        /// <summary>
        /// Gets demo console log entries.
        /// </summary>
        /// <param name="count">Number of log entries to generate</param>
        /// <param name="parameters">Optional parameters to customize the generated data</param>
        /// <returns>List of generated console log entries</returns>
        /// <remarks>
        /// Generates sample log entries with:
        /// - Various severity levels
        /// - Different message types
        /// - Player events
        /// - System events
        /// </remarks>
        Task<List<ConsoleLog>> GetDemoConsoleLogsAsync(int count, DemoParameters? parameters = null);

        /// <summary>
        /// Updates demo data generation parameters.
        /// </summary>
        /// <param name="parameters">New parameters for data generation</param>
        /// <remarks>
        /// Allows customization of:
        /// - Data ranges
        /// - Update frequency
        /// - Error rates
        /// - Scenario types
        /// </remarks>
        void UpdateParameters(DemoParameters parameters);

        /// <summary>
        /// Event that fires when demo parameters are changed.
        /// </summary>
        event EventHandler<DemoParametersChangedEventArgs> ParametersChanged;

        event EventHandler<OSDataDTO> MetricsUpdated;
        void Start();
        void StopService();
        Task RestartAsync(CancellationToken cancellationToken);
    }
}