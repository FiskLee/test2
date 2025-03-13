using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// Interface for the application's logging service.
    /// Provides methods for logging at different levels and managing log files.
    /// </summary>
    public interface ILoggingService
    {
        /// <summary>
        /// Event raised when a new log entry is added
        /// </summary>
        event EventHandler<LogEventArgs> LogEntryAdded;

        /// <summary>
        /// Logs a debug message asynchronously
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="data">Optional structured data to include in the log</param>
        Task LogDebugAsync(string message, object? data = null);

        /// <summary>
        /// Logs an information message asynchronously
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="data">Optional structured data to include in the log</param>
        Task LogInformationAsync(string message, object? data = null);

        /// <summary>
        /// Logs a warning message asynchronously
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="data">Optional structured data to include in the log</param>
        Task LogWarningAsync(string message, object? data = null);

        /// <summary>
        /// Logs an error message asynchronously
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="exception">Optional exception to include in the log</param>
        /// <param name="data">Optional structured data to include in the log</param>
        Task LogErrorAsync(string message, Exception? exception = null, object? data = null);

        /// <summary>
        /// Gets recent log entries
        /// </summary>
        /// <param name="count">Number of log entries to retrieve</param>
        /// <param name="minLevel">Minimum log level to include</param>
        /// <returns>List of log entries</returns>
        Task<List<string>> GetRecentLogsAsync(int count, string minLevel = "Debug");

        /// <summary>
        /// Gets the path to the current log file
        /// </summary>
        /// <returns>Full path to the current log file</returns>
        string GetCurrentLogPath();

        /// <summary>
        /// Cleans up old log files based on retention policy
        /// </summary>
        /// <returns>Number of files deleted</returns>
        Task<int> CleanupOldLogsAsync();
    }

    /// <summary>
    /// Event arguments for log entries
    /// </summary>
    public class LogEventArgs : EventArgs
    {
        /// <summary>
        /// The log message
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// The log level
        /// </summary>
        public string Level { get; }

        /// <summary>
        /// The timestamp of the log entry
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of LogEventArgs
        /// </summary>
        public LogEventArgs(string message, string level, DateTime timestamp)
        {
            Message = message;
            Level = level;
            Timestamp = timestamp;
        }
    }
}