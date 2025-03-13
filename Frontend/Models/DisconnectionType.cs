namespace ArmaReforgerServerMonitor.Frontend.Models
{
    /// <summary>
    /// Represents the type of disconnection from the RCON server.
    /// </summary>
    public enum DisconnectionType
    {
        /// <summary>
        /// Manual disconnection by the user.
        /// </summary>
        Manual,

        /// <summary>
        /// Connection timeout.
        /// </summary>
        Timeout,

        /// <summary>
        /// Connection lost due to network issues.
        /// </summary>
        ConnectionLost,

        /// <summary>
        /// Authentication failure.
        /// </summary>
        AuthenticationFailed,

        /// <summary>
        /// Server closed the connection.
        /// </summary>
        ServerClosed,

        /// <summary>
        /// Unknown disconnection reason.
        /// </summary>
        Unknown
    }
}