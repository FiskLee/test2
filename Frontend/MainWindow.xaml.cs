using ArmaReforgerServerMonitor.Frontend.Configuration;
using ArmaReforgerServerMonitor.Frontend.Models;
using ArmaReforgerServerMonitor.Frontend.Rcon;
using ArmaReforgerServerMonitor.Frontend.Services;
using ArmaReforgerServerMonitor.Frontend.Windows;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using MahApps.Metro.Controls;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using NetworkException = ArmaReforgerServerMonitor.Frontend.Services.NetworkException;

namespace ArmaReforgerServerMonitor.Frontend
{
    /// <summary>
    /// Main window of the ArmA Reforger Server Monitor application.
    /// Provides the primary user interface for monitoring and managing servers.
    /// </summary>
    /// <remarks>
    /// This window serves as the central hub for the application, providing:
    /// - Server status monitoring
    /// - Performance metrics visualization
    /// - Console log viewing
    /// - Server management controls
    /// - Configuration options
    /// 
    /// The window updates automatically based on the polling interval
    /// and provides real-time feedback about server health and performance.
    /// </remarks>
    public partial class MainWindow : MetroWindow, INotifyPropertyChanged, IDisposable
    {
        private readonly IAuthService _authService;
        private readonly IMetricsService _metricsService;
        private readonly IThemeService _themeService;
        private readonly IUpdateService _updateService;
        private readonly IDemoService _demoService;
        private readonly AppSettings _settings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ISettingsService _settingsService;
        private readonly object _lock = new();

        private readonly DispatcherTimer _pollTimer;
        private bool _isConnected = false;
        private int _failureCount = 0;
        private const int MaxFailures = 3;
        private DateTime _lastUpdateTime;

        private readonly ObservableCollection<double> _cpuValues = new();
        private readonly ObservableCollection<double> _memoryValues = new();
        private readonly ObservableCollection<double> _fpsValues = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        private string _performanceSummary = "No data";
        public string PerformanceSummary
        {
            get => _performanceSummary;
            set => SetProperty(ref _performanceSummary, value);
        }

        private string _consoleLogSummary = "No console log entries parsed yet";
        public string ConsoleLogSummary
        {
            get => _consoleLogSummary;
            set => SetProperty(ref _consoleLogSummary, value);
        }

        private string _status = "Disconnected";
        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        private string _rconPort = "";
        public string RconPort
        {
            get => _rconPort;
            set => SetProperty(ref _rconPort, value);
        }

        private string _rconPassword = "";
        public string RconPassword
        {
            get => _rconPassword;
            set => SetProperty(ref _rconPassword, value);
        }

        // Chart series
        private ObservableCollection<ISeries> _totalCpuSeries = new();
        public ObservableCollection<ISeries> TotalCpuSeries
        {
            get => _totalCpuSeries;
        }

        private ObservableCollection<ISeries> _cpuSeries = new();
        public ObservableCollection<ISeries> CpuSeries
        {
            get => _cpuSeries;
        }

        private ObservableCollection<ISeries> _memorySeries = new();
        public ObservableCollection<ISeries> MemorySeries
        {
            get => _memorySeries;
        }

        private ObservableCollection<ISeries> _fpsSeries = new();
        public ObservableCollection<ISeries> FPSSeries
        {
            get => _fpsSeries;
        }

        private ObservableCollection<ISeries> _frameTimeSeries = new();
        public ObservableCollection<ISeries> FrameTimeSeries
        {
            get => _frameTimeSeries;
        }

        private ObservableCollection<ISeries> _activePlayersSeries = new();
        public ObservableCollection<ISeries> ActivePlayersSeries
        {
            get => _activePlayersSeries;
        }

        // Labels for the charts
        private ObservableCollection<string> _totalCpuLabels = new();
        public ObservableCollection<string> TotalCpuLabels
        {
            get => _totalCpuLabels;
        }

        private ObservableCollection<string> _cpuLabels = new();
        public ObservableCollection<string> CpuLabels
        {
            get => _cpuLabels;
        }

        private ObservableCollection<string> _memoryLabels = new();
        public ObservableCollection<string> MemoryLabels
        {
            get => _memoryLabels;
        }

        private ObservableCollection<string> _fpsLabels = new();
        public ObservableCollection<string> FPSLabels
        {
            get => _fpsLabels;
        }

        private ObservableCollection<string> _frameTimeLabels = new();
        public ObservableCollection<string> FrameTimeLabels
        {
            get => _frameTimeLabels;
        }

        private ObservableCollection<string> _activePlayersLabels = new();
        public ObservableCollection<string> ActivePlayersLabels
        {
            get => _activePlayersLabels;
        }

        // Formatters.
        public Func<double, string> TotalCpuFormatter { get; set; } = value => $"{value:N0}%";
        public Func<double, string> CpuFormatter { get; set; } = value => $"{value:N0}%";
        public Func<double, string> MemoryFormatter { get; set; } = value => $"{value:N2} GB";
        public Func<double, string> FPSFormatter { get; set; } = value => $"{value:N0}";
        public Func<double, string> FrameTimeFormatter { get; set; } = value => $"{value:N0} ms";
        public Func<double, string> ActivePlayersFormatter { get; set; } = value => $"{value:N0}";

        // RCON client wrapper.
        private BattleyeRconClient? _battleyeRconClient;

        private TextBlock _statusTextBlock;

        private readonly CancellationTokenSource _cts = new();
        private readonly SemaphoreSlim _pollSemaphore = new(1, 1);

        private readonly ConnectionStateTracker _connectionStateTracker;
        private string _currentConnectionId = string.Empty;

        private bool _isDisposed;

        private readonly ILogger _logger;

        private readonly IServiceProvider _serviceProvider;

        private string _networkStatus;
        public string NetworkStatus
        {
            get => _networkStatus;
            set => SetProperty(ref _networkStatus, value);
        }

        private string _diagnosticsText;
        public string DiagnosticsText
        {
            get => _diagnosticsText;
            set => SetProperty(ref _diagnosticsText, value);
        }

        /// <summary>
        /// Default constructor that resolves dependencies from the application's service provider.
        /// </summary>
        public MainWindow(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));
            _serviceProvider = serviceProvider;

            var app = Application.Current as App;
            if (app == null)
            {
                throw new InvalidOperationException("Application.Current is not of type App");
            }

            var services = app.Services;
            _authService = services.GetRequiredService<IAuthService>();
            _metricsService = services.GetRequiredService<IMetricsService>();
            _themeService = services.GetRequiredService<IThemeService>();
            _updateService = services.GetRequiredService<IUpdateService>();
            _demoService = services.GetRequiredService<IDemoService>();
            _settings = services.GetRequiredService<AppSettings>();
            _httpClientFactory = services.GetRequiredService<IHttpClientFactory>();
            _settingsService = services.GetRequiredService<ISettingsService>();

            InitializeComponent();
            SetupWindow();

            _pollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(_settings.PollIntervalSeconds)
            };
            _pollTimer.Tick += async (s, e) => await PollBackendSafeAsync().ConfigureAwait(false);

            // Initialize demo mode if enabled by default
            if (_settings.DemoSettings.EnabledByDefault)
            {
                _demoService.SetDemoMode(true);
                UpdateDemoParameters();
            }

            _statusTextBlock = (TextBlock)FindName("StatusTextBlock");

            _connectionStateTracker = new ConnectionStateTracker(services.GetRequiredService<ILogger<ConnectionStateTracker>>());

            // Initialize the _battleyeRconClient field
            _battleyeRconClient = services.GetRequiredService<BattleyeRconClient>();

            _logger.LogInformation("FrontendLogsWindow initialized.");
        }

        /// <summary>
        /// Sets up the window's initial state and event handlers.
        /// </summary>
        /// <remarks>
        /// This method:
        /// 1. Configures window properties
        /// 2. Sets up UI controls
        /// 3. Subscribes to events
        /// 4. Initializes data bindings
        /// </remarks>
        private void SetupWindow()
        {
            Title = $"ArmA Reforger Server Monitor v{_settings.Version}";
            DataContext = this;

            // Apply theme
            var (theme, accent) = _themeService.GetCurrentTheme();
            _themeService.SetTheme(theme, accent);

            // Subscribe to events
            Loaded += OnWindowLoaded;
            Closing += OnWindowClosing;
            _themeService.ThemeChanged += OnThemeChanged;
            _updateService.UpdateStatusChanged += OnUpdateStatusChanged;

            _logger.LogDebug("Window setup completed");
        }

        /// <summary>
        /// Handles the window loaded event.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>
        /// <remarks>
        /// This method:
        /// 1. Checks for updates
        /// 2. Attempts initial connection
        /// 3. Starts polling timer
        /// </remarks>
        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.LogInformation("Window loaded, initializing components");

                // Initialize RCON port from settings if available
                if (_settings != null && !string.IsNullOrWhiteSpace(_settings.RconPort))
                {
                    RconPortTextBox.Text = _settings.RconPort;
                    _logger.LogDebug("Initialized RCON port from settings: {Port}", _settings.RconPort);
                }
                else
                {
                    _logger.LogDebug("No RCON port found in settings, using default");
                }

                // Only start polling timer if we're in demo mode or have valid credentials
                if (_demoService.IsDemoMode || !string.IsNullOrWhiteSpace(ServerUrlTextBox.Text))
                {
                    _pollTimer.Start();
                }

                // Check for updates asynchronously
                await CheckForUpdates().ConfigureAwait(false);

                // Only attempt connection if we have valid credentials
                if (!string.IsNullOrWhiteSpace(ServerUrlTextBox.Text) &&
                    !string.IsNullOrWhiteSpace(UsernameTextBox.Text) &&
                    !string.IsNullOrWhiteSpace(PasswordBox.Password))
                {
                    await ConnectToServer().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during window initialization");
                MessageBox.Show($"An error occurred during initialization: {ex.Message}", "Initialization Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handles the window closing event.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>
        /// <remarks>
        /// This method:
        /// 1. Stops polling
        /// 2. Saves settings
        /// 3. Cleans up resources
        /// 4. Logs out if authenticated
        /// </remarks>
        private void OnWindowClosing(object? sender, CancelEventArgs e)
        {
            try
            {
                _logger.LogInformation("Window closing, performing cleanup");

                _pollTimer.Stop();

                if (!_cts.IsCancellationRequested)
                {
                    _cts.Cancel();
                }

                if (_authService != null && _authService.IsAuthenticated)
                {
                    _authService.Logout();
                }

                if (_battleyeRconClient != null)
                {
                    await _battleyeRconClient.DisconnectAsync().ConfigureAwait(false);
                    _battleyeRconClient.Dispose();
                }

                _pollSemaphore?.Dispose();
                _cts?.Dispose();

                await SaveSettings().ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning(ex, "Operation canceled during window cleanup");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during window cleanup");
            }
        }

        /// <summary>
        /// Polls the backend server for updates.
        /// </summary>
        /// <remarks>
        /// This method:
        /// 1. Checks connection status
        /// 2. Retrieves server metrics
        /// 3. Updates UI with new data
        /// 4. Handles any errors
        /// 
        /// Called automatically by the polling timer.
        /// </remarks>
        private async Task PollBackend()
        {
            try
            {
                if (!_isConnected && !_demoService.IsDemoMode)
                {
                    return;
                }

                OSDataDTO? metrics;
                if (_demoService.IsDemoMode)
                {
                    metrics = await _demoService.GetDemoMetricsAsync().ConfigureAwait(false);
                    var demoStatus = await _demoService.GetDemoServerStatusAsync().ConfigureAwait(false);
                    var demoLogs = await _demoService.GetDemoConsoleLogsAsync(10).ConfigureAwait(false);
                    await UpdateMetricsDisplayAsync(metrics).ConfigureAwait(false);
                    UpdateServerStatus(demoStatus);
                    UpdateConsoleLogs(demoLogs);
                }
                else
                {
                    _logger.LogDebug("Polling backend for updates");

                    if (!_authService.IsAuthenticated)
                    {
                        return; // Don't attempt to connect automatically
                    }

                    var serverUrl = ServerUrlTextBox.Text;
                    if (string.IsNullOrWhiteSpace(serverUrl))
                    {
                        return; // Don't attempt to connect if no URL is entered
                    }

                    var serverUri = new Uri(serverUrl);
                    metrics = await _metricsService.GetOSMetricsAsync(serverUri).ConfigureAwait(false);
                    if (metrics != null)
                    {
                        await UpdateMetricsDisplayAsync(metrics).ConfigureAwait(false);
                        _lastUpdateTime = DateTime.Now;
                        _isConnected = true;
                    }
                    else
                    {
                        HandleConnectionLoss("Failed to retrieve metrics");
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogWarning("Authentication expired");
                HandleConnectionLoss("Authentication expired");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling backend");
                HandleConnectionLoss("Error polling backend: " + ex.Message);
            }
        }

        /// <summary>
        /// Attempts to connect to the server.
        /// </summary>
        /// <returns>Task representing the connection attempt</returns>
        /// <remarks>
        /// This method:
        /// 1. Validates server URL
        /// 2. Attempts authentication
        /// 3. Updates connection status
        /// 4. Handles connection errors
        /// </remarks>
        private async Task ConnectToServer()
        {
            try
            {
                var serverUrl = ServerUrlTextBox.Text;
                if (string.IsNullOrWhiteSpace(serverUrl))
                {
                    throw new ArgumentException("Server URL cannot be empty");
                }

                _logger.LogInformation("Attempting to connect to server: {Url}", serverUrl);

                var (success, error) = await _authService.LoginAsync(
                    serverUrl,
                    UsernameTextBox.Text,
                    PasswordBox.Password).ConfigureAwait(false);

                if (success)
                {
                    _logger.LogInformation("Successfully connected to server");
                    _isConnected = true;
                    UpdateConnectionStatus(true, "Successfully connected to server");
                }
                else
                {
                    _logger.LogWarning("Failed to connect: {Error}", error);
                    HandleConnectionLoss(error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to server");
                HandleConnectionLoss(ex.Message);
            }
        }

        /// <summary>
        /// Handles loss of connection to the server.
        /// </summary>
        /// <param name="reason">Reason for connection loss</param>
        /// <remarks>
        /// This method:
        /// 1. Updates connection status
        /// 2. Shows error message
        /// 3. Attempts reconnection if appropriate
        /// 4. Updates UI to reflect disconnected state
        /// </remarks>
        private void HandleConnectionLoss(string reason)
        {
            _logger.LogWarning("Connection lost: {Reason}", reason);

            _isConnected = false;
            UpdateConnectionStatus(false, reason);

            if (_settings.AutoReconnect && _failureCount < MaxFailures)
            {
                _failureCount++;
                _logger.LogInformation("Auto-reconnect enabled, will attempt reconnection (attempt {Count}/{Max})",
                    _failureCount, MaxFailures);
            }
            else if (_failureCount >= MaxFailures)
            {
                _logger.LogWarning("Max reconnection attempts reached");
                _failureCount = 0;
            }
        }

        /// <summary>
        /// Updates the connection status display.
        /// </summary>
        /// <param name="connected">Whether connected to server</param>
        /// <param name="message">Optional status message</param>
        /// <remarks>
        /// Updates UI elements to reflect current connection state:
        /// - Status indicator
        /// - Connection time
        /// - Error messages
        /// - Available features
        /// </remarks>
        private void UpdateConnectionStatus(bool connected, string message = "Connected")
        {
            try
            {
                _logger.LogDebug("Updating connection status - Connected: {Connected}, Message: {Message}", connected, message);

                if (!Dispatcher.CheckAccess())
                {
                    _logger.LogDebug("Cross-thread UI update detected, dispatching to UI thread");
                    Dispatcher.Invoke(() => UpdateConnectionStatus(connected, message));
                    return;
                }

                // Log UI state before changes
                _logger.LogDebug("Current UI state:\n" +
                    "1. Status Text: {CurrentStatus}\n" +
                    "2. Status Color: {CurrentColor}\n" +
                    "3. Connect Button Text: {ButtonText}\n" +
                    "4. Connect Button Enabled: {ButtonEnabled}",
                    StatusTextBlock?.Text ?? "Not initialized",
                    StatusTextBlock?.Foreground ?? Brushes.Black,
                    ConnectButton?.Content ?? "Not initialized",
                    ConnectButton?.IsEnabled ?? false);

                // Update UI elements with error handling
                try
                {
                    if (StatusTextBlock != null)
                    {
                        StatusTextBlock.Text = message;
                        StatusTextBlock.Foreground = connected ? Brushes.Green : Brushes.Red;
                    }

                    if (ConnectButton != null)
                    {
                        ConnectButton.Content = connected ? "Disconnect" : "Connect";
                        ConnectButton.IsEnabled = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating UI elements:\n" +
                        "1. Exception Details:\n" +
                        "   - Type: {ExceptionType}\n" +
                        "   - Message: {Message}\n" +
                        "   - Stack Trace: {StackTrace}\n" +
                        "2. UI State:\n" +
                        "   - StatusTextBlock: {StatusBlock}\n" +
                        "   - ConnectButton: {ConnectButton}\n" +
                        "3. Update Values:\n" +
                        "   - Connected: {Connected}\n" +
                        "   - Message: {Message}",
                        ex.GetType().Name,
                        ex.Message,
                        ex.StackTrace,
                        StatusTextBlock != null ? "Initialized" : "Not initialized",
                        ConnectButton != null ? "Initialized" : "Not initialized",
                        connected,
                        message);
                    throw;
                }

                // Log UI state after changes
                _logger.LogDebug("Updated UI state:\n" +
                    "1. Status Text: {NewStatus}\n" +
                    "2. Status Color: {NewColor}\n" +
                    "3. Connect Button Text: {NewButtonText}\n" +
                    "4. Connect Button Enabled: {NewButtonEnabled}",
                    StatusTextBlock?.Text ?? "Not initialized",
                    StatusTextBlock?.Foreground ?? Brushes.Black,
                    ConnectButton?.Content ?? "Not initialized",
                    ConnectButton?.IsEnabled ?? false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error in UpdateConnectionStatus:\n" +
                    "1. Exception Details:\n" +
                    "   - Type: {ExceptionType}\n" +
                    "   - Message: {Message}\n" +
                    "   - Stack Trace: {StackTrace}\n" +
                    "2. Connection State:\n" +
                    "   - Connected: {Connected}\n" +
                    "   - Message: {Message}\n" +
                    "3. UI State:\n" +
                    "   - Dispatcher: {Dispatcher}\n" +
                    "   - UI Thread: {UIThread}",
                    ex.GetType().Name,
                    ex.Message,
                    ex.StackTrace,
                    connected,
                    message,
                    Dispatcher != null ? "Initialized" : "Not initialized",
                    Dispatcher?.Thread.ManagedThreadId ?? -1);
                throw;
            }
        }

        /// <summary>
        /// Updates the metrics display with new data.
        /// </summary>
        /// <param name="metrics">New metrics data</param>
        /// <remarks>
        /// Updates various UI elements with new metrics:
        /// - Performance graphs
        /// - Resource usage
        /// - Player count
        /// - Server status
        /// </remarks>
        private async Task UpdateMetricsDisplayAsync(OSDataDTO metrics)
        {
            if (metrics == null)
            {
                _logger.LogWarning("Received null metrics data");
                return;
            }

            if (!Dispatcher.CheckAccess())
            {
                await Dispatcher.InvokeAsync(() => UpdateMetricsDisplayAsync(metrics));
                return;
            }

            try
            {
                // Update performance summary
                PerformanceSummary = $"CPU: {metrics.CpuUsage:F1}%\n" +
                                   $"Memory: {metrics.MemoryUsage:F1} GB\n" +
                                   $"FPS: {metrics.FPS:F0}\n" +
                                   $"Active Players: {metrics.ActivePlayers}";

                // Update charts
                UpdateChart(TotalCpuSeries, TotalCpuLabels, metrics.CpuUsage, "Total CPU");
                UpdateChart(CpuSeries, CpuLabels, metrics.CpuUsage, "CPU");
                UpdateChart(MemorySeries, MemoryLabels, metrics.MemoryUsage, "Memory");
                UpdateChart(FPSSeries, FPSLabels, metrics.FPS, "FPS");
                UpdateChart(ActivePlayersSeries, ActivePlayersLabels, metrics.ActivePlayers, "Players");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating metrics display");
            }
        }

        /// <summary>
        /// Shows the update available dialog.
        /// </summary>
        /// <remarks>
        /// Displays a dialog when updates are available:
        /// - Version information
        /// - Release notes
        /// - Update options
        /// - Download progress
        /// </remarks>
        private async void ShowUpdateAvailableDialog()
        {
            var info = await _updateService.GetLatestVersionInfoAsync().ConfigureAwait(false);
            if (info == null) return;

            var result = MessageBox.Show(
                $"A new version ({info.Version}) is available.\n\n{info.ReleaseNotes}\n\nWould you like to update now?",
                "Update Available",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                await _updateService.DownloadAndInstallUpdateAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Saves current application settings.
        /// </summary>
        /// <remarks>
        /// Saves various settings including:
        /// - Window position/size
        /// - User preferences
        /// - Connection details
        /// - UI customization
        /// </remarks>
        private async Task SaveSettings()
        {
            try
            {
                _logger.Information("Saving application settings");

                // Save window position and size
                await Dispatcher.InvokeAsync(() => SaveWindowSettings()).ConfigureAwait(false);

                // Save other settings asynchronously
                await Task.Run(() => _settingsService.SaveSettings()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save settings");
                throw;
            }
        }

        private void SaveWindowSettings()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => SaveWindowSettings());
                return;
            }

            _settings.WindowSettings = new WindowSettings
            {
                Left = Left,
                Top = Top,
                Width = Width,
                Height = Height,
                WindowState = WindowState
            };
        }

        #region Event Handlers

        /// <summary>
        /// Handles theme change events.
        /// </summary>
        private void OnThemeChanged(object? sender, EventArgs e)
        {
            _logger.Information("Theme changed, updating window appearance");
            // Theme changes are handled automatically by MahApps.Metro
        }

        /// <summary>
        /// Handles update status change events.
        /// </summary>
        private void OnUpdateStatusChanged(object? sender, Models.UpdateStatusEventArgs e)
        {
            _logger.Information("Update status changed: {Status}", e.Status);

            if (e.Status == UpdateStatus.UpdateAvailable)
            {
                Dispatcher.Invoke(ShowUpdateAvailableDialog);
            }
        }

        #endregion

        private void UpdateServerStatus(Models.ServerStatus status)
        {
            if (status == null)
            {
                _logger.Warning("Received null server status");
                return;
            }

            // Update UI with server status
            Status = status.IsRunning ? "Running" : "Stopped";
            PerformanceSummary = $"FPS: {status.Performance.FPS:F1} | Frame Time: {status.Performance.FrameTime:F1}ms | Players: {status.CurrentPlayers}/{status.MaxPlayers}";

            // Update charts
            UpdateChart(FPSSeries, FPSLabels, status.Performance.FPS, "FPS");
            UpdateChart(FrameTimeSeries, FrameTimeLabels, status.Performance.FrameTime, "Frame Time (ms)");
            UpdateChart(ActivePlayersSeries, ActivePlayersLabels, status.CurrentPlayers, "Players");
        }

        private void UpdateConsoleLogs(List<ConsoleLog> logs)
        {
            if (logs == null) return;

            var summary = new StringBuilder();
            foreach (var log in logs)
            {
                if (log != null)
                {
                    summary.AppendLine($"{log.Timestamp:yyyy-MM-dd HH:mm:ss} [{log.Level}] {log.Message}");
                }
            }
            ConsoleLogSummary = summary.ToString(CultureInfo.InvariantCulture);
        }

        private void UpdateChart<T>(ObservableCollection<ISeries> series, ObservableCollection<string> labels, T value, string title)
        {
            const int MaxDataPoints = 60;

            if (series == null)
            {
                _logger.Warning("Series collection is null");
                return;
            }

            if (labels == null)
            {
                _logger.Warning("Labels collection is null");
                return;
            }

            if (series[0] is LineSeries<T> lineSeries)
            {
                if (lineSeries.Values is ObservableCollection<T> values)
                {
                    values.Add(value);

                    if (values.Count > MaxDataPoints)
                    {
                        values.RemoveAt(0);
                        if (labels.Count > 0)
                        {
                            labels.RemoveAt(0);
                        }
                    }
                }
                else
                {
                    _logger.Warning("Invalid Values type in LineSeries");
                    return;
                }
            }
            else
            {
                _logger.Warning("Invalid series type in chart");
                return;
            }

            labels.Add(DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        }

        private void InitializeMenu()
        {
            var demoMenu = new MenuItem { Header = "_Demo" };

            var toggleDemoMode = new MenuItem
            {
                Header = "Toggle Demo Mode",
                IsCheckable = true,
                IsChecked = _demoService.IsDemoMode
            };
            toggleDemoMode.Click += OnToggleDemoModeClick;

            var configureDemoMode = new MenuItem { Header = "Configure Demo Settings..." };
            configureDemoMode.Click += OnConfigureDemoClick;

            demoMenu.Items.Add(toggleDemoMode);
            demoMenu.Items.Add(new Separator());
            demoMenu.Items.Add(configureDemoMode);

            MainMenu.Items.Add(demoMenu);
        }

        private void OnConfigureDemoClick(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowDemoConfigDialog().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error configuring demo mode");
                MessageBox.Show("Failed to configure demo mode. Please try again.", "Configuration Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ShowDemoConfigDialog()
        {
            if (_settings?.DemoSettings == null)
            {
                _logger.Warning("Demo settings not initialized");
                return;
            }

            var demoParams = new DemoParameters
            {
                CpuRange = _settings.DemoSettings.DefaultCpuRange,
                MemoryRange = _settings.DemoSettings.DefaultMemoryRange,
                FpsRange = _settings.DemoSettings.DefaultFpsRange,
                PlayerCount = _settings.DemoSettings.DefaultPlayerCount,
                UpdateFrequencyMs = _settings.DemoSettings.DefaultUpdateFrequencyMs,
                ErrorProbability = _settings.DemoSettings.DefaultErrorProbability,
                SimulateLatency = _settings.DemoSettings.SimulateLatencyByDefault,
                LatencyRange = _settings.DemoSettings.DefaultLatencyRange,
                GenerateTrends = _settings.DemoSettings.GenerateTrendsByDefault,
                TrendCycleDuration = _settings.DemoSettings.DefaultTrendCycleDuration
            };

            var dialog = new DemoConfigWindow(_demoService, demoParams);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                UpdateDemoParameters(demoParams).ConfigureAwait(false);
            }
        }

        private async Task UpdateDemoParameters(DemoParameters parameters)
        {
            if (parameters == null)
            {
                _logger.Warning("Attempted to update demo parameters with null value");
                return;
            }

            if (_settings?.DemoSettings == null)
            {
                _logger.Warning("Demo settings not initialized");
                return;
            }

            _settings.DemoSettings.DefaultCpuRange = parameters.CpuRange;
            _settings.DemoSettings.DefaultMemoryRange = parameters.MemoryRange;
            _settings.DemoSettings.DefaultFpsRange = parameters.FpsRange;
            _settings.DemoSettings.DefaultPlayerCount = parameters.PlayerCount;
            _settings.DemoSettings.DefaultUpdateFrequencyMs = parameters.UpdateFrequencyMs;
            _settings.DemoSettings.DefaultErrorProbability = parameters.ErrorProbability;
            _settings.DemoSettings.SimulateLatencyByDefault = parameters.SimulateLatency;
            _settings.DemoSettings.DefaultLatencyRange = parameters.LatencyRange;
            _settings.DemoSettings.GenerateTrendsByDefault = parameters.GenerateTrends;
            _settings.DemoSettings.DefaultTrendCycleDuration = parameters.TrendCycleDuration;

            _settingsService.SaveSettings();
            await _demoService.RestartAsync(_cts.Token).ConfigureAwait(false);
        }

        private async void OnToggleDemoModeClick(object sender, RoutedEventArgs e)
        {
            var menuItem = (MenuItem)sender;
            _demoService.SetDemoMode(menuItem.IsChecked);
            if (menuItem.IsChecked)
            {
                await ShowDemoConfigDialog().ConfigureAwait(false);
            }
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is string theme)
            {
                var (currentTheme, accent) = _themeService.GetCurrentTheme();
                _themeService.SetTheme(theme, accent);
            }
        }

        private void AccentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is string accent)
            {
                var theme = _themeService.GetCurrentTheme().Theme;
                _themeService.SetTheme(theme, accent);
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private async void OnConnectClick(object sender, RoutedEventArgs e)
        {
            await Task.Run(() => Connect());
        }

        private async void OnDisconnectClick(object sender, RoutedEventArgs e)
        {
            await Task.Run(() => Disconnect());
        }

        private void OnExitClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnThemeClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string theme)
            {
                _themeService.SetTheme(theme, _settings.Theme.DefaultAccent);
            }
        }

        private void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            RefreshData();
        }

        private void RefreshData()
        {
            // TODO: Implement refresh logic
            _logger.Information("Refreshing data...");
        }

        private async Task CheckForUpdates(bool userInitiated = false)
        {
            try
            {
                _logger.Information("Checking for updates...");
                var updateAvailable = await _updateService.CheckForUpdatesAsync().ConfigureAwait(false);

                if (updateAvailable)
                {
                    ShowUpdateAvailableDialog();
                }
                else if (userInitiated)
                {
                    MessageBox.Show("No updates available.", "Update Check", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.Warning("GitHub API authentication failed. Please check your GitHub token configuration.");
                if (userInitiated)
                {
                    MessageBox.Show("Unable to check for updates due to GitHub API authentication issues. Please check your GitHub token configuration.",
                        "Update Check", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking for updates");
                if (userInitiated)
                {
                    MessageBox.Show($"Error checking for updates: {ex.Message}", "Update Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async Task PollBackendSafeAsync()
        {
            if (!await _pollSemaphore.WaitAsync(0).ConfigureAwait(false))
            {
                _logger.Debug("Polling already in progress, skipping");
                return;
            }

            try
            {
                await PollBackend().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error polling backend");
                HandleConnectionLoss($"Error polling backend: {ex.Message}");
            }
            finally
            {
                _pollSemaphore.Release();
            }
        }

        private async Task ConnectToRconAsync()
        {
            try
            {
                if (!ValidateConnectionInputs())
                {
                    return;
                }

                var port = int.Parse(RconPort, CultureInfo.InvariantCulture);
                var password = RconPassword;

                await _rconClient.ConnectAsync(ServerAddress, port, password).ConfigureAwait(false);
                IsRconConnected = true;
                _logger.Information("Connected to RCON on {ServerAddress}:{Port}", ServerAddress, port);
            }
            catch (FormatException ex)
            {
                _logger.Error(ex, "Invalid RCON port format: {Message}", ex.Message);
                MessageBox.Show("Invalid RCON port format. Please enter a valid number.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (ConnectionException ex)
            {
                _logger.Error(ex, "Failed to connect to RCON: {Message}", ex.Message);
                MessageBox.Show($"Failed to connect to RCON: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error connecting to RCON: {Message}", ex.Message);
                MessageBox.Show("An unexpected error occurred while connecting to RCON.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static bool ValidateConnectionInputs()
        {
            if (string.IsNullOrWhiteSpace(ServerAddress))
            {
                MessageBox.Show("Please enter a server address.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (string.IsNullOrWhiteSpace(RconPort))
            {
                MessageBox.Show("Please enter an RCON port.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (string.IsNullOrWhiteSpace(RconPassword))
            {
                MessageBox.Show("Please enter an RCON password.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        private async Task PerformNetworkDiagnosticsAsync(string host, int port)
        {
            try
            {
                var diagnostics = new StringBuilder();
                diagnostics.AppendLine($"Network Diagnostics for {host}:{port}");
                diagnostics.AppendLine($"Ping: {pingResult.RoundTripTime}ms");
                diagnostics.AppendLine($"Bandwidth: {bandwidth:F2} Mbps");
                diagnostics.AppendLine($"Jitter: {jitter:F2}ms");
                diagnostics.AppendLine($"Network Quality: {quality}");

                var ping = state.NetworkStats.Ping;
                var bandwidth = state.NetworkStats.Bandwidth;

                var jitter = NetworkQualityHelper.CalculateJitter(pingResult.RoundTripTime);

                var quality = GetNetworkStatusText(ping, jitter, bandwidth);

                DiagnosticsText = diagnostics.ToString();
            }
            catch (NetworkException ex)
            {
                _logger.Error(ex, "Network diagnostics failed: {Message}", ex.Message);
                MessageBox.Show($"Network diagnostics failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error during network diagnostics: {Message}", ex.Message);
                MessageBox.Show("An unexpected error occurred during network diagnostics.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string GetNetworkStatusText(double ping, double jitter, double bandwidth)
        {
            if (ping < 50 && jitter < 10 && bandwidth > 10)
                return "Excellent";
            if (ping < 100 && jitter < 20 && bandwidth > 5)
                return "Good";
            if (ping < 200 && jitter < 50 && bandwidth > 2)
                return "Fair";
            return "Poor";
        }

        private static double CalculateJitter(double ping)
        {
            // Simple jitter calculation based on ping variation
            return ping * 0.1; // 10% of ping as a rough estimate
        }

        private static string AssessNetworkQuality(double ping, double jitter, double bandwidth)
        {
            return GetNetworkStatusText(ping, jitter, bandwidth);
        }

        private async Task<double> GetNetworkBandwidth(string host)
        {
            try
            {
                // Implement actual bandwidth measurement logic here
                // For now, return a simulated value
                return 10.0;
            }
            catch (NetworkException ex)
            {
                _logger.Error(ex, "Failed to measure network bandwidth: {Message}", ex.Message);
                throw;
            }
        }

        private void UpdateConnectionNetworkStats(ConnectionState state)
        {
            try
            {
                if (state?.NetworkStats == null)
                    return;

                if (state.NetworkStats.TryGetValue("ping", out var pingObj) &&
                    state.NetworkStats.TryGetValue("bandwidth", out var bandwidthObj))
                {
                    var ping = Convert.ToDouble(pingObj, CultureInfo.InvariantCulture);
                    var bandwidth = Convert.ToDouble(bandwidthObj, CultureInfo.InvariantCulture);

                    NetworkStatus = GetNetworkStatusText(ping, CalculateJitterValue(ping), bandwidth);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update network stats: {Message}", ex.Message);
            }
        }

        private static double CalculateJitterValue(double ping)
        {
            // Simple jitter calculation based on ping variation
            return ping * 0.1; // 10% of ping as a rough estimate
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _cts?.Cancel();
                    _cts?.Dispose();
                    _pollSemaphore?.Dispose();
                    _battleyeRconClient?.Dispose();
                }
                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Remove these methods as they are no longer needed

        private void OnAboutClick(object sender, RoutedEventArgs e)
        {
            // Implement the logic for the About click event
            MessageBox.Show("About this application", "About", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
        {
            // Implement the logic for checking updates
            MessageBox.Show("Checking for updates...", "Check Updates", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void FetchRawDataButton_Click(object sender, RoutedEventArgs e)
        {
            // Implement the logic for fetching raw data
            _logger.Information("Fetching raw data...");
        }

        private void StopRawDataButton_Click(object sender, RoutedEventArgs e)
        {
            // Implement the logic for stopping raw data fetch
            _logger.Information("Stopping raw data fetch...");
        }

        private void FetchBackendLogsButton_Click(object sender, RoutedEventArgs e)
        {
            // Implement the logic for fetching backend logs
            _logger.Information("Fetching backend logs...");
        }

        private void StopBackendLogsButton_Click(object sender, RoutedEventArgs e)
        {
            // Implement the logic for stopping backend logs fetch
            _logger.Information("Stopping backend logs fetch...");
        }

        private void FetchFrontendLogsButton_Click(object sender, RoutedEventArgs e)
        {
            // Implement the logic for fetching frontend logs
            _logger.Information("Fetching frontend logs...");
        }

        private void StopFrontendLogsButton_Click(object sender, RoutedEventArgs e)
        {
            // Implement the logic for stopping frontend logs fetch
            _logger.Information("Stopping frontend logs fetch...");
        }
    }

    public class ConnectionStateTracker
    {
        private readonly object _lock = new();
        private readonly Dictionary<string, ConnectionState> _connectionStates = new();
        private readonly Timer _networkStatsTimer;
        private readonly ILogger _logger;
        private readonly TimeSpan _stateTimeout = TimeSpan.FromMinutes(5);
        private readonly Dictionary<string, int> _errorThresholds = new()
        {
            { "Timeout", 3 },
            { "AuthenticationFailed", 2 },
            { "NetworkUnavailable", 5 },
            { "DnsResolutionFailed", 3 },
            { "PortBlocked", 2 },
            { "ServerUnavailable", 3 }
        };

        public ConnectionStateTracker(ILogger logger)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            _logger = logger;
            _networkStatsTimer = new Timer(UpdateNetworkStats, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        }

        private async void UpdateNetworkStats(object? state)
        {
            try
            {
                await Task.Run(() =>
                {
                    lock (_lock)
                    {
                        foreach (var connection in _connectionStates.Values)
                        {
                            if (connection.CurrentState == ConnectionStateType.Connected)
                            {
                                _ = UpdateConnectionNetworkStats(connection);
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating network stats");
            }
        }

        private async Task UpdateConnectionNetworkStats(ConnectionState state)
        {
            try
            {
                if (string.IsNullOrEmpty(state.ServerUrl))
                {
                    _logger.LogWarning("Server URL is null or empty");
                    return;
                }

                var uri = new Uri(state.ServerUrl);
                var port = uri.Port;

                var diagnostics = await PerformNetworkDiagnosticsAsync(state.ServerUrl, port).ConfigureAwait(false);
                state.NetworkStats = new Models.NetworkStats
                {
                    IsDnsAvailable = diagnostics.IsDnsAvailable,
                    IsPortAccessible = diagnostics.IsPortAccessible,
                    Latency = diagnostics.Latency,
                    NetworkType = diagnostics.NetworkType,
                    NetworkSpeed = diagnostics.NetworkSpeed,
                    Jitter = diagnostics.Diagnostics.ContainsKey("Jitter") ? Convert.ToDouble(diagnostics.Diagnostics["Jitter"]) : 0,
                    Bandwidth = diagnostics.Diagnostics.ContainsKey("Bandwidth") ? Convert.ToDouble(diagnostics.Diagnostics["Bandwidth"]) : 0,
                    Quality = diagnostics.NetworkQuality,
                    PingSuccess = diagnostics.PingSuccess,
                    PingTime = diagnostics.PingTime
                };

                // Add diagnostics with proper type conversion
                foreach (var kvp in diagnostics.Diagnostics)
                {
                    if (kvp.Value != null)
                    {
                        switch (kvp.Key)
                        {
                            case "Latency" when kvp.Value is double latency:
                                state.Diagnostics[kvp.Key] = latency;
                                break;
                            case "Port" when kvp.Value is int portValue:
                                state.Diagnostics[kvp.Key] = portValue;
                                break;
                            default:
                                state.Diagnostics[kvp.Key] = kvp.Value.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating network stats for {ServerUrl}", state.ServerUrl);
            }
        }

        public void UpdateState(string connectionId, ConnectionState newState)
        {
            lock (_lock)
            {
                if (!_connectionStates.ContainsKey(connectionId))
                {
                    _connectionStates[connectionId] = new ConnectionState
                    {
                        ServerUrl = connectionId,
                        CurrentState = ConnectionStateType.Disconnected,
                        LastUpdate = DateTime.UtcNow,
                        Diagnostics = new Dictionary<string, object>(),
                        ConnectionHistory = new List<ConnectionAttempt>(),
                        ErrorCounts = new Dictionary<string, int>()
                    };
                }

                var state = _connectionStates[connectionId];
                var previousState = state.CurrentState;

                // Update basic state information
                state.LastUpdate = DateTime.UtcNow;
                state.CurrentState = newState.CurrentState;
                state.LastError = newState.LastError;
                state.LastErrorTime = DateTime.UtcNow;

                // Track connection attempts
                if (newState.CurrentState == ConnectionStateType.Connecting)
                {
                    state.ConnectionHistory.Add(new ConnectionAttempt
                    {
                        StartTime = DateTime.UtcNow,
                        Diagnostics = new Dictionary<string, object>(newState.Diagnostics)
                    });
                }
                else if (newState.CurrentState == ConnectionStateType.Connected ||
                         newState.CurrentState == ConnectionStateType.Error)
                {
                    var lastAttempt = state.ConnectionHistory.LastOrDefault();
                    if (lastAttempt != null)
                    {
                        lastAttempt.EndTime = DateTime.UtcNow;
                        lastAttempt.Success = newState.CurrentState == ConnectionStateType.Connected;
                        lastAttempt.ErrorMessage = newState.LastError;
                    }
                }

                // Update error tracking
                if (!string.IsNullOrEmpty(newState.LastError))
                {
                    var errorType = DetermineErrorType(newState.LastError);
                    if (!state.ErrorCounts.TryGetValue(errorType, out var errorCount))
                    {
                        errorCount = 0;
                    }
                    state.ErrorCounts[errorType] = errorCount + 1;

                    // Check if we've exceeded error thresholds
                    if (_errorThresholds.TryGetValue(errorType, out var threshold) &&
                        state.ErrorCounts[errorType] >= threshold)
                    {

                        _logger.LogWarning("Error threshold exceeded for {ErrorType} on connection {ConnectionId}",
                            errorType, connectionId);
                        HandleErrorThresholdExceeded(connectionId, errorType);
                    }
                }

                // Update connection duration
                if (newState.CurrentState == ConnectionStateType.Connected)
                {
                    if (state.ConnectionStartTime == default)
                    {
                        state.ConnectionStartTime = DateTime.UtcNow;
                    }
                    state.ConnectionDuration = DateTime.UtcNow - state.ConnectionStartTime;
                }
                else if (newState.CurrentState == ConnectionStateType.Disconnected)
                {
                    if (state.ConnectionStartTime != default)
                    {
                        state.LastSuccessfulConnectionDuration = DateTime.UtcNow - state.ConnectionStartTime;
                        state.LastSuccessfulConnectionTime = DateTime.UtcNow;
                    }
                }

                // Update reconnection tracking
                if (newState.CurrentState == ConnectionStateType.Reconnecting)
                {
                    state.IsReconnecting = true;
                    state.ReconnectionAttempts++;
                }
                else if (newState.CurrentState == ConnectionStateType.Connected)
                {
                    state.IsReconnecting = false;
                    state.ReconnectionAttempts = 0;
                }

                // Log state change
                _logger.LogDebug("Connection state updated for {ConnectionId}:\n" +
                    "1. State Change:\n" +
                    "   - Previous: {PreviousState}\n" +
                    "   - Current: {CurrentState}\n" +
                    "   - Duration: {Duration}\n" +
                    "2. Error Tracking:\n" +
                    "   - Last Error: {LastError}\n" +
                    "   - Error Counts: {ErrorCounts}\n" +
                    "3. Connection History:\n" +
                    "   - Total Attempts: {TotalAttempts}\n" +
                    "   - Successful: {SuccessfulAttempts}\n" +
                    "   - Failed: {FailedAttempts}\n" +
                    "4. Network Stats:\n" +
                    "   - Latency: {Latency}ms\n" +
                    "   - Packet Loss: {PacketLoss}%\n" +
                    "   - Bandwidth: {Bandwidth}MB/s",
                    connectionId,
                    previousState,
                    state.CurrentState,
                    state.ConnectionDuration,
                    state.LastError,
                    string.Join(", ", state.ErrorCounts.Select(e => $"{e.Key}: {e.Value}")),
                    state.ConnectionHistory.Count,
                    state.ConnectionHistory.Count(a => a.Success),
                    state.ConnectionHistory.Count(a => !a.Success),
                    state.NetworkStats.Latency,
                    state.NetworkStats.PacketLoss,
                    state.NetworkStats.Bandwidth);
            }
        }

        private string DetermineErrorType(string errorMessage)
        {
            if (errorMessage.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                return "Timeout";
            if (errorMessage.Contains("authentication", StringComparison.OrdinalIgnoreCase))
                return "AuthenticationFailed";
            if (errorMessage.Contains("network", StringComparison.OrdinalIgnoreCase))
                return "NetworkUnavailable";
            if (errorMessage.Contains("dns", StringComparison.OrdinalIgnoreCase))
                return "DnsResolutionFailed";
            if (errorMessage.Contains("port", StringComparison.OrdinalIgnoreCase))
                return "PortBlocked";
            if (errorMessage.Contains("server", StringComparison.OrdinalIgnoreCase))
                return "ServerUnavailable";
            return "Unknown";
        }

        private void HandleErrorThresholdExceeded(string connectionId, string errorType)
        {
            var state = _connectionStates[connectionId];

            // Implement error threshold handling logic
            switch (errorType)
            {
                case "Timeout":
                    state.CurrentState = ConnectionStateType.Timeout;
                    break;
                case "AuthenticationFailed":
                    state.CurrentState = ConnectionStateType.AuthenticationFailed;
                    break;
                case "NetworkUnavailable":
                    state.CurrentState = ConnectionStateType.NetworkUnavailable;
                    break;
                case "DnsResolutionFailed":
                    state.CurrentState = ConnectionStateType.DnsResolutionFailed;
                    break;
                case "PortBlocked":
                    state.CurrentState = ConnectionStateType.PortBlocked;
                    break;
                case "ServerUnavailable":
                    state.CurrentState = ConnectionStateType.ServerUnavailable;
                    break;
            }

            _logger.LogWarning("Error threshold exceeded for {ErrorType} on connection {ConnectionId}. " +
                "Current state: {CurrentState}, Error count: {ErrorCount}",
                errorType, connectionId, state.CurrentState, state.ErrorCounts[errorType]);
        }

        public ConnectionState? GetState(string connectionId)
        {
            lock (_lock)
            {
                if (_connectionStates.TryGetValue(connectionId, out var state))
                {
                    if (DateTime.UtcNow - state.LastUpdate > _stateTimeout)
                    {
                        _connectionStates.Remove(connectionId);
                        return null;
                    }
                    return state;
                }
                return null;
            }
        }

        public void ClearState(string connectionId)
        {
            lock (_lock)
            {
                if (_connectionStates.Remove(connectionId))
                {
                    _logger.LogDebug("Connection state cleared for {ConnectionId}", connectionId);
                }
            }
        }

        public void Dispose()
        {
            _networkStatsTimer?.Dispose();
        }

        public IReadOnlyCollection<ConnectionState> GetConnectionStates()
        {
            lock (_lock)
            {
                return _connectionStates.Values.ToList();
            }
        }
    }

    public class ConnectionState
    {
        public ConnectionStateType CurrentState { get; set; }
        public DateTime LastUpdate { get; set; }
        public int FailureCount { get; set; }
        public string LastError { get; set; } = string.Empty;
        public DateTime LastErrorTime { get; set; }
        public Dictionary<string, object> Diagnostics { get; set; } = new();
        public Models.NetworkStats NetworkStats { get; set; } = new();
        public TimeSpan ConnectionDuration { get; set; }
        public DateTime ConnectionStartTime { get; set; }
        public List<ConnectionAttempt> ConnectionHistory { get; set; } = new();
        public Dictionary<string, int> ErrorCounts { get; set; } = new();
        public bool IsReconnecting { get; set; }
        public int ReconnectionAttempts { get; set; }
        public DateTime? LastSuccessfulConnectionTime { get; set; }
        public TimeSpan LastSuccessfulConnectionDuration { get; set; }
        public string? ServerUrl { get; set; }

        public ConnectionState()
        {
            ServerUrl = string.Empty;
            Diagnostics = new Dictionary<string, object>();
            ConnectionHistory = new List<ConnectionAttempt>();
            ErrorCounts = new Dictionary<string, int>();
            NetworkStats = new Models.NetworkStats();
        }
    }

    public class ConnectionAttempt
    {
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public TimeSpan Duration => EndTime.HasValue ? EndTime.Value - StartTime : TimeSpan.Zero;
        public Dictionary<string, object> Diagnostics { get; set; } = new();
    }

    public enum ConnectionStateType
    {
        Disconnected,
        Connecting,
        Connected,
        Error,
        Timeout,
        AuthenticationFailed,
        NetworkUnavailable,
        DnsResolutionFailed,
        PortBlocked,
        ServerUnavailable,
        Reconnecting,
        ConnectionLost,
        ProtocolError,
        RateLimited,
        FirewallBlocked,
        ProxyError,
        SslError,
        CertificateError,
        InvalidResponse
    }
}
