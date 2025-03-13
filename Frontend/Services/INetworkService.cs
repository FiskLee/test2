using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// Interface for managing HTTP communication with the backend server.
    /// Provides methods for making authenticated API requests and handling responses.
    /// </summary>
    /// <remarks>
    /// Key responsibilities:
    /// - Managing HTTP client lifecycle
    /// - Handling authentication headers
    /// - Implementing retry policies
    /// - Error handling and logging
    /// 
    /// This service provides a centralized way to communicate with the backend API,
    /// handling common concerns like authentication, retries, and error handling.
    /// It supports both authenticated and anonymous requests.
    /// 
    /// Usage example:
    /// ```csharp
    /// var response = await _networkService.GetAsync<ServerStatus>("api/status");
    /// await _networkService.PostAsync("api/config", newConfig);
    /// ```
    /// </remarks>
    public interface INetworkService
    {
        /// <summary>
        /// Makes an HTTP GET request to the specified endpoint.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the response to</typeparam>
        /// <param name="endpoint">The API endpoint (relative to base URL)</param>
        /// <param name="headers">Optional additional headers</param>
        /// <returns>The deserialized response object</returns>
        /// <remarks>
        /// This method:
        /// 1. Builds the full URL from base URL and endpoint
        /// 2. Adds authentication header if token available
        /// 3. Handles retry logic for transient failures
        /// 4. Deserializes the response to type T
        /// 
        /// Throws NetworkException for network errors or invalid responses.
        /// </remarks>
        Task<T?> GetAsync<T>(string endpoint, Dictionary<string, string>? headers = null) where T : class;

        /// <summary>
        /// Makes an HTTP POST request to the specified endpoint.
        /// </summary>
        /// <typeparam name="T">The type of the request body</typeparam>
        /// <param name="endpoint">The API endpoint (relative to base URL)</param>
        /// <param name="data">The data to send in the request body</param>
        /// <param name="headers">Optional additional headers</param>
        /// <returns>True if request was successful, false otherwise</returns>
        /// <remarks>
        /// This method:
        /// 1. Serializes the request body to JSON
        /// 2. Adds content-type and authentication headers
        /// 3. Implements retry logic for failed requests
        /// 4. Validates the response status code
        /// 
        /// Throws NetworkException for network errors or invalid responses.
        /// </remarks>
        Task<bool> PostAsync<T>(string endpoint, T data, Dictionary<string, string>? headers = null) where T : class;

        /// <summary>
        /// Makes an HTTP PUT request to the specified endpoint.
        /// </summary>
        /// <typeparam name="T">The type of the request body</typeparam>
        /// <param name="endpoint">The API endpoint (relative to base URL)</param>
        /// <param name="data">The data to send in the request body</param>
        /// <param name="headers">Optional additional headers</param>
        /// <returns>True if request was successful, false otherwise</returns>
        /// <remarks>
        /// Similar to PostAsync, but uses HTTP PUT method.
        /// Typically used for updating existing resources.
        /// </remarks>
        Task<bool> PutAsync<T>(string endpoint, T data, Dictionary<string, string>? headers = null) where T : class;

        /// <summary>
        /// Makes an HTTP DELETE request to the specified endpoint.
        /// </summary>
        /// <param name="endpoint">The API endpoint (relative to base URL)</param>
        /// <param name="headers">Optional additional headers</param>
        /// <returns>True if request was successful, false otherwise</returns>
        /// <remarks>
        /// Used for deleting resources on the server.
        /// Validates response status code and handles errors.
        /// </remarks>
        Task<bool> DeleteAsync(string endpoint, Dictionary<string, string>? headers = null);

        /// <summary>
        /// Tests the connection to the backend server.
        /// </summary>
        /// <param name="timeoutMs">Timeout in milliseconds</param>
        /// <returns>True if server is reachable, false otherwise</returns>
        /// <remarks>
        /// This method:
        /// 1. Attempts to connect to the health check endpoint
        /// 2. Respects the specified timeout
        /// 3. Returns connection status
        /// 
        /// Used for validating server availability before operations.
        /// </remarks>
        Task<bool> TestConnectionAsync(int timeoutMs = 5000);

        /// <summary>
        /// Sets the base URL for API requests.
        /// </summary>
        /// <param name="baseUrl">The base URL of the backend server</param>
        /// <remarks>
        /// Updates the base URL used for all requests.
        /// Validates the URL format and throws if invalid.
        /// </remarks>
        void SetBaseUrl(Uri baseUrl);

        /// <summary>
        /// Sets the authentication token for API requests.
        /// </summary>
        /// <param name="token">The JWT token for authentication</param>
        /// <remarks>
        /// Updates the authentication header used for requests.
        /// Pass null to clear the authentication token.
        /// </remarks>
        void SetAuthToken(string? token);

        /// <summary>
        /// Event that fires when network status changes.
        /// </summary>
        /// <remarks>
        /// Notifies subscribers of:
        /// - Connection established/lost
        /// - Authentication changes
        /// - Server availability changes
        /// </remarks>
        event EventHandler<NetworkStatusEventArgs>? NetworkStatusChanged;

        event EventHandler<NetworkException>? NetworkError;
    }

    /// <summary>
    /// Event arguments for network status changes.
    /// </summary>
    public class NetworkStatusEventArgs : EventArgs
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext<NetworkStatusEventArgs>();

        /// <summary>
        /// Whether the server is currently reachable
        /// </summary>
        public bool IsConnected { get; }

        /// <summary>
        /// The current base URL being used
        /// </summary>
        public string BaseUrl { get; }

        /// <summary>
        /// Optional error message if connection failed
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Creates a new NetworkStatusEventArgs instance.
        /// </summary>
        public NetworkStatusEventArgs(bool isConnected, string baseUrl, string? errorMessage = null)
        {
            _logger.Verbose("Network status changed - Connected: {IsConnected}, BaseUrl: {BaseUrl}, Error: {Error}",
                isConnected,
                baseUrl,
                errorMessage ?? "none");

            IsConnected = isConnected;
            BaseUrl = baseUrl;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Returns a string representation of the network status.
        /// </summary>
        public override string ToString()
        {
            var status = $"Network Status: {(IsConnected ? "Connected" : "Disconnected")}, Base URL: {BaseUrl}";
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                status += $", Error: {ErrorMessage}";
            }

            _logger.Verbose("Converting NetworkStatusEventArgs to string: {Status}", status);
            return status;
        }
    }

    /// <summary>
    /// Custom exception for network-related errors.
    /// </summary>
    public class NetworkException : Exception
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext<NetworkException>();

        /// <summary>
        /// The HTTP status code if applicable
        /// </summary>
        public int? StatusCode { get; }

        /// <summary>
        /// The raw response content if available
        /// </summary>
        public string? ResponseContent { get; }

        /// <summary>
        /// Creates a new NetworkException instance.
        /// </summary>
        public NetworkException(string message, Exception? innerException = null, int? statusCode = null, string? responseContent = null)
            : base(message, innerException)
        {
            _logger.Verbose("Creating NetworkException - Message: {Message}, Status code: {Code}, Response content: {Content}",
                message,
                statusCode?.ToString(CultureInfo.InvariantCulture) ?? "none",
                responseContent ?? "none");

            StatusCode = statusCode;
            ResponseContent = responseContent;

            _logger.Verbose("NetworkException created successfully");
        }

        /// <summary>
        /// Returns a string representation of the network exception.
        /// </summary>
        public override string ToString()
        {
            var details = $"Network Error: {Message}";
            if (StatusCode.HasValue)
            {
                details += $", Status Code: {StatusCode}";
            }
            if (!string.IsNullOrEmpty(ResponseContent))
            {
                details += $", Response: {ResponseContent}";
            }

            _logger.Verbose("Converting NetworkException to string: {Details}", details);
            return details;
        }
    }
}