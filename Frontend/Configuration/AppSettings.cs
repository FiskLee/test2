using Serilog;
using Serilog.Events;
using System.Collections.Generic;
using System;

namespace ArmaReforgerServerMonitor.Frontend.Configuration
{
    /// <summary>
    /// Root configuration class that holds all application settings.
    /// This class maps directly to the "AppSettings" section in appsettings.json.
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// Application version
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// Default URL for connecting to the backend server
        /// </summary>
        public string DefaultServerUrl { get; set; } = "http://localhost:5000";

        /// <summary>
        /// Interval in seconds between backend polling requests
        /// </summary>
        public int PollIntervalSeconds { get; set; } = 2;

        /// <summary>
        /// Maximum number of retry attempts for failed network requests
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Timeout in seconds for network requests
        /// </summary>
        public int ConnectionTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Timeout in seconds for HTTP requests
        /// </summary>
        public int RequestTimeout { get; set; } = 30;

        /// <summary>
        /// Logging configuration settings
        /// </summary>
        public LoggingSettings LoggingSettings { get; set; } = new();

        /// <summary>
        /// UI theme configuration settings
        /// </summary>
        public ThemeSettings Theme { get; set; } = new();

        /// <summary>
        /// Application telemetry configuration settings
        /// </summary>
        public TelemetrySettings Telemetry { get; set; } = new();

        /// <summary>
        /// Automatic update configuration settings
        /// </summary>
        public UpdateSettings Updates { get; set; } = new();

        /// <summary>
        /// Offline mode configuration settings
        /// </summary>
        public OfflineSettings Offline { get; set; } = new();

        /// <summary>
        /// Pastebin integration configuration settings
        /// </summary>
        public PastebinSettings PastebinSettings { get; set; } = new();

        /// <summary>
        /// Settings for demo mode.
        /// </summary>
        public DemoSettings DemoSettings { get; set; } = new();

        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string RconPort { get; set; } = string.Empty;
        public bool AutoReconnect { get; set; } = true;
        public int ReconnectDelayMs { get; set; } = 5000;
        public int MaxReconnectAttempts { get; set; } = 3;
        public UpdateSettings UpdateSettings { get; set; } = new();
        public WindowSettings2 WindowSettings2 { get; set; } = new();
        public string? GithubRepositoryUrl { get; set; }
        public string? VersionCheckUrl { get; set; }
        public string? GitHubOwner { get; set; }
        public string? GitHubRepo { get; set; }
        public string? CurrentVersion { get; set; }

        // Update settings from another instance
        public void UpdateFrom(AppSettings other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            // Update properties
            Version = other.Version;
            DefaultServerUrl = other.DefaultServerUrl;
            PollIntervalSeconds = other.PollIntervalSeconds;
            MaxRetryAttempts = other.MaxRetryAttempts;
            ConnectionTimeoutSeconds = other.ConnectionTimeoutSeconds;
            // Add other properties as needed
        }

        // Get a setting value
        public T GetValue<T>(string key, T defaultValue)
        {
            // Implement logic to retrieve the value based on the key
            // For example, using reflection or a dictionary
            return defaultValue; // Placeholder
        }

        // Set a setting value
        public void SetValue<T>(string key, T value)
        {
            // Implement logic to set the value based on the key
        }

        // Reset settings to default values
        public void ResetToDefaults()
        {
            // Implement logic to reset properties to default values
            Version = "1.0.0";
            DefaultServerUrl = "http://localhost:5000";
            PollIntervalSeconds = 2;
            MaxRetryAttempts = 3;
            ConnectionTimeoutSeconds = 30;
            // Reset other properties as needed
        }
    }

    /// <summary>
    /// Configuration settings for application logging
    /// </summary>
    public class LoggingSettings
    {
        /// <summary>
        /// Directory where log files will be stored
        /// </summary>
        public string LogsDirectory { get; set; } = "Logs";

        /// <summary>
        /// Number of days to keep log files before deletion
        /// </summary>
        public int RetentionDays { get; set; } = 7;

        /// <summary>
        /// Minimum log level to capture (Verbose, Debug, Information, Warning, Error, Fatal)
        /// </summary>
        public string? MinimumLevel { get; set; } = "Debug";

        /// <summary>
        /// Maximum number of log files to keep
        /// </summary>
        public int MaxLogFiles { get; set; } = 10;

        /// <summary>
        /// Maximum size of each log file in megabytes
        /// </summary>
        public int MaxFileSizeMB { get; set; } = 10;

        /// <summary>
        /// Whether to enable console logging
        /// </summary>
        public bool EnableConsoleLogging { get; set; } = true;

        /// <summary>
        /// Whether to enable debug output logging
        /// </summary>
        public bool EnableDebugLogging { get; set; } = true;

        /// <summary>
        /// Whether to enable file logging
        /// </summary>
        public bool EnableFileLogging { get; set; } = true;

        /// <summary>
        /// Log file name template
        /// </summary>
        public string FileNameTemplate { get; set; } = "log-{Date}.txt";

        /// <summary>
        /// Output template for log entries
        /// </summary>
        public string OutputTemplate { get; set; } = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}";

        /// <summary>
        /// Whether to flush to disk immediately (useful for debugging)
        /// </summary>
        public bool ImmediateFlush { get; set; } = false;

        /// <summary>
        /// Whether to share log files between processes
        /// </summary>
        public bool SharedFiles { get; set; } = true;

        /// <summary>
        /// Override minimum levels for specific namespaces
        /// </summary>
        public Dictionary<string, string> LevelOverrides { get; set; } = new()
        {
            { "Microsoft", "Warning" },
            { "System", "Warning" }
        };

        /// <summary>
        /// Gets the LogEventLevel from the MinimumLevel string
        /// </summary>
        public LogEventLevel GetMinimumLogEventLevel()
        {
            return MinimumLevel?.ToLower() switch
            {
                "verbose" => LogEventLevel.Verbose,
                "debug" => LogEventLevel.Debug,
                "information" => LogEventLevel.Information,
                "warning" => LogEventLevel.Warning,
                "error" => LogEventLevel.Error,
                "fatal" => LogEventLevel.Fatal,
                _ => LogEventLevel.Information
            };
        }
    }

    /// <summary>
    /// Configuration settings for UI theme customization
    /// </summary>
    public class ThemeSettings
    {
        /// <summary>
        /// Default application theme (Light/Dark)
        /// </summary>
        public string DefaultTheme { get; set; } = "Dark";

        /// <summary>
        /// Default accent color
        /// </summary>
        public string DefaultAccent { get; set; } = "Blue";

        /// <summary>
        /// Whether to use system theme
        /// </summary>
        public bool UseSystemTheme { get; set; } = true;

        /// <summary>
        /// Whether to enable animations
        /// </summary>
        public bool EnableAnimations { get; set; } = true;

        /// <summary>
        /// Font family for the theme
        /// </summary>
        public string FontFamily { get; set; } = "Segoe UI";

        /// <summary>
        /// Font size for the theme
        /// </summary>
        public double FontSize { get; set; } = 12;
    }

    /// <summary>
    /// Configuration settings for application telemetry and monitoring
    /// </summary>
    public class TelemetrySettings
    {
        /// <summary>
        /// Whether telemetry is enabled
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Whether to collect usage data
        /// </summary>
        public bool CollectUsageData { get; set; } = false;

        /// <summary>
        /// Whether to collect error reports
        /// </summary>
        public bool CollectErrorReports { get; set; } = false;

        /// <summary>
        /// Application Insights instrumentation key
        /// </summary>
        public string? InstrumentationKey { get; set; }

        /// <summary>
        /// Connection string for telemetry
        /// </summary>
        public string? ConnectionString { get; set; }
    }

    /// <summary>
    /// Configuration settings for automatic updates
    /// </summary>
    public class UpdateSettings
    {
        /// <summary>
        /// Whether to automatically check for updates
        /// </summary>
        public bool AutoCheckForUpdates { get; set; } = true;

        /// <summary>
        /// Whether to automatically download updates
        /// </summary>
        public bool AutoDownloadUpdates { get; set; } = false;

        /// <summary>
        /// Interval in hours between update checks
        /// </summary>
        public int CheckIntervalHours { get; set; } = 24;

        /// <summary>
        /// Whether to include pre-release versions
        /// </summary>
        public bool IncludePreReleases { get; set; } = false;

        /// <summary>
        /// GitHub repository URL for updates
        /// </summary>
        public string? RepositoryUrl { get; set; }

        /// <summary>
        /// GitHub repository URL for updates
        /// </summary>
        public string? GithubRepositoryUrl { get; set; }

        /// <summary>
        /// URL for checking the latest version
        /// </summary>
        public string? VersionCheckUrl { get; set; }

        /// <summary>
        /// GitHub owner for updates
        /// </summary>
        public string? GitHubOwner { get; set; }

        /// <summary>
        /// GitHub repository for updates
        /// </summary>
        public string? GitHubRepo { get; set; }

        /// <summary>
        /// Current version for updates
        /// </summary>
        public string? CurrentVersion { get; set; }
    }

    /// <summary>
    /// Configuration settings for offline mode functionality
    /// </summary>
    public class OfflineSettings
    {
        /// <summary>
        /// Whether offline mode is enabled
        /// </summary>
        public bool EnableOfflineMode { get; set; } = false;

        /// <summary>
        /// Cache expiration time in hours
        /// </summary>
        public int CacheExpirationHours { get; set; } = 24;

        /// <summary>
        /// Maximum cache size in megabytes
        /// </summary>
        public int MaxCacheSizeMB { get; set; } = 100;
    }

    /// <summary>
    /// Configuration settings for Pastebin integration
    /// </summary>
    public class PastebinSettings
    {
        /// <summary>
        /// Whether to automatically share error logs
        /// </summary>
        public bool AutoShareErrorLogs { get; set; } = false;

        /// <summary>
        /// Whether to ask before sharing logs
        /// </summary>
        public bool AskBeforeSharing { get; set; } = true;

        /// <summary>
        /// Pastebin API endpoint
        /// </summary>
        public string ApiEndpoint { get; set; } = "https://pastebin.com/api/api_post.php";

        /// <summary>
        /// Pastebin API key
        /// </summary>
        public string? ApiKey { get; set; }
    }

    /// <summary>
    /// Settings for demo mode and sample data generation.
    /// </summary>
    public class DemoSettings
    {
        /// <summary>
        /// Whether demo mode is enabled by default
        /// </summary>
        public bool EnabledByDefault { get; set; } = false;

        /// <summary>
        /// Update interval in milliseconds
        /// </summary>
        public int UpdateIntervalMs { get; set; } = 1000;

        /// <summary>
        /// Default CPU usage range
        /// </summary>
        public MetricRange DefaultCpuRange { get; set; } = new() { Min = 10, Max = 80 };

        /// <summary>
        /// Default memory usage range
        /// </summary>
        public MetricRange DefaultMemoryRange { get; set; } = new() { Min = 20, Max = 90 };

        /// <summary>
        /// Default FPS range
        /// </summary>
        public MetricRange DefaultFpsRange { get; set; } = new() { Min = 30, Max = 120 };

        public MetricRange DefaultPlayerCount { get; set; } = new() { Min = 1, Max = 64 };
        public int DefaultUpdateFrequencyMs { get; set; } = 1000;
        public double DefaultErrorProbability { get; set; } = 0.1;
        public bool SimulateLatencyByDefault { get; set; } = false;
        public MetricRange DefaultLatencyRange { get; set; } = new() { Min = 50, Max = 200 };
        public bool GenerateTrendsByDefault { get; set; } = true;
        public int DefaultTrendCycleDuration { get; set; } = 60;
    }

    /// <summary>
    /// Range for metric values
    /// </summary>
    public class MetricRange
    {
        /// <summary>
        /// Minimum value
        /// </summary>
        public int Min { get; set; }

        /// <summary>
        /// Maximum value
        /// </summary>
        public int Max { get; set; }
    }

    /// <summary>
    /// Window settings for the application
    /// </summary>
    public class WindowSettings2
    {
        /// <summary>
        /// Whether to remember window position
        /// </summary>
        public bool RememberPosition { get; set; } = true;

        /// <summary>
        /// Whether to remember window size
        /// </summary>
        public bool RememberSize { get; set; } = true;

        /// <summary>
        /// Last window position X coordinate
        /// </summary>
        public double? LastPositionX { get; set; }

        /// <summary>
        /// Last window position Y coordinate
        /// </summary>
        public double? LastPositionY { get; set; }

        /// <summary>
        /// Last window width
        /// </summary>
        public double? LastWidth { get; set; }

        /// <summary>
        /// Last window height
        /// </summary>
        public double? LastHeight { get; set; }
    }
}