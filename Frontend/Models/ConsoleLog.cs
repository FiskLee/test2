using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArmaReforgerServerMonitor.Frontend.Models
{
    /// <summary>
    /// Represents a console log entry from the ArmA Reforger server.
    /// Contains information about server events, errors, and player actions.
    /// </summary>
    /// <remarks>
    /// This class is used to parse and store server console log entries.
    /// It provides structured access to log information and supports
    /// filtering and analysis of server events.
    /// 
    /// Log entries are collected continuously and can be used for:
    /// - Monitoring server health
    /// - Tracking player activity
    /// - Debugging issues
    /// - Generating statistics
    /// </remarks>
    public class ConsoleLog
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext<ConsoleLog>();

        /// <summary>
        /// Unique identifier for the log entry.
        /// </summary>
        /// <remarks>
        /// Generated when the log entry is created.
        /// Used for tracking and referencing specific entries.
        /// </remarks>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// When the log entry was created.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// The severity level of the log entry.
        /// </summary>
        /// <remarks>
        /// Common values:
        /// - Info: Normal operational messages
        /// - Warning: Potential issues
        /// - Error: Serious problems
        /// - Debug: Detailed diagnostic information
        /// </remarks>
        public LogLevel Level { get; set; }

        /// <summary>
        /// The source component that generated the log.
        /// </summary>
        /// <remarks>
        /// Examples:
        /// - GameServer
        /// - Network
        /// - Mission
        /// - PlayerManager
        /// </remarks>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// The actual log message content.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Additional structured data associated with the log.
        /// </summary>
        /// <remarks>
        /// Can contain:
        /// - Player information
        /// - Error details
        /// - Performance metrics
        /// - Event-specific data
        /// </remarks>
        public Dictionary<string, string> Properties { get; set; } = new();

        /// <summary>
        /// Stack trace if this is an error log.
        /// </summary>
        public string? StackTrace { get; set; }

        /// <summary>
        /// Related player name if applicable.
        /// </summary>
        /// <remarks>
        /// Set when the log entry is related to a specific player action
        /// or event. Helps with player activity tracking.
        /// </remarks>
        public string? PlayerName { get; set; }

        /// <summary>
        /// Creates a new ConsoleLog instance.
        /// </summary>
        public ConsoleLog()
        {
            _logger.Verbose("Creating new ConsoleLog with default values - ID: {Id}", Id);
            Timestamp = DateTime.UtcNow;
            Level = LogLevel.Info;
            LogConsoleLog();
        }

        /// <summary>
        /// Creates a new ConsoleLog instance with specified message and level.
        /// </summary>
        /// <param name="message">The log message</param>
        /// <param name="level">The severity level</param>
        public ConsoleLog(string message, LogLevel level = LogLevel.Info)
            : this()
        {
            _logger.Verbose("Creating new ConsoleLog with message - ID: {Id}, Message: {Message}, Level: {Level}",
                Id,
                message,
                level);

            Message = message;
            Level = level;
            LogConsoleLog();
        }

        /// <summary>
        /// Adds a property to the log entry.
        /// </summary>
        /// <param name="key">Property key</param>
        /// <param name="value">Property value</param>
        /// <remarks>
        /// Use this method to add structured data to the log entry.
        /// Properties can be used for filtering and analysis.
        /// </remarks>
        public void AddProperty(string key, string value)
        {
            _logger.Verbose("Adding property to ConsoleLog {Id} - Key: {Key}, Value: {Value}",
                Id,
                key,
                value);

            Properties[key] = value;
            LogConsoleLog();
        }

        /// <summary>
        /// Gets a formatted string representation of the log entry.
        /// </summary>
        /// <returns>Formatted log entry string</returns>
        /// <remarks>
        /// Format: [Timestamp] [Level] [Source] Message
        /// Properties are appended as key=value pairs
        /// </remarks>
        public override string ToString()
        {
            var result = $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level}] ";
            if (!string.IsNullOrEmpty(Source))
                result += $"[{Source}] ";
            result += Message;

            if (Properties.Count > 0)
            {
                result += " {";
                result += string.Join(", ", Properties.Select(p => $"{p.Key}={p.Value}"));
                result += "}";
            }

            _logger.Verbose("Converting ConsoleLog {Id} to string: {Result}", Id, result);
            return result;
        }

        private void LogConsoleLog()
        {
            _logger.Verbose("ConsoleLog Details - ID: {Id}, Timestamp: {Timestamp}, Level: {Level}, Source: {Source}",
                Id,
                Timestamp,
                Level,
                Source);

            _logger.Verbose("Message: {Message}, Player: {Player}, Properties: {PropertyCount}, Has StackTrace: {HasStackTrace}",
                Message,
                PlayerName ?? "none",
                Properties.Count,
                !string.IsNullOrEmpty(StackTrace));
        }
    }

    /// <summary>
    /// Represents the severity level of a log entry.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// Detailed diagnostic information
        /// </summary>
        Debug,

        /// <summary>
        /// Normal operational messages
        /// </summary>
        Info,

        /// <summary>
        /// Potential issues that should be reviewed
        /// </summary>
        Warning,

        /// <summary>
        /// Serious problems that need attention
        /// </summary>
        Error,

        /// <summary>
        /// Critical issues that may affect server stability
        /// </summary>
        Critical
    }

    /// <summary>
    /// Collection of console log statistics.
    /// </summary>
    public class ConsoleLogStats
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext<ConsoleLogStats>();

        /// <summary>
        /// Total number of log entries.
        /// </summary>
        public int TotalEntries { get; set; }

        /// <summary>
        /// Number of entries by log level.
        /// </summary>
        public Dictionary<LogLevel, int> EntriesByLevel { get; set; } = new();

        /// <summary>
        /// Number of entries by source.
        /// </summary>
        public Dictionary<string, int> EntriesBySource { get; set; } = new();

        /// <summary>
        /// Recent error messages.
        /// </summary>
        /// <remarks>
        /// Contains the last few error messages for quick review.
        /// Limited to avoid memory issues with large logs.
        /// </remarks>
        public List<ConsoleLog> RecentErrors { get; set; } = new();

        /// <summary>
        /// When the statistics were last updated.
        /// </summary>
        public DateTime LastUpdate { get; set; }

        /// <summary>
        /// Creates a new ConsoleLogStats instance.
        /// </summary>
        public ConsoleLogStats()
        {
            _logger.Verbose("Initializing ConsoleLogStats");
            LastUpdate = DateTime.UtcNow;
            foreach (LogLevel level in Enum.GetValues(typeof(LogLevel)))
            {
                EntriesByLevel[level] = 0;
            }
            LogStats();
        }

        /// <summary>
        /// Updates statistics with a new log entry.
        /// </summary>
        /// <param name="log">The log entry to process</param>
        /// <remarks>
        /// This method:
        /// 1. Increments total count
        /// 2. Updates level statistics
        /// 3. Updates source statistics
        /// 4. Adds to recent errors if applicable
        /// </remarks>
        public void ProcessLogEntry(ConsoleLog log)
        {
            _logger.Verbose("Processing log entry - ID: {Id}, Level: {Level}, Source: {Source}",
                log.Id,
                log.Level,
                log.Source);

            TotalEntries++;
            EntriesByLevel[log.Level]++;

            if (!string.IsNullOrEmpty(log.Source))
            {
                if (!EntriesBySource.ContainsKey(log.Source))
                    EntriesBySource[log.Source] = 0;
                EntriesBySource[log.Source]++;
            }

            if (log.Level >= LogLevel.Error)
            {
                RecentErrors.Add(log);
                if (RecentErrors.Count > 100)
                {
                    RecentErrors.RemoveAt(0);
                }
            }

            LastUpdate = DateTime.UtcNow;
            LogStats();
        }

        private void LogStats()
        {
            _logger.Verbose("ConsoleLogStats - Total Entries: {Total}, Last Update: {LastUpdate}",
                TotalEntries,
                LastUpdate);

            _logger.Verbose("Entries by Level:");
            foreach (var level in EntriesByLevel)
            {
                _logger.Verbose("  {Level}: {Count}", level.Key, level.Value);
            }

            _logger.Verbose("Entries by Source:");
            foreach (var source in EntriesBySource)
            {
                _logger.Verbose("  {Source}: {Count}", source.Key, source.Value);
            }

            _logger.Verbose("Recent Errors: {Count}", RecentErrors.Count);
        }
    }
}