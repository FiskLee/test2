using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ArmaReforgerServerMonitor.Frontend.Utilities
{
    /// <summary>
    /// Provides global exception handling for the application
    /// </summary>
    public class GlobalExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the GlobalExceptionHandler class
        /// </summary>
        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Initializes global exception handling for the application
        /// </summary>
        public void Initialize()
        {
            AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
            Application.Current.DispatcherUnhandledException += HandleDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += HandleUnobservedTaskException;

            _logger.LogInformation("Global exception handling initialized");
        }

        internal void HandleUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            LogException(exception, "Unhandled AppDomain Exception", e.IsTerminating);

            if (e.IsTerminating)
            {
                ShowFatalErrorDialog(exception);
            }
        }

        internal void HandleDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogException(e.Exception, "Unhandled Dispatcher Exception");
            e.Handled = true;

            ShowErrorDialog(e.Exception);
        }

        internal void HandleUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LogException(e.Exception, "Unobserved Task Exception");
            e.SetObserved();

            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ShowErrorDialog(e.Exception);
                });
            }
        }

        private void LogException(Exception? exception, string context, bool isTerminating = false)
        {
            if (exception == null)
            {
                _logger.LogError("Unknown error occurred in {Context}", context);
                return;
            }

            if (isTerminating)
            {
                _logger.LogCritical(
                    exception,
                    "Fatal error in {Context}. Application will terminate. Exception details: {ExceptionType} - {Message}",
                    context,
                    exception.GetType().Name,
                    exception.Message);
            }
            else
            {
                _logger.LogError(
                    exception,
                    "Error in {Context}. Exception details: {ExceptionType} - {Message}",
                    context,
                    exception.GetType().Name,
                    exception.Message);
            }
        }

        private void ShowErrorDialog(Exception? exception)
        {
            var message = exception?.Message ?? "An unknown error occurred.";
            MessageBox.Show(
                $"An error occurred:\n\n{message}\n\nPlease check the logs for more details.",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private void ShowFatalErrorDialog(Exception? exception)
        {
            var message = exception?.Message ?? "An unknown fatal error occurred.";
            MessageBox.Show(
                $"A fatal error occurred:\n\n{message}\n\nThe application will now close. Please check the logs for more details.",
                "Fatal Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}