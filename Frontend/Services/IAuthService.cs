using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// Interface for handling authentication with the backend server.
    /// Manages JWT token-based authentication, login/logout operations, and token state.
    /// </summary>
    /// <remarks>
    /// Key responsibilities:
    /// - Managing user authentication state
    /// - Handling JWT token lifecycle
    /// - Providing authentication status to other components
    /// - Securing API requests with valid tokens
    /// 
    /// Usage example:
    /// ```csharp
    /// var (success, error) = await _authService.LoginAsync("http://server", "admin", "password");
    /// if (success) {
    ///     // User is authenticated
    ///     var token = _authService.GetCurrentToken();
    /// } else {
    ///     // Handle error
    /// }
    /// ```
    /// </remarks>
    public interface IAuthService
    {
        /// <summary>
        /// Gets whether the user is currently authenticated.
        /// This property checks if there is a valid, non-expired JWT token available.
        /// </summary>
        /// <value>True if the user is authenticated and has a valid token, false otherwise.</value>
        bool IsAuthenticated { get; }

        /// <summary>
        /// Attempts to authenticate with the backend server using the provided credentials.
        /// </summary>
        /// <param name="baseUrl">The base URL of the backend server</param>
        /// <param name="username">Username for authentication (defaults to "admin")</param>
        /// <param name="password">Password for authentication (defaults to "admin123")</param>
        /// <returns>
        /// A tuple containing:
        /// - Success: Whether the authentication was successful
        /// - ErrorMessage: Description of the error if authentication failed, empty string otherwise
        /// </returns>
        /// <remarks>
        /// This method:
        /// 1. Makes a POST request to the authentication endpoint
        /// 2. Receives and validates the JWT token
        /// 3. Stores the token for future requests
        /// 4. Updates the IsAuthenticated state
        /// 
        /// The token is automatically added to subsequent API requests.
        /// </remarks>
        Task<(bool Success, string ErrorMessage)> LoginAsync(string baseUrl, string username = "admin", string password = "admin123");

        /// <summary>
        /// Logs out the current user and invalidates the authentication token.
        /// </summary>
        /// <remarks>
        /// This method:
        /// 1. Clears the stored JWT token
        /// 2. Sets IsAuthenticated to false
        /// 3. Cleans up any authentication-related resources
        /// 
        /// After calling this method, the user will need to log in again to access protected resources.
        /// </remarks>
        void Logout();

        /// <summary>
        /// Gets the current JWT authentication token.
        /// </summary>
        /// <returns>The current JWT token if authenticated, or an empty string if not authenticated.</returns>
        /// <remarks>
        /// This token is used to authenticate API requests to the backend server.
        /// The token should be included in the Authorization header as a Bearer token.
        /// 
        /// Example header: "Authorization: Bearer {token}"
        /// </remarks>
        string GetCurrentToken();
    }
}