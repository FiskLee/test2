using ArmaReforgerServerMonitor.Frontend.Configuration;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// Implementation of the network service that handles HTTP communication
    /// with the backend server, including retries and error handling.
    /// </summary>
    /// <remarks>
    /// This service provides:
    /// - HTTP request handling
    /// - Authentication management
    /// - Retry policies
    /// - Error handling
    /// - Connection monitoring
    /// 
    /// The service uses Polly for resilient HTTP communication and
    /// implements automatic retries for transient failures.
    /// </remarks>
    internal class NetworkService : INetworkService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly Serilog.ILogger _logger;
        private readonly AppSettings _settings;
        private readonly IAsyncPolicy<HttpResponseMessage> _policy;
        private readonly IAsyncPolicy<HttpResponseMessage> _circuitBreaker;
        private string _baseUrl = string.Empty;
        private bool _isDisposed;

        /// <summary>
        /// Event that fires when network status changes.
        /// </summary>
        public event EventHandler<NetworkStatusEventArgs>? NetworkStatusChanged;

        /// <summary>
        /// Event that fires when a network error occurs.
        /// </summary>
        public event EventHandler<NetworkException>? NetworkError;

        /// <summary>
        /// Initializes a new instance of the NetworkService class.
        /// </summary>
        /// <param name="httpClient">HTTP client for requests</param>
        /// <param name="settings">Application settings</param>
        /// <remarks>
        /// The constructor:
        /// 1. Configures HTTP client
        /// 2. Sets up retry policies
        /// 3. Initializes error handling
        /// 4. Configures timeouts
        /// </remarks>
        public NetworkService(HttpClient httpClient, IOptions<AppSettings> settings)
        {
            if (httpClient == null)
            {
                throw new ArgumentNullException(nameof(httpClient), "HTTP client cannot be null");
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings), "Settings cannot be null");
            }

            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _logger = Log.ForContext<NetworkService>();

            _logger.Verbose("NetworkService initialization started - Max retries: {MaxRetries}, Connection timeout: {Timeout}s",
                _settings.MaxRetryAttempts, _settings.ConnectionTimeoutSeconds);

            _circuitBreaker = Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .Or<TimeoutException>()
                .AdvancedCircuitBreakerAsync(
                    failureThreshold: 0.5, // Break on 50% failure rate
                    samplingDuration: TimeSpan.FromSeconds(30),
                    minimumThroughput: 5,
                    durationOfBreak: TimeSpan.FromMinutes(1),
                    onBreak: (ex, duration) =>
                    {
                        _logger.Warning(
                            "Circuit breaker tripped due to {ExceptionType} - Breaking for {Duration}s",
                            ex.Exception?.GetType().Name ?? "unknown",
                            duration.TotalSeconds);
                    },
                    onReset: () =>
                    {
                        _logger.Information("Circuit breaker reset - Resuming normal operations");
                    },
                    onHalfOpen: () =>
                    {
                        _logger.Information("Circuit breaker half-open - Testing connectivity");
                    });

            _policy = Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .WaitAndRetryAsync(
                    _settings.MaxRetryAttempts,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (delegateResult, timeSpan, retryCount, context) =>
                    {
                        var exception = delegateResult.Exception;
                        _logger.Warning("Retry {RetryCount} after {TimeSpan}s - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                            retryCount, timeSpan.TotalSeconds, exception.GetType().Name, exception.Message, exception.StackTrace);
                    })
                .WrapAsync(_circuitBreaker);

            _logger.Debug("Network policies configured - Max retries: {MaxRetries}, Circuit breaker threshold: {Threshold}, Break duration: {Duration}s",
                _settings.MaxRetryAttempts, 2, 1);

            ConfigureHttpClient();
            _logger.Information("NetworkService initialized successfully");
        }

        /// <summary>
        /// Makes an HTTP GET request to the specified endpoint.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the response to</typeparam>
        /// <param name="endpoint">The API endpoint</param>
        /// <param name="headers">Optional additional headers</param>
        /// <returns>The deserialized response object</returns>
        /// <remarks>
        /// This method:
        /// 1. Validates endpoint
        /// 2. Adds headers
        /// 3. Makes request with retries
        /// 4. Handles errors
        /// 5. Deserializes response
        /// </remarks>
        public async Task<T?> GetAsync<T>(string endpoint, Dictionary<string, string>? headers = null) where T : class
        {
            try
            {
                _logger.Verbose("Making GET request to {Endpoint} - Headers: {Headers}, Expected type: {Type}",
                    endpoint,
                    headers != null ? string.Join(", ", headers.Select(h => $"{h.Key}={h.Value}")) : "none",
                    typeof(T).Name);

                var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                AddHeaders(request, headers);

                _logger.Debug("Request configured - Method: {Method}, URI: {Uri}, Headers: {Headers}",
                    request.Method,
                    request.RequestUri,
                    string.Join(", ", request.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}")));

                var response = await _policy.ExecuteAsync(async () =>
                    await _httpClient.SendAsync(request).ConfigureAwait(false)).ConfigureAwait(false);

                _logger.Verbose("Response received - Status: {StatusCode}, Headers: {Headers}, Content type: {ContentType}",
                    response.StatusCode,
                    string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}")),
                    response.Content?.Headers.ContentType?.ToString());

                if (response.IsSuccessStatusCode)
                {
                    if (response.Content == null)
                    {
                        throw new InvalidOperationException("Response content is null");
                    }
                    var result = await response.Content.ReadFromJsonAsync<T>();
                    _logger.Debug("Successfully retrieved data from {Endpoint} - Result type: {Type}, Value: {Value}",
                        endpoint,
                        result?.GetType().Name ?? "null",
                        result != null ? JsonSerializer.Serialize(result) : "null");
                    return result;
                }

                HandleErrorResponse(response);
                return null;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.Warning("Unauthorized access to endpoint: {Endpoint} - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    endpoint, ex.GetType().Name, ex.Message, ex.StackTrace);
                throw new UnauthorizedAccessException("Access denied", ex);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.Warning("Endpoint not found: {Endpoint} - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    endpoint, ex.GetType().Name, ex.Message, ex.StackTrace);
                throw new KeyNotFoundException($"Endpoint not found: {endpoint}", ex);
            }
            catch (HttpRequestException ex)
            {
                _logger.Error(ex, "HTTP request failed - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, "JSON parsing error - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
            catch (TaskCanceledException ex)
            {
                _logger.Error(ex, "Task was canceled - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error in network operation - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        /// <summary>
        /// Makes an HTTP POST request to the specified endpoint.
        /// </summary>
        /// <typeparam name="T">The type of the request body</typeparam>
        /// <param name="endpoint">The API endpoint</param>
        /// <param name="data">The data to send</param>
        /// <param name="headers">Optional additional headers</param>
        /// <returns>True if request was successful</returns>
        /// <remarks>
        /// This method:
        /// 1. Validates inputs
        /// 2. Serializes data
        /// 3. Makes request with retries
        /// 4. Handles errors
        /// 5. Processes response
        /// </remarks>
        public async Task<bool> PostAsync<T>(string endpoint, T data, Dictionary<string, string>? headers = null) where T : class
        {
            try
            {
                _logger.Verbose("Making POST request to {Endpoint} - Data type: {Type}, Headers: {Headers}",
                    endpoint,
                    data?.GetType().Name ?? "null",
                    headers != null ? string.Join(", ", headers.Select(h => $"{h.Key}={h.Value}")) : "none");

                var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = JsonContent.Create(data)
                };
                AddHeaders(request, headers);

                _logger.Debug("Request configured - Method: {Method}, URI: {Uri}, Content type: {ContentType}, Headers: {Headers}",
                    request.Method,
                    request.RequestUri,
                    request.Content?.Headers.ContentType?.ToString(),
                    string.Join(", ", request.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}")));

                var response = await _policy.ExecuteAsync(async () =>
                    await _httpClient.SendAsync(request).ConfigureAwait(false)).ConfigureAwait(false);

                _logger.Verbose("Response received - Status: {StatusCode}, Headers: {Headers}, Content type: {ContentType}",
                    response.StatusCode,
                    string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}")),
                    response.Content?.Headers.ContentType?.ToString());

                if (response.IsSuccessStatusCode)
                {
                    _logger.Debug("Successfully posted data to {Endpoint} - Status: {StatusCode}", endpoint, response.StatusCode);
                    return true;
                }

                HandleErrorResponse(response);
                return false;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.Warning("Unauthorized access to endpoint: {Endpoint} - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    endpoint, ex.GetType().Name, ex.Message, ex.StackTrace);
                throw new UnauthorizedAccessException("Access denied", ex);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.Warning("Resource not found at endpoint: {Endpoint} - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    endpoint, ex.GetType().Name, ex.Message, ex.StackTrace);
                throw new KeyNotFoundException($"Resource not found at endpoint: {endpoint}", ex);
            }
            catch (HttpRequestException ex)
            {
                _logger.Error(ex, "HTTP request failed for endpoint: {Endpoint} - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    endpoint, ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, "Failed to serialize/deserialize data for endpoint: {Endpoint} - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    endpoint, ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
            catch (TaskCanceledException ex)
            {
                _logger.Error(ex, "Task was canceled - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error in network operation - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        /// <summary>
        /// Makes an HTTP PUT request to the specified endpoint.
        /// </summary>
        /// <typeparam name="T">The type of the request body</typeparam>
        /// <param name="endpoint">The API endpoint</param>
        /// <param name="data">The data to send</param>
        /// <param name="headers">Optional additional headers</param>
        /// <returns>True if request was successful</returns>
        /// <remarks>
        /// Similar to PostAsync, but uses HTTP PUT method.
        /// Typically used for updating existing resources.
        /// </remarks>
        public async Task<bool> PutAsync<T>(string endpoint, T data, Dictionary<string, string>? headers = null) where T : class
        {
            try
            {
                _logger.Verbose("Making PUT request to {Endpoint} - Data type: {Type}, Headers: {Headers}",
                    endpoint,
                    data?.GetType().Name ?? "null",
                    headers != null ? string.Join(", ", headers.Select(h => $"{h.Key}={h.Value}")) : "none");

                var request = new HttpRequestMessage(HttpMethod.Put, endpoint)
                {
                    Content = JsonContent.Create(data)
                };
                AddHeaders(request, headers);

                _logger.Verbose("Request configured - Method: {Method}, URI: {Uri}, Content type: {ContentType}, Headers: {Headers}",
                    request.Method,
                    request.RequestUri,
                    request.Content?.Headers.ContentType?.ToString(),
                    string.Join(", ", request.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}")));

                var response = await _policy.ExecuteAsync(async () =>
                    await _httpClient.SendAsync(request).ConfigureAwait(false)).ConfigureAwait(false);

                _logger.Verbose("Response received - Status: {StatusCode}, Headers: {Headers}",
                    response.StatusCode,
                    string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}")));

                if (response.IsSuccessStatusCode)
                {
                    _logger.Verbose("Successfully updated data at {Endpoint}", endpoint);
                    return true;
                }

                HandleErrorResponse(response);
                return false;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.Warning("Unauthorized access to endpoint: {Endpoint} - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    endpoint, ex.GetType().Name, ex.Message, ex.StackTrace);
                throw new UnauthorizedAccessException("Access denied", ex);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.Warning("Resource not found at endpoint: {Endpoint} - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    endpoint, ex.GetType().Name, ex.Message, ex.StackTrace);
                throw new KeyNotFoundException($"Resource not found at endpoint: {endpoint}", ex);
            }
            catch (HttpRequestException ex)
            {
                _logger.Error(ex, "HTTP request failed for endpoint: {Endpoint} - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    endpoint, ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, "Failed to serialize/deserialize data for endpoint: {Endpoint} - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    endpoint, ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
            catch (TaskCanceledException ex)
            {
                _logger.Error(ex, "Task was canceled - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error in network operation - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        /// <summary>
        /// Makes an HTTP DELETE request to the specified endpoint.
        /// </summary>
        /// <param name="endpoint">The API endpoint</param>
        /// <param name="headers">Optional additional headers</param>
        /// <returns>True if request was successful</returns>
        /// <remarks>
        /// Similar to other HTTP methods, but uses DELETE method.
        /// Used for removing resources from the server.
        /// </remarks>
        public async Task<bool> DeleteAsync(string endpoint, Dictionary<string, string>? headers = null)
        {
            try
            {
                _logger.Verbose("Making DELETE request to {Endpoint} - Headers: {Headers}",
                    endpoint,
                    headers != null ? string.Join(", ", headers.Select(h => $"{h.Key}={h.Value}")) : "none");

                var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
                AddHeaders(request, headers);

                _logger.Verbose("Request configured - Method: {Method}, URI: {Uri}, Headers: {Headers}",
                    request.Method,
                    request.RequestUri,
                    string.Join(", ", request.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}")));

                var response = await _policy.ExecuteAsync(async () =>
                    await _httpClient.SendAsync(request).ConfigureAwait(false)).ConfigureAwait(false);

                _logger.Verbose("Response received - Status: {StatusCode}, Headers: {Headers}",
                    response.StatusCode,
                    string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}")));

                if (response.IsSuccessStatusCode)
                {
                    _logger.Verbose("Successfully deleted resource at {Endpoint}", endpoint);
                    return true;
                }

                HandleErrorResponse(response);
                return false;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.Warning("Unauthorized access to endpoint: {Endpoint} - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    endpoint, ex.GetType().Name, ex.Message, ex.StackTrace);
                throw new UnauthorizedAccessException("Access denied", ex);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.Warning("Resource not found at endpoint: {Endpoint} - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    endpoint, ex.GetType().Name, ex.Message, ex.StackTrace);
                throw new KeyNotFoundException($"Resource not found at endpoint: {endpoint}", ex);
            }
            catch (HttpRequestException ex)
            {
                _logger.Error(ex, "HTTP request failed for endpoint: {Endpoint} - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    endpoint, ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, "Failed to deserialize response from endpoint: {Endpoint} - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    endpoint, ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
            catch (TaskCanceledException ex)
            {
                _logger.Error(ex, "Task was canceled - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error in network operation - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        /// <summary>
        /// Tests the connection to the server.
        /// </summary>
        /// <param name="timeoutMs">Timeout in milliseconds</param>
        /// <returns>True if connection is successful</returns>
        /// <remarks>
        /// This method:
        /// 1. Makes a simple request
        /// 2. Verifies response
        /// 3. Updates connection status
        /// 4. Handles timeouts
        /// </remarks>
        public async Task<bool> TestConnectionAsync(int timeoutMs = 5000)
        {
            try
            {
                _logger.Verbose("Testing connection to server - Timeout: {Timeout}ms", timeoutMs);

                if (string.IsNullOrEmpty(_baseUrl))
                {
                    _logger.Warning("Cannot test connection - Base URL not set");
                    OnNetworkStatusChanged(false, "Base URL not configured");
                    return false;
                }

                using var cts = new CancellationTokenSource(timeoutMs);
                var request = new HttpRequestMessage(HttpMethod.Get, _baseUrl);

                var response = await _policy.ExecuteAsync(async () =>
                    await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false)).ConfigureAwait(false);

                var isConnected = response.IsSuccessStatusCode;

                _logger.Verbose("Connection test result - Success: {Success}, Status: {StatusCode}",
                    isConnected,
                    response.StatusCode);

                OnNetworkStatusChanged(isConnected);
                return isConnected;
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("Connection test timed out for server");
                return false;
            }
            catch (HttpRequestException ex)
            {
                _logger.Warning(ex, "Connection test failed for server - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                return false;
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, "JSON parsing error during connection test for server - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                return false;
            }

            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error during connection test for server - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                return false;
            }
        }

        /// <summary>
        /// Sets the base URL for all requests.
        /// </summary>
        /// <param name="baseUrl">The base URL to use</param>
        /// <remarks>
        /// This method:
        /// 1. Validates URL
        /// 2. Updates configuration
        /// 3. Updates client
        /// </remarks>
        public void SetBaseUrl(Uri baseUrl)
        {
            if (baseUrl == null)
            {
                throw new ArgumentNullException(nameof(baseUrl));
            }
            _baseUrl = baseUrl.ToString();
            _logger.Information("Base URL set to {BaseUrl}", _baseUrl);
        }

        /// <summary>
        /// Sets the authentication token for requests.
        /// </summary>
        /// <param name="token">The authentication token</param>
        /// <remarks>
        /// This method:
        /// 1. Updates token
        /// 2. Configures client
        /// 3. Handles null token
        /// </remarks>
        public void SetAuthToken(string? token)
        {
            _logger.Verbose("Setting auth token - Has token: {HasToken}", token != null);

            if (string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Remove("Authorization");
            }
            else
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            _logger.Verbose("Auth token configured");
        }

        private void ConfigureHttpClient()
        {
            try
            {
                _logger.Verbose("Configuring HTTP client");
                _httpClient.Timeout = TimeSpan.FromSeconds(_settings.ConnectionTimeoutSeconds);
                _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                _logger.Debug("HTTP client configured - Timeout: {Timeout}s, Default headers: {Headers}",
                    _settings.ConnectionTimeoutSeconds,
                    string.Join(", ", _httpClient.DefaultRequestHeaders.Select(h => $"{h.Key}={string.Join(",", h.Value)}")));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to configure HTTP client - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        private void AddHeaders(HttpRequestMessage request, Dictionary<string, string>? headers)
        {
            try
            {
                _logger.Verbose("Adding headers to request - Headers: {Headers}",
                    headers != null ? string.Join(", ", headers.Select(h => $"{h.Key}={h.Value}")) : "none");

                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        request.Headers.Add(header.Key, header.Value);
                        _logger.Verbose("Added header - Key: {Key}, Value: {Value}", header.Key, header.Value);
                    }
                }

                _logger.Debug("Headers added successfully - Total headers: {Count}",
                    request.Headers.Count());
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to add headers to request - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        private void HandleErrorResponse(HttpResponseMessage response)
        {
            try
            {
                _logger.Warning("Handling error response - Status: {StatusCode}, Reason: {ReasonPhrase}",
                    response.StatusCode, response.ReasonPhrase);

                var errorContent = response.Content.ReadAsStringAsync().Result;
                _logger.Debug("Error response content: {Content}", errorContent);

                var error = new NetworkException($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}", new Exception(errorContent));

                _logger.Verbose("Raising NetworkError event");
                NetworkError?.Invoke(this, error);
                _logger.Debug("NetworkError event raised successfully");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to handle error response - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        private void HandleRequestException(Exception ex, string endpoint)
        {
            _logger.Verbose(ex, "Request failed - Endpoint: {Endpoint}, Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                endpoint,
                ex.GetType().Name,
                ex.Message,
                ex.StackTrace);

            var networkException = new NetworkException($"Request to {endpoint} failed", ex);
            NetworkError?.Invoke(this, networkException);
            OnNetworkStatusChanged(false, ex.Message);
        }

        private void OnNetworkStatusChanged(bool isConnected, string? errorMessage = null)
        {
            _logger.Verbose("Network status changed - Connected: {Connected}, Error: {Error}",
                isConnected,
                errorMessage ?? "none");

            NetworkStatusChanged?.Invoke(this, new NetworkStatusEventArgs(isConnected, _baseUrl, errorMessage));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _httpClient?.Dispose();
                }
                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}