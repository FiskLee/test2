using Serilog;
using System;

namespace ArmaReforgerServerMonitor.Frontend.Models
{
    /// <summary>
    /// Represents a log entry in the application.
    /// </summary>
    public class LogEntry
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext<LogEntry>();

        /// <summary>
        /// Gets or sets the timestamp of the log entry.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the log level.
        /// </summary>
        public string Level { get; set; }

        /// <summary>
        /// Gets or sets the log message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the source of the log entry.
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Gets or sets additional details or exception information.
        /// </summary>
        public string? Details { get; set; }

        /// <summary>
        /// Initializes a new instance of the LogEntry class.
        /// </summary>
        public LogEntry(DateTime timestamp, string level, string message, string source, string? details = null)
        {
            _logger.Verbose("Creating new LogEntry - Timestamp: {Timestamp}, Level: {Level}, Source: {Source}",
                timestamp,
                level,
                source);

            Timestamp = timestamp;
            Level = level;
            Message = message;
            Source = source;
            Details = details;

            LogEntryDetails();
        }

        private void LogEntryDetails()
        {
            _logger.Verbose("Log Entry Details - Message: {Message}, Has Details: {HasDetails}",
                Message,
                Details != null);

            if (Details != null)
            {
                _logger.Verbose("Log Entry Additional Details: {Details}", Details);
            }
        }

        /// <summary>
        /// Returns a string representation of the log entry.
        /// </summary>
        public override string ToString()
        {
            var logString = $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level}] [{Source}] {Message}{(Details != null ? $"\n{Details}" : "")}";
            _logger.Verbose("Converting LogEntry to string: {LogString}", logString);
            return logString;
        }
    }
}