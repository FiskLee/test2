using ArmaReforgerServerMonitor.Frontend.Configuration;
using MahApps.Metro.Controls;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace ArmaReforgerServerMonitor.Frontend
{
    public partial class FrontendLogsWindow : MetroWindow
    {
        private readonly DispatcherTimer _refreshTimer;
        private readonly string _logsFolder;
        private string _currentLogFilePath = string.Empty;
        private long _lastPosition = 0;

        public FrontendLogsWindow()
        {
            InitializeComponent();

            // Get settings from DI
            var app = (App)Application.Current;
            var settings = app.Services.GetRequiredService<AppSettings>();

            // Determine the absolute logs folder path
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _logsFolder = Path.Combine(baseDir, settings.LoggingSettings.LogsDirectory);

            // Ensure the logs folder exists
            Directory.CreateDirectory(_logsFolder);

            // Set _currentLogFilePath to today's log file
            _currentLogFilePath = Path.Combine(_logsFolder, $"log-{DateTime.Now:yyyy-MM-dd}.txt");

            Log.Information("FrontendLogsWindow initialized. Using log file: {LogFilePath}", _currentLogFilePath);

            // Initialize the timer with an interval of 1 second
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _refreshTimer.Tick += RefreshTimer_Tick;

            // Add initial terminal header
            AppendTerminalHeader();

            _refreshTimer.Start();
        }

        private void AppendTerminalHeader()
        {
            var header = $"=== Frontend Logs Terminal ===\n" +
                        $"Started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                        $"Log file: {_currentLogFilePath}\n" +
                        $"Refresh rate: 1 second\n" +
                        "===========================\n\n";

            LogsTextBox.Text = header;
            _lastPosition = 0; // Reset position to start reading from beginning
        }

        private void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_currentLogFilePath) || !File.Exists(_currentLogFilePath))
                {
                    if (LogsTextBox.Text.EndsWith("Waiting for log file...\n"))
                        return;

                    LogsTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] Waiting for log file...\n");
                    Log.Debug("Log file not found in {LogsFolder}", _logsFolder);
                    return;
                }

                // Open the file with shared read access
                using (var stream = new FileStream(_currentLogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    // If the file size is smaller than our last position (file was truncated/recreated)
                    if (stream.Length < _lastPosition)
                    {
                        _lastPosition = 0;
                        AppendTerminalHeader();
                    }

                    if (stream.Length > _lastPosition)
                    {
                        stream.Seek(_lastPosition, SeekOrigin.Begin);
                        using (var reader = new StreamReader(stream))
                        {
                            string newContent = reader.ReadToEnd();
                            if (!string.IsNullOrEmpty(newContent))
                            {
                                LogsTextBox.AppendText(newContent);
                                LogsTextBox.ScrollToEnd();
                            }
                        }
                        _lastPosition = stream.Length;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error reading log file in FrontendLogsWindow");
                LogsTextBox.AppendText($"\n[{DateTime.Now:HH:mm:ss}] Error reading log file: {ex.Message}\n");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _refreshTimer.Stop();
            base.OnClosed(e);
        }
    }
}
