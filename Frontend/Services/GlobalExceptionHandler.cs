using ArmaReforgerServerMonitor.Frontend.Configuration;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// Global exception handler that captures and processes all unhandled exceptions in the application
    /// </summary>
    internal class GlobalExceptionHandler
    {
        private readonly ILogger _logger;
        private readonly IPastebinService? _pastebinService;
        private readonly PastebinSettings _pastebinSettings;
        private readonly string _logsDirectory;
        private readonly Dispatcher _dispatcher;

        public GlobalExceptionHandler(
            IPastebinService? pastebinService,
            IOptions<AppSettings> settings)
        {
            _logger = Log.ForContext<GlobalExceptionHandler>();
            _pastebinService = pastebinService;
            _pastebinSettings = settings.Value.PastebinSettings ?? new PastebinSettings();
            _logsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, settings.Value.LoggingSettings.LogsDirectory);
            _dispatcher = Application.Current.Dispatcher;

            _logger.Verbose("GlobalExceptionHandler initialized with settings - Logs directory: {Directory}, Pastebin enabled: {PastebinEnabled}",
                _logsDirectory, _pastebinService != null);

            AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
            Application.Current.DispatcherUnhandledException += HandleDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += HandleUnobservedTaskException;

            _logger.Debug("Exception handlers registered successfully");
        }

        public void HandleDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            _logger.Verbose("Dispatcher unhandled exception detected");
            LogException(e.Exception, "Dispatcher Unhandled Exception");
            ShowErrorMessage(e.Exception);
            e.Handled = true;
            _logger.Debug("Dispatcher exception handled successfully");
        }

        public void HandleUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            _logger.Verbose("Unhandled exception detected");
            if (e == null) throw new ArgumentNullException(nameof(e));
            if (e.ExceptionObject is Exception exception)
            {
                LogException(exception, "Unhandled Exception");
                ShowErrorMessage(exception);
                _logger.Debug("Unhandled exception processed successfully");
            }
            else
            {
                _logger.Warning("Unhandled exception object is not of type Exception: {Type}",
                    e.ExceptionObject?.GetType().FullName ?? "null");
            }
        }

        public void HandleUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            _logger.Verbose("Unobserved task exception detected");
            LogException(e.Exception, "Unobserved Task Exception");
            ShowErrorMessage(e.Exception);
            e.SetObserved();
            _logger.Debug("Unobserved task exception marked as observed");
        }

        private void LogException(Exception ex, string context)
        {
            try
            {
                var logContext = _logger
                    .ForContext("ExceptionType", ex.GetType().FullName)
                    .ForContext("ExceptionMessage", ex.Message)
                    .ForContext("StackTrace", ex.StackTrace)
                    .ForContext("Source", ex.Source)
                    .ForContext("TargetSite", ex.TargetSite?.Name)
                    .ForContext("Context", context);

                if (ex.InnerException != null)
                {
                    logContext = logContext
                        .ForContext("InnerExceptionType", ex.InnerException.GetType().FullName)
                        .ForContext("InnerExceptionMessage", ex.InnerException.Message)
                        .ForContext("InnerStackTrace", ex.InnerException.StackTrace);
                }

                logContext.Error(ex, "Unhandled exception occurred in {Context} - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    context, ex.GetType().FullName, ex.Message, ex.StackTrace);

                _logger.Verbose("Exception logged successfully with full context");
            }
            catch (IOException logEx)
            {
                _logger.Error(logEx, "Failed to log exception with context: {Context}", context);
            }
            catch (Exception logEx)
            {
                _logger.Error(logEx, "Unexpected error during logging: {Message}", logEx.Message);
            }
        }

        private void ShowErrorMessage(Exception ex)
        {
            try
            {
                _logger.Verbose("Preparing error message for display");
                var message = new StringBuilder();
                message.AppendLine($"An error occurred: {ex.Message}");

                if (ex.InnerException != null)
                {
                    message.AppendLine($"Inner Exception: {ex.InnerException.Message}");
                }

                _logger.Debug("Error message prepared: {Message}", message.ToString());

                _dispatcher.Invoke(() =>
                {
                    _logger.Verbose("Showing error message dialog");
                    MessageBox.Show(message.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    _logger.Debug("Error message dialog displayed successfully");
                });
            }
            catch (InvalidOperationException showEx)
            {
                _logger.Error(showEx, "Failed to show error message - Original exception: {ExceptionType}, Message: {Message}",
                    ex.GetType().FullName, ex.Message);
            }
            catch (Exception showEx)
            {
                _logger.Error(showEx, "Unexpected error showing error message: {Message}", showEx.Message);
            }
        }

        private async Task<string> GetRecentLogsAsync()
        {
            try
            {
                _logger.Verbose("Retrieving recent logs from directory: {Directory}", _logsDirectory);
                var logFiles = Directory.GetFiles(_logsDirectory, "frontend_log_*.log")
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .Take(2);

                _logger.Debug("Found {Count} recent log files", logFiles.Count());

                var combinedLogs = new StringBuilder();
                foreach (var file in logFiles)
                {
                    _logger.Verbose("Reading log file: {File}", file);
                    combinedLogs.AppendLine($"=== Log File: {Path.GetFileName(file)} ===");
                    combinedLogs.AppendLine(await File.ReadAllTextAsync(file));
                    combinedLogs.AppendLine();
                    _logger.Debug("Successfully read log file: {File}", file);
                }

                _logger.Information("Successfully retrieved {Count} log files", logFiles.Count());
                return combinedLogs.ToString();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to read log files from {Directory} - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    _logsDirectory, ex.GetType().FullName, ex.Message, ex.StackTrace);
                throw;
            }
        }
    }
}