/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 * BattleNET v1.3.4 - BattlEye Library and Client            *
 *                                                         *
 *  Copyright (C) 2018 by it's authors.                    *
 *  Some rights reserved. See license.txt, authors.txt.    *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

using Serilog;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BattleNET
{
    public class BattlEyeEventArgs : EventArgs
    {
        public string Message { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public PacketProcessor.ProcessedPacket Packet { get; set; } = new PacketProcessor.ProcessedPacket(BattlEyePacketType.Unknown, 0, new byte[0], string.Empty, false);
        public Exception Error { get; set; } = new Exception();
    }

    public enum BattlEyeEventType
    {
        Connected,
        Disconnected,
        PacketReceived,
        PacketSent,
        CommandExecuted,
        CommandFailed,
        ConnectionFailed,
        Error
    }

    public enum ConnectionStatus
    {
        /// <summary>
        /// Client is not connected.
        /// </summary>
        Disconnected,

        /// <summary>
        /// Client is in the process of connecting.
        /// </summary>
        Connecting,

        /// <summary>
        /// Client is connected.
        /// </summary>
        Connected,

        /// <summary>
        /// Client is in the process of disconnecting.
        /// </summary>
        Disconnecting
    }

    /// <summary>
    /// Provides a client for connecting to and communicating with a BattlEye server.
    /// This class handles the low-level communication protocol and provides high-level methods
    /// for interacting with the server.
    /// </summary>
    public class BattlEyeClient : IDisposable, IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly PacketProcessor _packetProcessor;
        private readonly ConnectionManager _connectionManager;
        private readonly CommandQueue _commandQueue;
        private readonly MetricsCollector _metricsCollector;
        private readonly BattlEyeNetworkDiagnostics _networkDiagnostics;
        private readonly SecurityManager _securityManager;
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly EventSystem _eventSystem;
        private readonly string _host;
        private readonly int _port;
        private readonly string _password;
        private readonly Socket _socket;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly ConcurrentDictionary<int, TaskCompletionSource<PacketProcessor.ProcessedPacket>> _pendingPackets;
        private ConnectionStatus _connectionStatus;
        private bool _isDisposed;
        private bool _isDisposing;
        private Task _receiveTask = Task.CompletedTask;
        private Task _reconnectTask = Task.CompletedTask;
        private Task _heartbeatTask = Task.CompletedTask;
        private const int MAX_RECONNECT_ATTEMPTS = 5;
        private const int RECONNECT_DELAY_MS = 1000;

        /// <summary>
        /// Gets the current connection status of the client.
        /// </summary>
        public ConnectionStatus Status => _connectionStatus;

        /// <summary>
        /// Gets whether the client is currently connected.
        /// </summary>
        public bool IsConnected => _connectionStatus == ConnectionStatus.Connected;

        // Add a property to track the last error
        public Exception? LastError { get; private set; }

        // Add events for message received and disconnect
        public event EventHandler<BattlEyeEventArgs>? OnMessageReceived;
        public event EventHandler<BattlEyeEventArgs>? OnDisconnect;

        public BattlEyeClient(string host, int port, string password)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _port = port;
            _password = password ?? throw new ArgumentNullException(nameof(password));
            _socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            _cancellationTokenSource = new CancellationTokenSource();
            _pendingPackets = new ConcurrentDictionary<int, TaskCompletionSource<PacketProcessor.ProcessedPacket>>();
            _logger = Log.ForContext<BattlEyeClient>();
            _packetProcessor = new PacketProcessor(_logger);
            _networkDiagnostics = new BattlEyeNetworkDiagnostics(_logger);
            _performanceMonitor = new PerformanceMonitor(_logger);
            _eventSystem = new EventSystem(_logger);
            _connectionManager = new ConnectionManager(_logger, _networkDiagnostics, _performanceMonitor, _eventSystem);
            _securityManager = new SecurityManager(_logger);
            _commandQueue = new CommandQueue(_logger, _performanceMonitor, _securityManager);
            _metricsCollector = new MetricsCollector(_logger, TimeSpan.FromSeconds(30), 100);
            _connectionStatus = ConnectionStatus.Disconnected;

            _logger.Information("Initialized BattlEyeClient with host: {Host}, port: {Port}, password length: {PasswordLength}",
                host, port, password.Length);

            // Subscribe to events
            _eventSystem.Subscribe(BattlEyeEventType.Connected, (sender, args) => OnConnected(args));
            _eventSystem.Subscribe(BattlEyeEventType.Disconnected, (sender, args) => OnDisconnected(args));
            _eventSystem.Subscribe(BattlEyeEventType.PacketReceived, (sender, args) => OnPacketReceived(args));
            _eventSystem.Subscribe(BattlEyeEventType.PacketSent, (sender, args) => OnPacketSent(args));
            _eventSystem.Subscribe(BattlEyeEventType.CommandExecuted, (sender, args) => OnCommandExecuted(args));
            _eventSystem.Subscribe(BattlEyeEventType.CommandFailed, (sender, args) => OnCommandFailed(args));
        }

        public async Task<BattlEyeConnectionResult> ConnectAsync(
            IPAddress host,
            int port,
            string password,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Information("Connecting to BattlEye server at {Host}:{Port}", host, port);

                _connectionStatus = ConnectionStatus.Connecting;

                // Start metrics collection
                await _metricsCollector.StartAsync();

                // Start command queue processing
                _commandQueue.StartProcessing();

                // Connect to server
                var result = await _connectionManager.ConnectAsync(host, port, password, cancellationToken);
                if (!result.Success)
                {
                    _connectionStatus = ConnectionStatus.Disconnected;
                    return result;
                }

                // Start background tasks
                _receiveTask = ReceivePacketsAsync(_cancellationTokenSource.Token);
                _heartbeatTask = SendHeartbeatAsync(_cancellationTokenSource.Token);

                _connectionStatus = ConnectionStatus.Connected;
                _logger.Information("Successfully connected to BattlEye server");
                return BattlEyeConnectionResult.CreateSuccess();
            }
            catch (Exception ex)
            {
                _connectionStatus = ConnectionStatus.Disconnected;
                _logger.Error(ex, "Failed to connect to BattlEye server");
                return BattlEyeConnectionResult.CreateFailure(
                    BattlEyeConnectionResultStatus.ConnectionFailed,
                    ex.Message);
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                _logger.Information("Disconnecting from BattlEye server");
                _connectionStatus = ConnectionStatus.Disconnecting;

                // Stop background tasks
                _cancellationTokenSource.Cancel();
                await Task.WhenAll(
                    _receiveTask ?? Task.CompletedTask,
                    _heartbeatTask ?? Task.CompletedTask,
                    _reconnectTask ?? Task.CompletedTask);

                // Stop components
                await _metricsCollector.StopAsync();
                await _commandQueue.StopProcessingAsync();
                await _connectionManager.DisconnectAsync();

                _connectionStatus = ConnectionStatus.Disconnected;
                _logger.Information("Successfully disconnected from BattlEye server");
            }
            catch (Exception ex)
            {
                _connectionStatus = ConnectionStatus.Disconnected;
                _logger.Error(ex, "Error during disconnection");
                throw;
            }
        }

        public async Task<string> SendCommandAsync(string command, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Sending command: {Command}", command);

                if (_connectionStatus != ConnectionStatus.Connected)
                {
                    throw new InvalidOperationException("Cannot send command while not connected");
                }

                var result = await _commandQueue.EnqueueCommandAsync(command, CommandPriority.Normal, cancellationToken);
                _logger.Debug("Command result: {Result}", result);
                return result ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to send command: {Command}", command);
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_isDisposed || _isDisposing)
                return;

            _isDisposing = true;

            try
            {
                await DisconnectAsync();
                _cancellationTokenSource.Dispose();
                _socket.Dispose();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during async disposal");
            }
            finally
            {
                _isDisposed = true;
                _isDisposing = false;
            }
        }

        public void Dispose()
        {
            if (_isDisposed || _isDisposing)
                return;

            _isDisposing = true;

            try
            {
                DisconnectAsync().GetAwaiter().GetResult();
                _cancellationTokenSource.Dispose();
                _socket.Dispose();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during disposal");
            }
            finally
            {
                _isDisposed = true;
                _isDisposing = false;
            }
        }

        private async Task ReceivePacketsAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var packet = await _packetProcessor.ProcessPacketAsync(new byte[0]);
                    if (packet != null)
                    {
                        await ProcessReceivedPacketAsync(packet);
                    }
                }
            }
            catch (Exception ex) when (!_isDisposing)
            {
                _logger.Error(ex, "Error in packet receive loop");
                await HandleConnectionErrorAsync(ex);
            }
        }

        private async Task SendHeartbeatAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                    if (_connectionStatus == ConnectionStatus.Connected)
                    {
                        await SendCommandAsync("#ping", cancellationToken);
                    }
                }
            }
            catch (Exception ex) when (!_isDisposing)
            {
                _logger.Error(ex, "Error in heartbeat loop");
                await HandleConnectionErrorAsync(ex);
            }
        }

        private async Task HandleConnectionErrorAsync(Exception error)
        {
            if (_connectionStatus == ConnectionStatus.Connected)
            {
                _logger.Warning("Connection error detected, initiating reconnection");
                _connectionStatus = ConnectionStatus.Disconnected;
                await _eventSystem.RaiseEventAsync(BattlEyeEventType.Error, new BattlEyeEventArgs { Error = error });
                await AttemptReconnectionAsync();
            }
        }

        private async Task AttemptReconnectionAsync()
        {
            for (int attempt = 1; attempt <= MAX_RECONNECT_ATTEMPTS; attempt++)
            {
                try
                {
                    _logger.Information("Reconnection attempt {Attempt} of {MaxAttempts}", attempt, MAX_RECONNECT_ATTEMPTS);
                    await Task.Delay(RECONNECT_DELAY_MS * attempt);
                    
                    var result = ConnectAsync(IPAddress.Parse(_host), _port, _password);
                    if (result.Result.Success)
                    {
                        _logger.Information("Reconnection successful");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Reconnection attempt {Attempt} failed", attempt);
                }
            }
            
            _logger.Error("All reconnection attempts failed");
            return;
        }

        private async Task ProcessReceivedPacketAsync(PacketProcessor.ProcessedPacket packet)
        {
            try
            {
                _logger.Debug("Processing received packet: {PacketType}", packet.Type);
                
                if (_pendingPackets.TryRemove(packet.SequenceNumber, out var completionSource))
                {
                    completionSource.SetResult(packet);
                }
                
                await _eventSystem.RaiseEventAsync(BattlEyeEventType.PacketReceived, new BattlEyeEventArgs { Packet = packet });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error processing received packet");
            }
        }

        private void OnConnected(BattlEyeEventArgs args)
        {
            _connectionStatus = ConnectionStatus.Connected;
            OnMessageReceived?.Invoke(this, args);
        }

        private void OnDisconnected(BattlEyeEventArgs args)
        {
            _connectionStatus = ConnectionStatus.Disconnected;
            OnDisconnect?.Invoke(this, args);
        }

        private void OnPacketReceived(BattlEyeEventArgs args)
        {
            _logger.Debug("Packet received: {PacketType}", args.Packet?.Type);
        }

        private void OnPacketSent(BattlEyeEventArgs args)
        {
            _logger.Debug("Packet sent: {Command}", args.Command);
        }

        private void OnCommandExecuted(BattlEyeEventArgs args)
        {
            _logger.Information("Command executed: {Command}", args.Command);
        }

        private void OnCommandFailed(BattlEyeEventArgs args)
        {
            _logger.Warning("Command failed: {Command}, Error: {Error}", args.Command, args.Error?.Message);
        }
    }

    public class StateObject
    {
        public Socket WorkSocket { get; }
        public const int BufferSize = 2048;
        public byte[] Buffer = new byte[BufferSize];
        public StringBuilder Message = new StringBuilder();
        public int PacketsTodo;

        public StateObject(Socket workSocket)
        {
            WorkSocket = workSocket;
        }
    }
}
