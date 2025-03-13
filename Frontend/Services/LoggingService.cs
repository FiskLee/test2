using ArmaReforgerServerMonitor.Frontend.Configuration;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// Implementation of the logging service that manages application logging
    /// and log file management.
    /// </summary>
    public class LoggingService : ILoggingService, IDisposable
    {
        private readonly ILogger _logger;
        private readonly AppSettings _settings;
        private readonly string _logDirectory;
        private bool _isDisposed;

        public event EventHandler<LogEventArgs>? LogEntryAdded;

        /// <summary>
        /// Initializes a new instance of the LoggingService class.
        /// </summary>
        /// <param name="settings">Application settings</param>
        public LoggingService(IOptions<AppSettings> settings)
        {
            ArgumentNullException.ThrowIfNull(settings, nameof(settings));
            _settings = settings.Value;
            _logDirectory = Path.GetFullPath(_settings.LoggingSettings.LogsDirectory);
            Directory.CreateDirectory(_logDirectory);

            _logger = ConfigureLogger();
        }

        private ILogger ConfigureLogger()
        {
            var logConfig = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .Enrich.WithEnvironmentName()
                .Enrich.WithProcessId()
                .Enrich.WithMachineName()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}",
                    restrictedToMinimumLevel: LogEventLevel.Debug)
                .WriteTo.Debug(
                    outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}",
                    restrictedToMinimumLevel: LogEventLevel.Debug)
                .WriteTo.File(
                    path: Path.Combine(_logDirectory, $"log-{DateTime.Now:yyyyMMdd}.txt"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: _settings.LoggingSettings.RetentionDays,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}",
                    shared: true);

            return logConfig.CreateLogger();
        }

        public async Task LogDebugAsync(string message, object? data = null)
        {
            await LogAsync(LogEventLevel.Debug, message, null, data);
        }

        public async Task LogInformationAsync(string message, object? data = null)
        {
            await LogAsync(LogEventLevel.Information, message, null, data);
        }

        public async Task LogWarningAsync(string message, object? data = null)
        {
            await LogAsync(LogEventLevel.Warning, message, null, data);
        }

        public async Task LogErrorAsync(string message, Exception? exception = null, object? data = null)
        {
            await LogAsync(LogEventLevel.Error, message, exception, data);
        }

        private async Task LogAsync(LogEventLevel level, string message, Exception? exception = null, object? data = null)
        {
            if (string.IsNullOrEmpty(message))
            {
                throw new ArgumentNullException(nameof(message));
            }

            try
            {
                // Create log entry
                var timestamp = DateTime.UtcNow;
                var logEntry = new LogEventArgs(message, level.ToString(), timestamp);

                // Log using Serilog
                switch (level)
                {
                    case LogEventLevel.Debug:
                        _logger.Debug(exception, message);
                        break;
                    case LogEventLevel.Information:
                        _logger.Information(exception, message);
                        break;
                    case LogEventLevel.Warning:
                        _logger.Warning(exception, message);
                        break;
                    case LogEventLevel.Error:
                        _logger.Error(exception, message);
                        break;
                    default:
                        _logger.Information(exception, message);
                        break;
                }

                // Raise event
                LogEntryAdded?.Invoke(this, logEntry);

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                // If logging fails, try to write to console as last resort
                try
                {
                    Console.Error.WriteLine($"Failed to log message: {ex.Message}");
                    Console.Error.WriteLine($"Original message: {message}");
                    if (exception != null)
                    {
                        Console.Error.WriteLine($"Original exception: {exception.Message}");
                    }
                }
                catch
                {
                    // If even console logging fails, we can't do anything
                }
            }
        }

        public async Task<List<string>> GetRecentLogsAsync(int count, string minLevel = "Debug")
        {
            try
            {
                var logFile = GetCurrentLogPath();
                if (!File.Exists(logFile))
                {
                    return new List<string>();
                }

                var logs = new List<string>();
                var lines = await File.ReadAllLinesAsync(logFile);
                var minLogLevel = Enum.Parse<LogEventLevel>(minLevel);

                foreach (var line in lines.Reverse().Take(count))
                {
                    // Simple parsing - in production you'd want more robust parsing
                    if (line.Contains($"[{minLogLevel}]") || line.Contains($"[{LogEventLevel.Error}]"))
                    {
                        logs.Add(line);
                    }
                }

                return logs;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve recent logs");
                return new List<string>();
            }
        }

        public string GetCurrentLogPath()
        {
            return Path.Combine(_logDirectory, $"log-{DateTime.Now:yyyyMMdd}.txt");
        }

        public async Task<int> CleanupOldLogsAsync()
        {
            try
            {
                var retentionDays = _settings.LoggingSettings.RetentionDays;
                var cutoffDate = DateTime.Now.AddDays(-retentionDays);
                var deletedCount = 0;

                foreach (var file in Directory.GetFiles(_logDirectory, "log-*.txt"))
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        await Task.Run(() => File.Delete(file));
                        deletedCount++;
                    }
                }

                return deletedCount;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to clean up old logs");
                return 0;
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            try
            {
                (_logger as IDisposable)?.Dispose();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error disposing LoggingService: {ex.Message}");
            }
            finally
            {
                _isDisposed = true;
            }
        }
    }
}