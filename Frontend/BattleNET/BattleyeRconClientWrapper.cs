using BattleNET;
using Polly;
using Serilog;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Rcon
{
    /// <summary>
    /// A higher-level wrapper for the BattleyeRconClient that provides additional functionality
    /// and better error handling.
    /// </summary>
    public class BattleyeRconClientWrapper : IDisposable
    {
        private readonly Serilog.ILogger _logger;
        private readonly IAsyncPolicy _retryPolicy;
        private BattleyeRconClient? _client;
        private bool _disposed;
        private Exception? _lastError;
        private BattlEyeCommandResult _lastCommandResult;

        public event EventHandler<string>? MessageReceived;
        public event EventHandler<DisconnectionType>? Disconnected;

        /// <summary>
        /// Initializes a new instance of the BattleyeRconClientWrapper class.
        /// </summary>
        /// <param name="logger">The logger to use for logging.</param>
        public BattleyeRconClientWrapper(Serilog.ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _retryPolicy = Policy
                .Handle<SocketException>()
                .Or<TimeoutException>()
                .WaitAndRetryAsync(3, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.Warning(exception, "Retry {RetryCount} after {TimeSpan}s due to {ExceptionType}",
                            retryCount, timeSpan.TotalSeconds, exception.GetType().Name);
                    });
        }

        /// <summary>
        /// Gets whether the client is connected to the server.
        /// </summary>
        public bool IsConnected => _client?.IsConnected ?? false;

        /// <summary>
        /// Gets the current connection status.
        /// </summary>
        public ConnectionStatus Status
        {
            get
            {
                if (_client == null)
                    return ConnectionStatus.Disconnected;
                return _client.IsConnected ? ConnectionStatus.Connected : ConnectionStatus.Disconnected;
            }
        }

        /// <summary>
        /// Gets the last error that occurred.
        /// </summary>
        public Exception? LastError => _lastError;

        /// <summary>
        /// Gets the last command result.
        /// </summary>
        public BattlEyeCommandResult LastCommandResult => _lastCommandResult;

        /// <summary>
        /// Connects to the specified server.
        /// </summary>
        /// <param name="host">The server host address.</param>
        /// <param name="port">The server port.</param>
        /// <param name="password">The RCON password.</param>
        /// <returns>The connection result.</returns>
        public async Task<BattlEyeConnectionResult> ConnectAsync(string host, int port, string password)
        {
            try
            {
                _logger.Information("Connecting to server {Host}:{Port}", host, port);

                _client = new BattleyeRconClient(host, port, password, Log.ForContext<BattleyeRconClientWrapper>());
                _client.MessageReceived += OnRconMessageReceived;
                _client.Disconnected += OnRconDisconnected;
                var isConnected = await _retryPolicy.ExecuteAsync(async () =>
                    await _client.ConnectAsync());

                BattlEyeConnectionResult result;
                if (isConnected)
                {
                    _logger.Information("Successfully connected to server {Host}:{Port}", host, port);
                    _lastError = null;
                    result = BattlEyeConnectionResult.CreateSuccess();
                }
                else
                {
                    _logger.Error("Failed to connect to server {Host}:{Port}", host, port);
                    _lastError = new Exception("Failed to connect");
                    result = BattlEyeConnectionResult.CreateFailure(BattlEyeConnectionResultStatus.ConnectionFailed, "Failed to connect");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error connecting to server {Host}:{Port}", host, port);
                _lastError = ex;
                return BattlEyeConnectionResult.CreateFailure(BattlEyeConnectionResultStatus.ConnectionFailed, ex.Message);
            }
        }

        /// <summary>
        /// Disconnects from the server.
        /// </summary>
        public async Task DisconnectAsync()
        {
            try
            {
                if (_client != null)
                {
                    _logger.Information("Disconnecting from server");
                    await _client.DisconnectAsync();
                    _client = null;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error disconnecting from server");
                _lastError = ex;
                throw;
            }
        }

        /// <summary>
        /// Executes a command on the server.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <returns>The command result.</returns>
        public async Task<string> ExecuteCommandAsync(string command)
        {
            if (!IsConnected)
            {
                _logger.Warning("Cannot execute command: Not connected to server");
                _lastCommandResult = BattlEyeCommandResult.NotConnected;
                return string.Empty;
            }

            try
            {
                _logger.Debug("Starting command execution: {Command}", command);
                var result = await _retryPolicy.ExecuteAsync(async () =>
                    await _client!.SendCommandAsync(command));
                _logger.Debug("Command execution completed: {Command}", command);
                _lastCommandResult = BattlEyeCommandResult.Success;
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error executing command: {Command}", command);
                _lastError = ex;
                _lastCommandResult = BattlEyeCommandResult.Error;
                throw;
            }
        }

        /// <summary>
        /// Executes a command on the server with parameters.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <param name="parameters">The command parameters.</param>
        /// <returns>The command result.</returns>
        public async Task<string> ExecuteCommandAsync(BattlEyeCommand command, params string[] parameters)
        {
            if (!IsConnected)
            {
                _logger.Warning("Cannot execute command: Not connected to server");
                _lastCommandResult = BattlEyeCommandResult.NotConnected;
                return string.Empty;
            }

            try
            {
                var commandString = command.GetCommandString(parameters);
                _logger.Debug("Executing command: {Command}", commandString);

                if (!command.ValidateParameters(parameters))
                {
                    _logger.Warning("Invalid parameters for command: {Command}", commandString);
                    _lastCommandResult = BattlEyeCommandResult.InvalidParameters;
                    return string.Empty;
                }

                var result = await _retryPolicy.ExecuteAsync(async () =>
                    await _client!.SendCommandAsync(commandString));

                _lastCommandResult = BattlEyeCommandResult.Success;
                return result;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error executing command: {Command}", command);
                _lastError = ex;
                _lastCommandResult = BattlEyeCommandResult.Error;
                return string.Empty;
            }
        }

        /// <summary>
        /// Releases all resources used by the BattleyeRconClientWrapper.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                if (_client != null)
                {
                    _logger.Debug("Disposing RCON client");
                    _client.Dispose();
                    _client = null;
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error during RCON client disposal");
                throw;
            }
            finally
            {
                _disposed = true;
            }
        }

        private void OnRconMessageReceived(object? sender, string message)
        {
            _logger.Debug("RCON message received: {Message}", message);
            MessageReceived?.Invoke(this, message);
        }

        private void OnRconDisconnected(object? sender, DisconnectionType type)
        {
            _logger.Warning("RCON disconnected: {Type}", type);
            Disconnected?.Invoke(this, type);
        }
    }

    /// <summary>
    /// Represents the connection status of the RCON client.
    /// </summary>
    public enum ConnectionStatus
    {
        /// <summary>
        /// Client is connected to the server.
        /// </summary>
        Connected,

        /// <summary>
        /// Client is disconnected from the server.
        /// </summary>
        Disconnected,

        /// <summary>
        /// Client is connecting to the server.
        /// </summary>
        Connecting,

        /// <summary>
        /// Client is disconnecting from the server.
        /// </summary>
        Disconnecting
    }
}
