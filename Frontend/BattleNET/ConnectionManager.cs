using Serilog;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace BattleNET
{
    public class ConnectionManager : IDisposable
    {
        private readonly Serilog.ILogger _logger;
        private readonly SemaphoreSlim _connectionLock;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly BattlEyeNetworkDiagnostics _networkDiagnostics;
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly EventSystem _eventSystem;
        private bool _isDisposed;

        private Socket? _socket;
        private ConnectionStatus _status;
        private DateTime _lastConnectionAttempt;
        private int _reconnectionAttempts;
        private const int MAX_RECONNECTION_ATTEMPTS = 5;
        private const int INITIAL_RECONNECTION_DELAY_MS = 1000;
        private const int MAX_RECONNECTION_DELAY_MS = 30000;

        public ConnectionManager(
            Serilog.ILogger logger,
            BattlEyeNetworkDiagnostics networkDiagnostics,
            PerformanceMonitor performanceMonitor,
            EventSystem eventSystem)
        {
            _logger = logger ?? Log.ForContext<ConnectionManager>();
            _connectionLock = new SemaphoreSlim(1, 1);
            _cancellationTokenSource = new CancellationTokenSource();
            _networkDiagnostics = networkDiagnostics ?? throw new ArgumentNullException(nameof(networkDiagnostics));
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
            _eventSystem = eventSystem ?? throw new ArgumentNullException(nameof(eventSystem));
            _status = ConnectionStatus.Disconnected;
        }

        public async Task<BattlEyeConnectionResult> ConnectAsync(
            IPAddress host,
            int port,
            string password,
            CancellationToken cancellationToken = default)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Password cannot be null or empty", nameof(password));
            }

            if (_status == ConnectionStatus.Connected)
            {
                _logger.Warning("Already connected to server");
                return BattlEyeConnectionResult.CreateSuccess();
            }

            try
            {
                await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    if (_status == ConnectionStatus.Connected)
                    {
                        return BattlEyeConnectionResult.CreateSuccess();
                    }

                    _status = ConnectionStatus.Connecting;
                    _logger.Information("Connecting to server {Host}:{Port}", host, port);

                    // Perform network diagnostics
                    var diagnostics = await _networkDiagnostics.GenerateReportAsync().ConfigureAwait(false);
                    if (diagnostics.OverallHealth == "Poor")
                    {
                        var errorMessage = "Network connectivity issues detected:\n" +
                            $"1. Network Health: {diagnostics.OverallHealth}\n" +
                            $"2. Issues: {string.Join(", ", diagnostics.Issues)}\n" +
                            $"3. Recommendations: {string.Join(", ", diagnostics.Recommendations)}";

                        _logger.Warning(errorMessage);
                        return BattlEyeConnectionResult.CreateFailure(
                            BattlEyeConnectionResultStatus.NetworkError,
                            errorMessage);
                    }

                    // Ensure we're using IPv4
                    if (host.AddressFamily != AddressFamily.InterNetwork)
                    {
                        _logger.Warning("Non-IPv4 address detected, attempting to convert to IPv4");
                        host = host.MapToIPv4();
                        _logger.Information("Converted to IPv4 address: {IpAddress}", host);
                    }

                    // Create and configure socket
                    _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    ConfigureSocket(_socket);

                    // Connect to server
                    await _socket.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
                    _logger.Debug("Socket connected successfully");

                    // Update connection state
                    _status = ConnectionStatus.Connected;
                    _lastConnectionAttempt = DateTime.UtcNow;
                    _reconnectionAttempts = 0;

                    // Update performance metrics
                    _performanceMonitor.TrackActiveConnections(1);
                    _performanceMonitor.TrackNetworkLatency(0);

                    // Raise connection event
                    await _eventSystem.RaiseEventAsync(BattlEyeEventType.Connected, new BattlEyeEventArgs()).ConfigureAwait(false);

                    _logger.Information("Connection established successfully");
                    return BattlEyeConnectionResult.CreateSuccess();
                }
                finally
                {
                    _connectionLock.Release();
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("Connection attempt timed out");
                return BattlEyeConnectionResult.CreateFailure(
                    BattlEyeConnectionResultStatus.ConnectionTimeout,
                    "Connection attempt timed out");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during connection attempt");
                return BattlEyeConnectionResult.CreateFailure(
                    BattlEyeConnectionResultStatus.ConnectionFailed,
                    $"Connection failed: {ex.Message}");
            }
        }

        public async Task DisconnectAsync()
        {
            if (_status == ConnectionStatus.Disconnected)
            {
                return;
            }

            try
            {
                await _connectionLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    _status = ConnectionStatus.Disconnecting;
                    _logger.Information("Disconnecting from server");

                    if (_socket != null && _socket.Connected)
                    {
                        await Task.Run(() =>
                        {
                            _socket.Shutdown(SocketShutdown.Both);
                            _socket.Close();
                            _logger.Debug("Socket closed successfully");
                        }).ConfigureAwait(false);
                    }

                    _socket = null;
                    _status = ConnectionStatus.Disconnected;

                    // Update performance metrics
                    _performanceMonitor.TrackActiveConnections(0);

                    // Raise disconnect event
                    await _eventSystem.RaiseEventAsync(BattlEyeEventType.Disconnected, new BattlEyeEventArgs()).ConfigureAwait(false);

                    _logger.Information("Disconnected successfully");
                }
                finally
                {
                    _connectionLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during disconnection");
                throw;
            }
        }

        public async Task<bool> ReconnectAsync(CancellationToken cancellationToken = default)
        {
            if (_reconnectionAttempts >= MAX_RECONNECTION_ATTEMPTS)
            {
                _logger.Warning("Maximum reconnection attempts reached");
                return false;
            }

            var delay = CalculateReconnectionDelay();
            _logger.Information("Attempting to reconnect in {Delay}ms (attempt {Attempt}/{Max})",
                delay, _reconnectionAttempts + 1, MAX_RECONNECTION_ATTEMPTS);

            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            _reconnectionAttempts++;

            return true;
        }

        private int CalculateReconnectionDelay()
        {
            var delay = INITIAL_RECONNECTION_DELAY_MS * (int)Math.Pow(2, _reconnectionAttempts);
            return Math.Min(delay, MAX_RECONNECTION_DELAY_MS);
        }

        private void ConfigureSocket(Socket socket)
        {
            socket.NoDelay = true;
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoDelay, true);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            try
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _connectionLock.Dispose();
                _socket?.Dispose();
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

        public ConnectionStatus Status => _status;
        public Socket? Socket => _socket;
        public bool IsConnected => _status == ConnectionStatus.Connected;
        public DateTime LastConnectionAttempt => _lastConnectionAttempt;
        public int ReconnectionAttempts => _reconnectionAttempts;
    }
}