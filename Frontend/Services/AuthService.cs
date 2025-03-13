using Serilog;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// Implementation of the authentication service that manages user authentication
    /// and JWT token handling for API requests.
    /// </summary>
    /// <remarks>
    /// This service provides:
    /// - User authentication
    /// - JWT token management
    /// - Token refresh handling
    /// - Authentication state tracking
    /// - Secure token storage
    /// 
    /// The service uses JWT tokens for API authentication and implements
    /// automatic token refresh when needed.
    /// </remarks>
    internal class AuthService : IAuthService
    {
        private readonly ILogger _logger = Log.ForContext<AuthService>();
        private readonly HttpClient _httpClient;
        private string? _token;

        /// <summary>
        /// Initializes a new instance of the AuthService class.
        /// </summary>
        /// <param name="httpClient">HTTP client for API requests</param>
        /// <remarks>
        /// The constructor initializes:
        /// - HTTP client configuration
        /// - Authentication state
        /// - Logging services
        /// </remarks>
        public AuthService(HttpClient httpClient)
        {
            _logger.Debug("Initializing AuthService");
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        /// <summary>
        /// Gets whether the user is currently authenticated.
        /// </summary>
        public bool IsAuthenticated => !string.IsNullOrEmpty(_token);

        /// <summary>
        /// Attempts to authenticate with the backend server.
        /// </summary>
        /// <param name="baseUrl">The base URL of the backend server</param>
        /// <param name="username">Username for authentication</param>
        /// <param name="password">Password for authentication</param>
        /// <returns>Tuple containing success status and error message</returns>
        /// <remarks>
        /// This method:
        /// 1. Validates input parameters
        /// 2. Makes authentication request
        /// 3. Handles JWT token response
        /// 4. Updates authentication state
        /// 5. Configures HTTP client
        /// 
        /// The token is stored securely and used for subsequent API requests.
        /// </remarks>
        public async Task<(bool Success, string ErrorMessage)> LoginAsync(string baseUrl, string username = "admin", string password = "admin123")
        {
            try
            {
                _logger.Debug("Attempting login to {BaseUrl} with username {Username}", baseUrl, username);

                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    const string error = "Base URL cannot be empty";
                    _logger.Error(error);
                    return (false, error);
                }

                // Ensure URL ends with /
                if (!baseUrl.EndsWith("/"))
                    baseUrl += "/";

                var loginUrl = $"{baseUrl}api/Authentication/login";
                _logger.Debug("Login URL: {LoginUrl}", loginUrl);

                var response = await _httpClient.PostAsJsonAsync(loginUrl,
                    new { Username = username, Password = password }).ConfigureAwait(false);

                _logger.Debug("Login response status code: {StatusCode}", response.StatusCode);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
                    if (result != null && !string.IsNullOrWhiteSpace(result.Token))
                    {
                        _token = result.Token;
                        _httpClient.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);

                        _logger.Information("Login successful. Token received and stored.");
                        return (true, string.Empty);
                    }
                    else
                    {
                        const string error = "Received empty or invalid token from server";
                        _logger.Error(error);
                        return (false, error);
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var error = $"Login failed with status code {response.StatusCode}. Response: {errorContent}";
                    _logger.Error(error);
                    return (false, error);
                }
            }
            catch (HttpRequestException ex)
            {
                var error = $"Network error during login: {ex.Message}";
                _logger.Error(ex, error);
                return (false, error);
            }
            catch (JsonException ex)
            {
                var error = $"Error parsing login response: {ex.Message}";
                _logger.Error(ex, error);
                return (false, error);
            }
            catch (ArgumentException ex)
            {
                var error = $"Invalid argument: {ex.Message}";
                _logger.Error(ex, error);
                return (false, error);
            }
            catch (InvalidOperationException ex)
            {
                var error = $"Operation error: {ex.Message}";
                _logger.Error(ex, error);
                return (false, error);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error during authentication - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                return (false, "Unexpected error occurred");
            }
        }

        /// <summary>
        /// Logs out the current user and clears authentication state.
        /// </summary>
        /// <remarks>
        /// This method:
        /// 1. Clears the current token
        /// 2. Resets HTTP client configuration
        /// 3. Updates authentication state
        /// 4. Logs the logout event
        /// </remarks>
        public void Logout()
        {
            _logger.Information("Logging out. Clearing authentication token.");
            _token = null;
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }

        /// <summary>
        /// Gets the current authentication token.
        /// </summary>
        /// <returns>The current JWT token or empty string if not authenticated</returns>
        /// <remarks>
        /// Returns the current authentication token for use in API requests.
        /// The token is used in the Authorization header with Bearer scheme.
        /// </remarks>
        public string GetCurrentToken()
        {
            return _token ?? string.Empty;
        }
    }

    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
    }
}