using ArmaReforgerServerMonitor.Frontend.Configuration;
using ArmaReforgerServerMonitor.Frontend.Models;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// Implementation of the demo service that generates and manages sample data
    /// for testing and demonstration purposes.
    /// </summary>
    /// <remarks>
    /// This service provides realistic sample data that simulates:
    /// - Server performance metrics
    /// - Player activity
    /// - System resources
    /// - Network conditions
    /// - Error scenarios
    /// 
    /// The data generation is customizable through parameters and
    /// supports various patterns and trends for testing purposes.
    /// </remarks>
    public class DemoService : IDisposable, IDemoService
    {
        private readonly Serilog.ILogger _logger;
        private readonly AppSettings _settings;
        private static readonly RandomNumberGenerator _random = RandomNumberGenerator.Create();
        private static readonly RandomNumberGenerator _secureRandom = RandomNumberGenerator.Create();
        private bool _isDemoMode;
        private Task? _demoTask;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private DemoParameters _parameters = new DemoParameters();
        private DateTime _startTime = DateTime.UtcNow;
        private double _trendPhase = 0;
        private readonly ConcurrentDictionary<string, object> _demoData;
        private bool _isDisposed;
        private static readonly List<string> _logLevels = new List<string> { "DEBUG", "INFO", "WARNING", "ERROR", "FATAL" };
        private static readonly List<string> _modNames = new List<string> { "ACE", "RHS", "CUP", "TFAR", "ACE3" };
        private static readonly List<string> _playerNames = new List<string> { "Player1", "Player2", "Player3", "Player4", "Player5" };
        private static readonly List<string> _serverNames = new List<string> { "Server1", "Server2", "Server3", "Server4", "Server5" };
        private static readonly List<string> _errorMessages = new List<string> { "Connection failed", "Server timeout", "Invalid response" };
        private static readonly List<string> _warningMessages = new List<string> { "High latency", "Low FPS", "Memory warning" };
        private static readonly List<string> _infoMessages = new List<string> { "Server started", "Player joined", "Mission loaded" };
        private static readonly List<string> _debugMessages = new List<string> { "Initializing", "Processing", "Updating" };
        private readonly List<string> _dnsServers = new List<string>();

        /// <summary>
        /// Event that fires when demo metrics are generated.
        /// </summary>
        public event EventHandler<DemoMetricsEventArgs>? MetricsGenerated;

        /// <summary>
        /// Event that fires when demo log entries are generated.
        /// </summary>
        public event EventHandler<DemoLogEventArgs>? LogGenerated;

        /// <summary>
        /// Event that fires when demo parameters are changed.
        /// </summary>
        public event EventHandler<DemoParametersChangedEventArgs>? ParametersChanged;

        /// <summary>
        /// Event that fires when demo metrics are updated.
        /// </summary>
        public event EventHandler<OSDataDTO>? MetricsUpdated;

        /// <summary>
        /// Gets whether the demo mode is enabled.
        /// </summary>
        public bool IsDemoMode => _isDemoMode;

        /// <summary>
        /// Initializes a new instance of the DemoService class.
        /// </summary>
        /// <param name="logger">Logger for demo events</param>
        /// <param name="settings">Application settings</param>
        /// <remarks>
        /// The constructor initializes:
        /// - Random number generator
        /// - Trend tracking
        /// </remarks>
        public DemoService(Serilog.ILogger logger, IOptions<AppSettings> settings)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _demoData = new ConcurrentDictionary<string, object>();
        }

        /// <summary>
        /// Sets the demo mode.
        /// </summary>
        /// <param name="enabled">True to enable demo mode, false to disable</param>
        public void SetDemoMode(bool enabled)
        {
            if (_isDemoMode != enabled)
            {
                _isDemoMode = enabled;
                if (enabled)
                {
                    Start();
                }
                else
                {
                    Stop();
                }
                _startTime = DateTime.UtcNow;
                _trendPhase = 0;
                _logger.Information("Demo mode {Status}", enabled ? "enabled" : "disabled");
            }
        }

        /// <summary>
        /// Starts the demo service.
        /// </summary>
        public void Start()
        {
            if (_isDemoMode)
                return;

            _isDemoMode = true;
            _demoTask = Task.Run(GenerateMetricsAsync);
            _logger.Information("Demo service started");
        }

        /// <summary>
        /// Stops the demo service.
        /// </summary>
        public void Stop()
        {
            if (!_isDemoMode)
                return;

            _isDemoMode = false;
            _cancellationTokenSource.Cancel();
            _demoTask?.Wait();
            _logger.Information("Demo service stopped");
        }

        /// <summary>
        /// Generates demo metrics asynchronously.
        /// </summary>
        private async Task GenerateMetricsAsync()
        {
            while (_isDemoMode)
            {
                try
                {
                    var metrics = GenerateMetrics();
                    OnMetricsGenerated(metrics);
                    GenerateLogEntry();
                    await Task.Delay(_settings.DemoSettings.DefaultUpdateFrequencyMs, _cancellationTokenSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    _logger.Information("Metrics generation task was canceled.");
                    break;
                }
                catch (InvalidOperationException ex)
                {
                    _logger.Warning(ex, "Invalid operation during metrics generation - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                        ex.GetType().Name, ex.Message, ex.StackTrace);
                }
                catch (ArgumentException ex)
                {
                    _logger.Warning(ex, "Argument error during metrics generation - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                        ex.GetType().Name, ex.Message, ex.StackTrace);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unexpected error generating demo metrics - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                        ex.GetType().Name, ex.Message, ex.StackTrace);
                }
            }
        }

        /// <summary>
        /// Generates a demo metrics object.
        /// </summary>
        /// <returns>Generated demo metrics</returns>
        private DemoMetrics GenerateMetrics()
        {
            var settings = _settings.DemoSettings;
            return new DemoMetrics
            {
                CpuUsage = GenerateValue(settings.DefaultCpuRange.Min, settings.DefaultCpuRange.Max),
                MemoryUsage = GenerateValue(settings.DefaultMemoryRange.Min, settings.DefaultMemoryRange.Max),
                FPS = GenerateValue(settings.DefaultFpsRange.Min, settings.DefaultFpsRange.Max),
                PlayerCount = GetRandomInt(settings.DefaultPlayerCount.Min, settings.DefaultPlayerCount.Max + 1)
            };
        }

        /// <summary>
        /// Generates a random value within a specified range.
        /// </summary>
        /// <param name="min">Minimum value</param>
        /// <param name="max">Maximum value</param>
        /// <returns>Generated random value</returns>
        private static double GenerateValue(double min, double max)
        {
            return min + (GetRandomDouble() * (max - min));
        }

        /// <summary>
        /// Raises the MetricsGenerated event.
        /// </summary>
        /// <param name="metrics">Generated metrics</param>
        private void OnMetricsGenerated(DemoMetrics metrics)
        {
            MetricsGenerated?.Invoke(this, new DemoMetricsEventArgs(metrics));
        }

        /// <summary>
        /// Generates a demo log entry.
        /// </summary>
        private void GenerateLogEntry()
        {
            var logLevel = GetSecureRandomInt(0, 100) switch
            {
                < 60 => LogEventLevel.Information,
                < 80 => LogEventLevel.Warning,
                < 95 => LogEventLevel.Error,
                _ => LogEventLevel.Fatal
            };

            var message = logLevel switch
            {
                LogEventLevel.Information => GetRandomInfoMessage(),
                LogEventLevel.Warning => GetRandomWarningMessage(),
                LogEventLevel.Error => GetRandomErrorMessage(),
                LogEventLevel.Fatal => GetRandomCriticalMessage(),
                _ => "Unknown log message"
            };

            _logger.Write(logLevel, message);

            // Ensure all required parameters are provided for LogEntry instantiation
            LogGenerated?.Invoke(this, new DemoLogEventArgs(message, ConvertToModelLogLevel(logLevel)));
        }

        private static Serilog.Events.LogEventLevel ConvertToModelLogLevel(LogEventLevel logEventLevel)
        {
            return logEventLevel;
        }

        private static string GetRandomInfoMessage()
        {
            var messages = new[]
            {
                "Server running normally",
                "Player connected successfully",
                "Map loaded successfully",
                "Mission started",
                "Server performance optimal"
            };
            return messages[GetRandomInt(0, messages.Length)];
        }

        private static string GetRandomWarningMessage()
        {
            var messages = new[]
            {
                "High memory usage detected",
                "Network latency increasing",
                "Server performance degrading",
                "Player timeout warning",
                "Low disk space warning"
            };
            return messages[GetRandomInt(0, messages.Length)];
        }

        private static string GetRandomErrorMessage()
        {
            return _errorMessages[GetRandomInt(0, _errorMessages.Count)];
        }

        private static string GetRandomCriticalMessage()
        {
            var messages = new[]
            {
                "Server crash detected",
                "Critical system failure",
                "Out of memory error",
                "Fatal error in mission script",
                "Emergency shutdown initiated"
            };
            return messages[GetRandomInt(0, messages.Length)];
        }

        /// <summary>
        /// Disposes the DemoService object.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
            {
                return;
            }

            try
            {
                Stop();
                _cancellationTokenSource.Dispose();
                _demoData.Clear();
                _logger.Information("DemoService resources disposed.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during disposal");
                throw;
            }
            finally
            {
                _isDisposed = true;
            }
        }

        public async Task<OSDataDTO> GetDemoMetricsAsync(DemoParameters? parameters = null)
        {
            parameters ??= _parameters;

            var metrics = new OSDataDTO
            {
                CpuUsage = GenerateMetricValue(parameters.CpuRange),
                TotalMemory = 16L * 1024 * 1024 * 1024, // 16 GB
                UsedMemory = (ulong)(16L * 1024 * 1024 * 1024 * (GenerateMetricValue(parameters.MemoryRange) / 100)),
                Timestamp = DateTime.UtcNow
            };

            metrics.AvailableMemory = metrics.TotalMemory - metrics.UsedMemory;
            metrics.MemoryUsage = (double)metrics.UsedMemory / metrics.TotalMemory * 100;

            // Generate disk information
            metrics.Disks.Clear();
            metrics.Disks.Add(GenerateDiskInfo("C:", 500L * 1024 * 1024 * 1024)); // 500 GB
            metrics.Disks.Add(GenerateDiskInfo("D:", 1000L * 1024 * 1024 * 1024)); // 1 TB

            // Generate network statistics
            metrics.NetworkStats = new Models.NetworkStats
            {
                IsNetworkAvailable = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable(),
                LastUpdate = DateTime.UtcNow,
                BytesReceived = (long)GetRandomDouble() * 1024 * 1024 * 1024, // 0-1GB
                BytesSent = (long)GetRandomDouble() * 512 * 1024 * 1024, // 0-512MB
                DownloadSpeed = GetRandomDouble() * 10 * 1024 * 1024, // 0-10 MB/s
                UploadSpeed = GetRandomDouble() * 5 * 1024 * 1024, // 0-5 MB/s
                IsActive = true
            };

            metrics.Uptime = (DateTime.UtcNow - _startTime).TotalSeconds;

            if (parameters.SimulateLatency)
            {
                await Task.Delay(GetRandomInt(parameters.LatencyRange.Min, parameters.LatencyRange.Max)).ConfigureAwait(false);
            }

            return metrics;
        }

        public async Task<ServerStatus> GetDemoServerStatusAsync(DemoParameters? parameters = null)
        {
            parameters ??= _parameters;

            var status = new ServerStatus();
            status.IsRunning = true;
            status.Version = "1.0.0";
            status.ServerName = _serverNames[GetRandomInt(0, _serverNames.Count)];
            status.GameMode = "Conflict";
            status.CurrentMap = "Everon";
            status.MaxPlayers = 32;
            status.CurrentPlayers = GetRandomInt(parameters.PlayerCount.Min, parameters.PlayerCount.Max);
            status.StartTime = _startTime;
            status.LastUpdate = DateTime.UtcNow;

            // Generate player list
            status.Players = GeneratePlayerList(status.CurrentPlayers);

            // Generate system metrics
            var systemMetrics = await GetDemoMetricsAsync(parameters);
            status.Performance = new PerformanceMetrics
            {
                FPS = (float)GenerateMetricValue(parameters.FpsRange),
                CPUUsage = systemMetrics.CpuUsage,
                MemoryUsage = systemMetrics.MemoryUsage,
                NetworkLatency = GetRandomDouble() * 100, // 0-100ms latency
                PacketLoss = GetRandomDouble() * 2, // 0-2% packet loss
                TickRate = 60.0f, // Standard tick rate
                FrameTime = 16.67f // ~60 FPS frame time
            };

            // Generate server configuration
            status.Config = new ServerConfig
            {
                IsPublic = true,
                BattlEyeEnabled = true,
                VoiceEnabled = true,
                Region = "EU",
                Description = "Demo server for testing purposes",
                Mods = GenerateModList()
            };

            if (parameters.SimulateLatency)
            {
                await Task.Delay(GetRandomInt(parameters.LatencyRange.Min, parameters.LatencyRange.Max)).ConfigureAwait(false);
            }

            return status;
        }

        public async Task<List<ConsoleLog>> GetDemoConsoleLogsAsync(int count, DemoParameters? parameters = null)
        {
            parameters ??= _parameters;
            var logs = new List<ConsoleLog>();

            for (int i = 0; i < count; i++)
            {
                var log = GenerateConsoleLog(parameters);
                logs.Add(log);
            }

            if (parameters.SimulateLatency)
            {
                await Task.Delay(GetRandomInt(parameters.LatencyRange.Min, parameters.LatencyRange.Max)).ConfigureAwait(false);
            }

            return logs;
        }

        public void UpdateParameters(DemoParameters parameters)
        {
            _parameters = parameters;
            OnParametersChanged(parameters);
            _logger.Information("Demo parameters updated");
        }

        private double GenerateMetricValue((double Min, double Max) range)
        {
            if (!_parameters.GenerateTrends)
            {
                return GetRandomDouble() * (range.Max - range.Min) + range.Min;
            }

            // Generate trending data using sine waves with multiple frequencies
            var time = (DateTime.UtcNow - _startTime).TotalSeconds;

            // Primary trend cycle
            var primaryCycle = 2 * Math.PI * time / _parameters.TrendCycleDuration;
            var primaryTrend = Math.Sin(primaryCycle + _trendPhase);

            // Secondary trend cycle (faster)
            var secondaryCycle = 2 * Math.PI * time / (_parameters.TrendCycleDuration * 0.5);
            var secondaryTrend = Math.Sin(secondaryCycle + _trendPhase * 1.5) * 0.3;

            // Random noise with controlled variance
            var noise = (GetRandomDouble() - 0.5) * 0.2;

            // Combine trends and noise
            var trendValue = (primaryTrend + secondaryTrend + noise) / 1.5;

            // Map the combined value (-1 to 1) to the desired range
            var mid = (range.Max + range.Min) / 2;
            var amplitude = (range.Max - range.Min) / 2;
            var value = mid + trendValue * amplitude * 0.8; // Use 80% of range for trend

            // Add some controlled random variation
            value += (GetRandomDouble() - 0.5) * amplitude * 0.2; // 20% noise

            // Ensure value stays within bounds
            value = Math.Max(range.Min, Math.Min(range.Max, value));

            // Add some realistic spikes occasionally
            if (GetRandomDouble() < 0.01) // 1% chance of spike
            {
                var spikeDirection = GetRandomDouble() < 0.5 ? 1 : -1;
                var spikeMagnitude = GetRandomDouble() * amplitude * 0.3; // Up to 30% spike
                value += spikeDirection * spikeMagnitude;
                value = Math.Max(range.Min, Math.Min(range.Max, value));
            }

            return value;
        }

        private static DiskInfo GenerateDiskInfo(string name, ulong totalSpace)
        {
            var usagePercent = GetRandomDouble() * 0.6 + 0.2; // 20-80% usage
            var usedSpace = (ulong)(totalSpace * usagePercent);

            return new DiskInfo
            {
                Name = name,
                TotalSpace = totalSpace,
                UsedSpace = usedSpace,
                FreeSpace = totalSpace - usedSpace,
                UsagePercentage = usagePercent * 100
            };
        }

        private List<PlayerInfo> GeneratePlayerList(int count)
        {
            var players = new List<PlayerInfo>(count);
            var names = new[] { "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf", "Hotel", "India", "Juliet" };
            var teams = new[] { "BLUFOR", "OPFOR", "Independent" };

            for (int i = 0; i < count; i++)
            {
                players.Add(new PlayerInfo
                {
                    Name = $"{names[GetRandomInt(0, names.Length)]}_{GetRandomInt(100, 999)}",
                    Team = teams[GetRandomInt(0, teams.Length)],
                    Ping = GetRandomInt(20, 200),
                    IsAdmin = GetRandomDouble() < 0.1 // 10% chance of being admin
                });
            }

            return players;
        }

        private List<ModInfo> GenerateModList()
        {
            var count = GetRandomInt(3, 8);
            var selectedMods = new List<string>(count);
            foreach (var mod in _modNames.OrderBy(x => GetRandomInt(0, int.MaxValue)).Take(count))
            {
                selectedMods.Add(mod);
            }

            return selectedMods.Select(name => new ModInfo
            {
                Name = name,
                Version = "1.0.0",
                IsRequired = true
            }).ToList();
        }

        private static ConsoleLog GenerateConsoleLog(DemoParameters parameters)
        {
            var sources = new[] { "GameServer", "Network", "Mission", "PlayerManager" };
            var playerEvents = new[] { "connected", "disconnected", "killed", "respawned", "changed team" };
            var systemEvents = new[] { "initialized", "map loaded", "mission started", "backup created", "configuration updated" };

            var isError = GetRandomDouble() < parameters.ErrorProbability;
            var level = isError ? Models.LogLevel.Error :
                GetRandomDouble() < 0.2 ? Models.LogLevel.Warning :
                GetRandomDouble() < 0.3 ? Models.LogLevel.Debug :
                Models.LogLevel.Info;

            var source = sources[GetRandomInt(0, sources.Length)];
            string message;
            string? playerName = null;

            if (level == Models.LogLevel.Error)
            {
                message = $"Error: {_errorMessages[GetRandomInt(0, _errorMessages.Count)]}";
            }
            else if (GetRandomDouble() < 0.4)
            {
                playerName = $"Player_{GetRandomInt(1000, 9999)}";
                message = $"Player {playerName} {playerEvents[GetRandomInt(0, playerEvents.Length)]}";
            }
            else
            {
                message = $"System {systemEvents[GetRandomInt(0, systemEvents.Length)]}";
            }

            var log = new ConsoleLog(message, level)
            {
                Source = source,
                PlayerName = playerName,
                Timestamp = DateTime.UtcNow.AddSeconds(-GetRandomInt(0, 3600))
            };

            if (level == Models.LogLevel.Error)
            {
                log.StackTrace = "at GameServer.ProcessRequest()\nat Network.SendData()\nat System.IO.IOException";
            }

            log.AddProperty("EventId", GetRandomInt(1000, 9999).ToString());
            if (playerName != null)
            {
                log.AddProperty("PlayerId", Guid.NewGuid().ToString());
            }

            return log;
        }

        private void OnParametersChanged(DemoParameters parameters)
        {
            try
            {
                ParametersChanged?.Invoke(this, new DemoParametersChangedEventArgs(parameters));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error raising ParametersChanged event");
            }
        }

        public async Task RestartAsync(CancellationToken cancellationToken)
        {
            Stop();
            await Task.Delay(100, cancellationToken); // Give a small delay for cleanup
            Start();
        }

        private void OnMetricsUpdated(OSDataDTO metrics)
        {
            MetricsUpdated?.Invoke(this, metrics);
        }

        private void UpdateNetworkStats(NetworkStats stats)
        {
            if (stats == null)
                return;

            var pingTime = GetSecureRandomInt(20, 200);
            var jitter = NetworkQualityHelper.CalculateJitter(pingTime);
            var bandwidth = NetworkQualityHelper.GetNetworkBandwidth();
            var quality = NetworkQualityHelper.AssessNetworkQuality(pingTime, true);

            // Generate realistic network stats with some variation
            stats.IsNetworkAvailable = true;
            stats.IsDnsAvailable = true;
            stats.IsPortAccessible = true;
            stats.Latency = pingTime;
            stats.NetworkType = GetSecureRandomDouble() < 0.7 ? "Ethernet" : "Wireless";
            stats.NetworkSpeed = GetSecureRandomDouble() < 0.6 ? "1000 Mbps" : "100 Mbps";
            stats.Jitter = jitter;
            stats.Bandwidth = bandwidth;
            stats.Quality = quality;
            stats.PingSuccess = true;
            stats.PingTime = pingTime;

            // Add some realistic packet loss
            stats.PacketLoss = GetRandomDouble() * 0.5; // 0-0.5% packet loss

            // Generate realistic traffic stats
            stats.BytesReceived = (long)(GetRandomDouble() * 1024 * 1024 * 1024); // 0-1GB
            stats.BytesSent = (long)(GetRandomDouble() * 512 * 1024 * 1024); // 0-512MB
            stats.DownloadSpeed = GetRandomDouble() * 10 * 1024 * 1024; // 0-10 MB/s
            stats.UploadSpeed = GetRandomDouble() * 5 * 1024 * 1024; // 0-5 MB/s

            // Add some realistic connection stats
            stats.ActiveConnections = GetRandomInt(1, 50);
            stats.ConnectionCount = GetRandomInt(100, 1000);

            // Add DNS information
            var dnsServers = new List<string> { "8.8.8.8", "8.8.4.4" };
            stats.GetType().GetField("_dnsServers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(stats, dnsServers);
            stats.DnsSuffix = "local";

            // Add diagnostics
            stats.Diagnostics = new Dictionary<string, object>
            {
                { "LastUpdate", DateTime.UtcNow },
                { "NetworkQuality", quality },
                { "SignalStrength", GetRandomInt(70, 100) },
                { "MTU", 1500 },
                { "Protocol", "IPv4" }
            };
        }

        public static string GenerateModListString()
        {
            var count = GetRandomInt(3, 8);
            var modNames = new List<string> { "ACE", "ACRE", "RHS", "CUP", "TFAR", "ACE3", "ACRE2", "RHSUSAF", "RHSGREF", "RHSAFRF" };
            var selectedMods = modNames.OrderBy(x => GetRandomInt(0, int.MaxValue)).Take(count).ToList();
            return string.Join(", ", selectedMods);
        }

        public static void UpdateNetworkStatsValue(NetworkStats stats)
        {
            stats.BytesReceived = GetRandomInt(1000, 1000000);
            stats.BytesSent = GetRandomInt(1000, 1000000);
            stats.PacketsReceived = GetRandomInt(100, 10000);
            stats.PacketsSent = GetRandomInt(100, 10000);
            stats.Latency = GetRandomInt(10, 200);
            stats.PacketLoss = GetRandomInt(0, 5) / 100.0;
            stats.Bandwidth = GetRandomInt(1, 100);
        }

        public async Task<DemoData> GenerateDemoDataAsync()
        {
            try
            {
                var data = new DemoData
                {
                    Timestamp = DateTime.UtcNow,
                    ServerName = _serverNames[GetRandomInt(0, _serverNames.Count)],
                    PlayerCount = GetRandomInt(0, 64),
                    MaxPlayers = 64,
                    ModList = GenerateModListString(),
                    NetworkStats = new NetworkStats(),
                    Players = GeneratePlayers(),
                    Logs = await GenerateLogsAsync()
                };

                UpdateNetworkStats(data.NetworkStats);
                return data;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error generating demo data");
                throw;
            }
        }

        private List<Player> GeneratePlayers()
        {
            var players = new List<Player>();
            var count = GetRandomInt(0, 10);

            for (int i = 0; i < count; i++)
            {
                players.Add(new Player
                {
                    Name = _playerNames[GetRandomInt(0, _playerNames.Count)],
                    Ping = GetRandomInt(10, 200),
                    Score = GetRandomInt(0, 1000),
                    Team = GetRandomInt(0, 4),
                    IsAdmin = GetRandomInt(0, 100) < 10
                });
            }

            return players;
        }

        private static Task<List<LogEntry>> GenerateLogsAsync()
        {
            var logs = new List<LogEntry>();
            var count = GetRandomInt(5, 20);

            for (int i = 0; i < count; i++)
            {
                var level = _logLevels[GetRandomInt(0, _logLevels.Count)];
                var message = GetRandomMessage(level);

                var timestamp = DateTime.UtcNow.AddSeconds(-GetRandomInt(0, 3600));
                logs.Add(new LogEntry(timestamp, level, message, "DemoService"));
            }

            return Task.FromResult(logs.OrderByDescending(x => x.Timestamp).ToList());
        }

        private static string GetRandomMessage(string level)
        {
            var messages = level.ToUpperInvariant() switch
            {
                "ERROR" => _errorMessages,
                "WARNING" => _warningMessages,
                "INFO" => _infoMessages,
                "DEBUG" => _debugMessages,
                _ => _infoMessages
            };
            return messages[GetRandomInt(0, messages.Count)];
        }

        private static int GetSecureRandomInt(int min, int max)
        {
            byte[] randomNumber = new byte[4];
            _secureRandom.GetBytes(randomNumber);
            int result = BitConverter.ToInt32(randomNumber, 0);
            return new Random(result).Next(min, max);
        }

        private static double GetSecureRandomDouble()
        {
            byte[] randomNumber = new byte[8];
            _secureRandom.GetBytes(randomNumber);
            ulong result = BitConverter.ToUInt64(randomNumber, 0) >> 11;
            return result / (double)(1UL << 53);
        }

        public void StopService()
        {
            if (_isDemoMode)
            {
                _cancellationTokenSource.Cancel();
                _isDemoMode = false;
                _logger.Information("Demo service stopped.");
            }
        }

        private static int GetRandomInt(int min, int max)
        {
            return new Random().Next(min, max);
        }

        private static double GetRandomDouble()
        {
            return new Random().NextDouble();
        }
    }
}