using Serilog;

namespace ArmaReforgerServerMonitor.Frontend.Models
{
    /// <summary>
    /// Helper class for logging DisconnectionType events.
    /// </summary>
    public static class DisconnectionTypeHelper
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext(typeof(DisconnectionTypeHelper));

        /// <summary>
        /// Logs a disconnection event with detailed information.
        /// </summary>
        /// <param name="type">The type of disconnection.</param>
        /// <param name="serverAddress">The server address that was disconnected from.</param>
        /// <param name="additionalInfo">Additional information about the disconnection.</param>
        public static void LogDisconnection(DisconnectionType type, string serverAddress, string? additionalInfo = null)
        {
            _logger.Verbose("Disconnection Event - Type: {Type}, Server: {Server}",
                type,
                serverAddress);

            switch (type)
            {
                case DisconnectionType.Manual:
                    _logger.Verbose("Manual disconnection initiated by user from server: {Server}", serverAddress);
                    break;

                case DisconnectionType.Timeout:
                    _logger.Verbose("Connection timeout occurred with server: {Server}", serverAddress);
                    break;

                case DisconnectionType.ConnectionLost:
                    _logger.Verbose("Connection lost with server: {Server}", serverAddress);
                    break;

                case DisconnectionType.AuthenticationFailed:
                    _logger.Verbose("Authentication failed with server: {Server}", serverAddress);
                    break;

                case DisconnectionType.ServerClosed:
                    _logger.Verbose("Server closed the connection: {Server}", serverAddress);
                    break;

                case DisconnectionType.Unknown:
                    _logger.Verbose("Unknown disconnection type from server: {Server}", serverAddress);
                    break;
            }

            if (!string.IsNullOrEmpty(additionalInfo))
            {
                _logger.Verbose("Additional Disconnection Information: {Info}", additionalInfo);
            }
        }

        /// <summary>
        /// Gets a user-friendly description of the disconnection type.
        /// </summary>
        public static string GetDescription(DisconnectionType type)
        {
            var description = type switch
            {
                DisconnectionType.Manual => "Manual disconnection by user",
                DisconnectionType.Timeout => "Connection timeout",
                DisconnectionType.ConnectionLost => "Connection lost due to network issues",
                DisconnectionType.AuthenticationFailed => "Authentication failure",
                DisconnectionType.ServerClosed => "Server closed the connection",
                DisconnectionType.Unknown => "Unknown disconnection reason",
                _ => "Invalid disconnection type"
            };

            _logger.Verbose("Getting description for DisconnectionType: {Type} -> {Description}",
                type,
                description);

            return description;
        }
    }
}