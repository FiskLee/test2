using ArmaReforgerServerMonitor.Frontend.Models;
using BattleNET;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Rcon
{
    /// <summary>
    /// Wraps the BattleNET BattlEyeClient to provide asynchronous RCON connection, disconnection, and command execution.
    /// </summary>
    public class BattleyeRconClient : IDisposable
    {
        private readonly BattlEyeClient _client;
        private readonly BattlEyeLoginCredentials _credentials;
        private bool _disposed;
        private readonly Serilog.ILogger _logger;
        private readonly IAsyncPolicy _retryPolicy;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private readonly TaskCompletionSource<bool> _initializationComplete = new();
        private bool _isInitialized;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Dictionary<string, TaskCompletionSource<string>> _commandResponses = new();
        private readonly object _commandResponsesLock = new();

        public event EventHandler<string>? MessageReceived;
        public event EventHandler<DisconnectionType>? Disconnected;

        public bool IsConnected => _client.IsConnected;

        public string? LastError => _client.LastError?.Message;

        public string Status => _client.Status.ToString();

        public List<NetworkInterfaceDetails> NetworkInterfaces { get; set; } = new();

        private const int SOCKET_TIMEOUT_MS = 5000;
        private const int MAX_PACKET_SIZE = 4096;
        private const int MIN_PACKET_SIZE = 9;
        private const int MAX_RETRY_ATTEMPTS = 3;
        private const int CLIENT_TIMEOUT_SECONDS = 5;
        private const int SERVER_TIMEOUT_SECONDS = 20;
        private const int COMMAND_TIMEOUT_MS = 30000;

        // Register the code pages provider so that Encoding.GetEncoding(1252) works.
        static BattleyeRconClient()
        {
            Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }

        /// <summary>
        /// Initializes a new instance using host, port, and password.
        /// </summary>
        public BattleyeRconClient(string host, int port, string password, Serilog.ILogger logger)
        {
            _logger = logger;
            _logger.Verbose("Initializing BattleyeRconClient with - Host: {Host}, Port: {Port}, Password length: {PasswordLength}",
                host,
                port,
                password.Length);

            // Convert host to IPAddress.
            if (!IPAddress.TryParse(host, out IPAddress? ipAddress))
            {
                try
                {
                    var hostEntry = Dns.GetHostEntry(host);
                    ipAddress = hostEntry.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                    if (ipAddress == null)
                    {
                        throw new ArgumentException($"Could not resolve hostname {host} to an IPv4 address");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to resolve hostname {Host}", host);
                    throw new ArgumentException($"Failed to resolve hostname {host}", nameof(host), ex);
                }
            }

            _logger.Verbose("Creating BattlEyeLoginCredentials with IPv4: {IpAddress}, Port: {Port}", ipAddress, port);
            _credentials = new BattlEyeLoginCredentials(ipAddress, port, password);
            _logger.Verbose("Creating BattlEyeClient with credentials");
            _client = new BattlEyeClient(host, port, password);

            _logger.Verbose("Setting up event handlers");
            _client.OnMessageReceived += (sender, args) =>
            {
                _logger.Verbose("Received message from BattlEyeClient - Message: {Message}, Message length: {MessageLength}",
                    args.Message,
                    args.Message.Length);
                MessageReceived?.Invoke(this, args.Message);
            };

            _client.OnDisconnect += (sender, args) =>
            {
                _logger.Verbose("Disconnected from BattlEyeClient - Last error: {LastError}",
                    _client.LastError?.Message ?? "none");
                Disconnected?.Invoke(this, DisconnectionType.ConnectionLost);
            };

            // Initialize retry policy with exponential backoff
            _retryPolicy = Policy
                .Handle<SocketException>()
                .Or<TimeoutException>()
                .Or<InvalidOperationException>()
                .WaitAndRetryAsync(MAX_RETRY_ATTEMPTS, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.Debug("Retry {RetryCount} after {TimeSpan}s due to {ExceptionType} - Message: {Message}",
                            retryCount, timeSpan.TotalSeconds, exception.GetType().Name, exception.Message);
                    });

            _isInitialized = true;
            _initializationComplete.TrySetResult(true);
        }

        private void ConfigureSocket(Socket socket)
        {
            socket.NoDelay = true;
            socket.ReceiveTimeout = SOCKET_TIMEOUT_MS;
            socket.SendTimeout = SOCKET_TIMEOUT_MS;
            socket.DontFragment = true;
            socket.ExclusiveAddressUse = true;
            socket.LingerState = new LingerOption(false, 0);
        }

        private NetworkDiagnostics PerformNetworkDiagnostics()
        {
            var diagnostics = new NetworkDiagnostics
            {
                PingError = string.Empty,
                PortError = string.Empty,
                NetworkType = string.Empty,
                NetworkSpeed = "0",
                DnsError = string.Empty,
                InterfaceError = string.Empty,
                NetworkQuality = string.Empty
            };

            try
            {
                // DNS Resolution
                try
                {
                    var entry = Dns.GetHostEntry(_credentials.Host);
                    diagnostics.DnsResolution = new DnsResolutionResult
                    {
                        Success = true,
                        DnsError = string.Empty,
                        InterfaceError = string.Empty
                    };
                }
                catch (Exception ex)
                {
                    diagnostics.DnsResolution = new DnsResolutionResult
                    {
                        Success = false,
                        DnsError = ex.Message,
                        InterfaceError = string.Empty
                    };
                }

                // Ping Test
                try
                {
                    using var ping = new Ping();
                    var reply = ping.Send(_credentials.Host, 1000);
                    diagnostics.PingSuccess = reply.Status == IPStatus.Success;
                    diagnostics.PingError = reply.Status != IPStatus.Success ? $"Ping failed with status: {reply.Status}" : string.Empty;
                }
                catch (Exception ex)
                {
                    diagnostics.PingSuccess = false;
                    diagnostics.PingError = ex.Message;
                }

                // Port Test
                try
                {
                    using var client = new TcpClient();
                    var connectTask = client.ConnectAsync(_credentials.Host, _credentials.Port);
                    if (connectTask.Wait(1000))
                    {
                        diagnostics.PortOpen = client.Connected;
                        client.Close();
                    }
                    else
                    {
                        diagnostics.PortOpen = false;
                        diagnostics.PortError = "Connection timeout";
                    }
                }
                catch (Exception ex)
                {
                    diagnostics.PortOpen = false;
                    diagnostics.PortError = ex.Message;
                }

                // Network Interfaces
                try
                {
                    diagnostics.AvailableInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                        .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                        .Select(ni => new NetworkInterfaceDetails
                        {
                            Name = ni.Name,
                            Type = ni.NetworkInterfaceType.ToString(),
                            Status = ni.OperationalStatus.ToString()
                        })
                        .ToList();
                }
                catch (Exception ex)
                {
                    diagnostics.InterfaceError = ex.Message;
                }
            }
            catch (Exception)
            {
                // Remove GeneralError assignment if it doesn't exist in NetworkDiagnostics
                // diagnostics.GeneralError = ex.Message;
            }

            return diagnostics;
        }

        public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (IsConnected)
            {
                _logger.Warning("Already connected to server");
                return true;
            }

            try
            {
                await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    if (IsConnected)
                    {
                        _logger.Warning("Already connected to server");
                        return true;
                    }

                    if (!_isInitialized)
                    {
                        _logger.Warning("Client not initialized, waiting for initialization");
                        await _initializationComplete.Task.ConfigureAwait(false);
                    }

                    // Perform network diagnostics
                    var diagnostics = PerformNetworkDiagnostics();
                    if (!diagnostics.DnsResolution?.Success ?? true || !diagnostics.PingSuccess || !diagnostics.PortOpen)
                    {
                        var errorMessage = "Network diagnostics failed:\n" +
                            $"1. DNS Resolution: {(diagnostics.DnsResolution?.Success ?? false ? "Success" : "Failed")}\n" +
                            $"2. Ping Test: {(diagnostics.PingSuccess ? "Success" : "Failed")}\n" +
                            $"3. Port Test: {(diagnostics.PortOpen ? "Open" : "Closed")}\n" +
                            $"4. Network Interfaces: {diagnostics.AvailableInterfaces?.Count ?? 0}";

                        if (!string.IsNullOrEmpty(diagnostics.DnsResolution?.DnsError))
                            errorMessage += $"\nDNS Error: {diagnostics.DnsResolution.DnsError}";
                        if (!string.IsNullOrEmpty(diagnostics.PingError))
                            errorMessage += $"\nPing Error: {diagnostics.PingError}";
                        if (!string.IsNullOrEmpty(diagnostics.PortError))
                            errorMessage += $"\nPort Error: {diagnostics.PortError}";

                        _logger.Warning(errorMessage);
                        return false;
                    }

                    // Connect using the BattleNET client with retry policy
                    var result = await _retryPolicy.ExecuteAsync(async () =>
                    {
                        var connectResult = await _client.ConnectAsync(_credentials.Host, _credentials.Port, _credentials.Password, cancellationToken).ConfigureAwait(false);
                        if (!connectResult.Success)
                        {
                            throw new InvalidOperationException($"Connection failed: {connectResult.Message}");
                        }
                        return connectResult;
                    }).ConfigureAwait(false);

                    if (result.Success)
                    {
                        _logger.Information("Successfully connected to server {Host}:{Port}", _credentials.Host, _credentials.Port);
                        return true;
                    }
                    else
                    {
                        _logger.Error("Failed to connect to server: {Error}", result.Message);
                        return false;
                    }
                }
                finally
                {
                    _connectionLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to connect to server");
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                _logger.Verbose("Disconnecting from BattlEye server");
                await _client.DisconnectAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error disconnecting from BattlEye server");
                throw;
            }
        }

        public async Task<string> SendCommandAsync(string command)
        {
            if (!IsConnected)
            {
                _logger.Warning("Cannot send command: Not connected to server");
                throw new InvalidOperationException("Not connected to server");
            }

            try
            {
                _logger.Verbose("Sending command to BattlEye server: {Command}", command);

                // Create a TaskCompletionSource for this command
                var tcs = new TaskCompletionSource<string>();
                var commandId = Guid.NewGuid().ToString();

                lock (_commandResponsesLock)
                {
                    _commandResponses[commandId] = tcs;
                }

                // Set up a timeout for the command
                using var cts = new CancellationTokenSource(COMMAND_TIMEOUT_MS);
                cts.Token.Register(() =>
                {
                    lock (_commandResponsesLock)
                    {
                        _commandResponses.Remove(commandId);
                    }
                    tcs.TrySetException(new TimeoutException($"Command timed out after {COMMAND_TIMEOUT_MS}ms"));
                });

                // Send the command
                var result = await _retryPolicy.ExecuteAsync(async () =>
                {
                    var response = await _client.SendCommandAsync(command).ConfigureAwait(false);
                    return response;
                }).ConfigureAwait(false);

                // Wait for the response with timeout
                return await tcs.Task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error sending command to BattlEye server");
                throw;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _cancellationTokenSource.Cancel();
                    _cancellationTokenSource.Dispose();
                    _client.Dispose();
                    _connectionLock.Dispose();

                    lock (_commandResponsesLock)
                    {
                        foreach (var tcs in _commandResponses.Values)
                        {
                            tcs.TrySetCanceled();
                        }
                        _commandResponses.Clear();
                    }
                }
                _disposed = true;
            }
        }
    }

    public enum DisconnectionType
    {
        Manual,
        ConnectionLost,
        Timeout,
        Error
    }
}

