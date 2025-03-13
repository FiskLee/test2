using ArmaReforgerServerMonitor.Frontend.Configuration;
using ArmaReforgerServerMonitor.Frontend.Services;
using ArmaReforgerServerMonitor.Frontend.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using UtilitiesGlobalExceptionHandler = ArmaReforgerServerMonitor.Frontend.Utilities.GlobalExceptionHandler;

namespace ArmaReforgerServerMonitor.Frontend
{
    /// <summary>
    /// Main application class for the ArmA Reforger Server Monitor.
    /// Handles application lifecycle, dependency injection, and configuration.
    /// </summary>
    public partial class App : Application
    {
        private static Mutex? _mutex;
        private IServiceProvider? _serviceProvider;
        public IServiceProvider Services => _serviceProvider ?? throw new InvalidOperationException("ServiceProvider is not initialized");
        private IConfiguration? _configuration;
        private AppSettings? _settings;
        private IAuthService? _authService;
        private IThemeService? _themeService;
        private UtilitiesGlobalExceptionHandler? _globalExceptionHandler;
        private readonly ILogger<App> _logger;
        private IUpdateService? _updateService;
        private ISettingsService? _settingsService;
        private IMetricsService? _metricsService;
        private DateTime _lastUpdateTime;
        private int _failureCount;
        private DispatcherTimer? _pollTimer;
        private SemaphoreSlim? _pollSemaphore;

        /// <summary>
        /// Gets the current log file path.
        /// </summary>
        public string LogFilePath { get; private set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the App class.
        /// </summary>
        public App()
        {
            // Configure Serilog early for startup logging
            ConfigureStartupLogging();
            _logger = new SerilogLoggerFactory(Log.Logger).CreateLogger<App>();
            InitializeComponent();
            InitializeApplication();
        }

        private void ConfigureStartupLogging()
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "Logs", $"log-{DateTime.Now:yyyyMMdd}.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            LogFilePath = logPath;

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .Enrich.WithEnvironmentName()
                .Enrich.WithProcessId()
                .Enrich.WithMachineName()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.Debug(
                    outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(logPath,
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}",
                    shared: true)
                .CreateLogger();
        }

        private void InitializeApplication()
        {
            try
            {
                // Check for existing instance using a more efficient method
                var mutexName = "ArmAReforgerServerMonitor";
                _mutex = new Mutex(true, mutexName, out bool createdNew);

                if (!createdNew)
                {
                    MessageBox.Show("Another instance of the application is already running.", "Application Already Running",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    Current.Shutdown();
                    return;
                }

                // Load configuration
                _configuration = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
                    .AddEnvironmentVariables()
                    .Build();

                // Load settings
                _settings = _configuration.GetSection("AppSettings").Get<AppSettings>() ?? new AppSettings();

                // Configure services first
                var services = new ServiceCollection();
                ConfigureServices(services);
                _serviceProvider = services.BuildServiceProvider();

                // Configure logging after services are configured
                ConfigureLogging();

                // Set up error handling
                ConfigureErrorHandling();

                // Initialize services
                _authService = _serviceProvider.GetRequiredService<IAuthService>();
                _themeService = _serviceProvider.GetRequiredService<IThemeService>();
                _globalExceptionHandler = _serviceProvider.GetRequiredService<UtilitiesGlobalExceptionHandler>();
                _updateService = _serviceProvider.GetRequiredService<IUpdateService>();
                _settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
                _metricsService = _serviceProvider.GetRequiredService<IMetricsService>();

                // Ensure services are not null
                if (_authService == null || _themeService == null || _globalExceptionHandler == null || _updateService == null || _settingsService == null || _metricsService == null)
                {
                    throw new InvalidOperationException("One or more required services are not initialized.");
                }

                // Initialize polling and connection state
                _lastUpdateTime = DateTime.MinValue;
                _failureCount = 0;
                _pollSemaphore = new SemaphoreSlim(1, 1);
                _pollTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(_settings.PollIntervalSeconds)
                };
                _pollTimer.Tick += async (s, e) => await PollBackendSafeAsync();

                _logger.LogInformation("Application initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize application");
                MessageBox.Show($"Failed to initialize application: {ex.Message}", "Initialization Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Current.Shutdown();
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            // Add configuration
            services.Configure<AppSettings>(_configuration!.GetSection("AppSettings"));

            // Add logging
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(dispose: true);
            });

            // Add core services
            services.AddSingleton<ILoggingService, LoggingService>();
            services.AddSingleton<IAuthService, AuthService>();
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<IUpdateService, UpdateService>();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<IMetricsService, MetricsService>();
            services.AddSingleton<UtilitiesGlobalExceptionHandler>();
            services.AddSingleton<ICacheService, MemoryCacheService>();
            services.AddSingleton<INetworkService, NetworkService>();
            services.AddSingleton<IDemoService, DemoService>();
            services.AddSingleton<ITelemetryService, TelemetryService>();
            services.AddSingleton<IPastebinService, PastebinService>();
            services.AddSingleton<IOfflineStorage, FileOfflineStorage>();

            // Add HTTP client with retry policy
            services.AddHttpClient("DefaultClient")
                .AddHttpMessageHandler<HttpClientPolicyHandler>();

            // Add main window
            services.AddTransient<MainWindow>();

            _logger.LogInformation("Services configured successfully");
        }

        private void ConfigureLogging()
        {
            if (_configuration == null || _settings == null)
            {
                throw new InvalidOperationException("Configuration or settings not initialized");
            }

            var logPath = Path.Combine(_settings.LoggingSettings.LogsDirectory, $"log-{DateTime.Now:yyyyMMdd}.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            LogFilePath = logPath;

            var logConfig = new LoggerConfiguration()
                .ReadFrom.Configuration(_configuration)
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .Enrich.WithEnvironmentName()
                .Enrich.WithProcessId()
                .Enrich.WithMachineName();

            // Configure sinks based on settings
            if (_settings.LoggingSettings.EnableConsoleLogging)
            {
                logConfig.WriteTo.Console(
                    outputTemplate: _settings.LoggingSettings.OutputTemplate,
                    restrictedToMinimumLevel: _settings.LoggingSettings.GetMinimumLogEventLevel());
            }

            if (_settings.LoggingSettings.EnableDebugLogging)
            {
                logConfig.WriteTo.Debug(
                    outputTemplate: _settings.LoggingSettings.OutputTemplate,
                    restrictedToMinimumLevel: _settings.LoggingSettings.GetMinimumLogEventLevel());
            }

            if (_settings.LoggingSettings.EnableFileLogging)
            {
                logConfig.WriteTo.File(
                    path: logPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: _settings.LoggingSettings.RetentionDays,
                    fileSizeLimitBytes: _settings.LoggingSettings.MaxFileSizeMB * 1024 * 1024,
                    outputTemplate: _settings.LoggingSettings.OutputTemplate,
                    shared: _settings.LoggingSettings.SharedFiles,
                    flushToDiskInterval: _settings.LoggingSettings.ImmediateFlush ? TimeSpan.Zero : TimeSpan.FromSeconds(1));
            }

            // Apply namespace-specific overrides
            foreach (var (ns, level) in _settings.LoggingSettings.LevelOverrides)
            {
                if (Enum.TryParse<LogEventLevel>(level, true, out var logLevel))
                {
                    logConfig.MinimumLevel.Override(ns, logLevel);
                }
            }

            Log.Logger = logConfig.CreateLogger();
            _logger.LogInformation("Logging configured successfully");
        }

        private void ConfigureErrorHandling()
        {
            DispatcherUnhandledException += (s, e) => _globalExceptionHandler?.HandleDispatcherUnhandledException(s, e);
            TaskScheduler.UnobservedTaskException += (s, e) => _globalExceptionHandler?.HandleUnobservedTaskException(s, e);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => _globalExceptionHandler?.HandleUnhandledException(s, e);
            _logger.LogInformation("Error handling configured successfully");
        }

        private async Task PollBackendSafeAsync()
        {
            if (_pollSemaphore == null || _metricsService == null)
            {
                return;
            }

            if (!await _pollSemaphore.WaitAsync(0))
            {
                return;
            }

            try
            {
                await _metricsService.GetOSMetricsAsync(new Uri("http://server"));
                _lastUpdateTime = DateTime.UtcNow;
                _failureCount = 0;
            }
            catch (Exception ex)
            {
                _failureCount++;
                _logger.LogError(ex, "Failed to poll backend. Attempt {FailureCount}", _failureCount);

                if (_settings != null && _failureCount >= _settings.MaxRetryAttempts)
                {
                    _pollTimer?.Stop();
                    _logger.LogError("Max retry attempts reached. Polling stopped.");
                }
            }
            finally
            {
                _pollSemaphore.Release();
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                _logger.LogInformation("Application starting up");
                var mainWindow = Services.GetRequiredService<MainWindow>();
                mainWindow.Show();
                _pollTimer?.Start();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start application");
                MessageBox.Show($"Failed to start application: {ex.Message}", "Startup Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _logger.LogInformation("Application shutting down");
                _pollTimer?.Stop();
                _mutex?.ReleaseMutex();
                _mutex?.Dispose();
                (_serviceProvider as IDisposable)?.Dispose();
                Log.CloseAndFlush();
            }
            catch (Exception ex)
            {
                // At this point, logging might not work, so we'll try console as last resort
                Console.Error.WriteLine($"Error during shutdown: {ex.Message}");
            }

            base.OnExit(e);
        }
    }
}
