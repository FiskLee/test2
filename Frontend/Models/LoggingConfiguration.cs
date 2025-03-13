using Serilog;
using Serilog.Events;
using System;
using System.Globalization;
using System.IO;

namespace ArmaReforgerServerMonitor.Frontend.Models
{
    public static class LoggingConfiguration
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext(typeof(LoggingConfiguration));

        private static string _logFilePath = string.Empty;
        public static string LogFilePath
        {
            get => _logFilePath;
            private set
            {
                _logger.Verbose("Updating LogFilePath from '{OldPath}' to '{NewPath}'", _logFilePath, value);
                _logFilePath = value;
            }
        }

        public static void ConfigureLogging()
        {
            _logger.Verbose("Starting logging configuration");

            var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            _logger.Verbose("Log directory path: {LogDirectory}", logDirectory);

            try
            {
                Directory.CreateDirectory(logDirectory);
                _logger.Verbose("Log directory created or already exists");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to create log directory: {LogDirectory}", logDirectory);
                throw;
            }

            LogFilePath = Path.Combine(logDirectory, $"frontend_log_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            _logger.Verbose("Log file path set to: {LogFilePath}", LogFilePath);

            try
            {
                _logger.Verbose("Configuring Serilog with the following settings:");
                _logger.Verbose("- Minimum Level: Debug");
                _logger.Verbose("- Microsoft Override Level: Information");
                _logger.Verbose("- Enrichment: LogContext, ThreadId, EnvironmentUserName");
                _logger.Verbose("- Console Output: Enabled with custom template");
                _logger.Verbose("- File Output: Enabled with daily rolling and 7-day retention");

                var loggerConfig = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                    .Enrich.FromLogContext()
                    .Enrich.WithThreadId()
                    .Enrich.WithEnvironmentUserName()
                    .WriteTo.Console(
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}",
                        formatProvider: CultureInfo.InvariantCulture)
                    .WriteTo.File(LogFilePath,
                        rollingInterval: RollingInterval.Day,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{ThreadId}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                        retainedFileCountLimit: 7)
                    .CreateLogger();

                _logger.Verbose("Serilog configuration completed successfully");
                Log.Information("Logging initialized. Log file: {LogFilePath}", LogFilePath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to configure Serilog");
                throw;
            }
        }

        public static void CloseAndFlush()
        {
            _logger.Verbose("Starting application shutdown and log flush");
            try
            {
                Log.Information("Application shutting down. Flushing logs...");
                Log.CloseAndFlush();
                _logger.Verbose("Log flush completed successfully");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to flush logs during shutdown");
                throw;
            }
        }
    }
}