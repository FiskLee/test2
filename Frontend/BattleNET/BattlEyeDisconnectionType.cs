/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 * BattleNET v1.3.4 - BattlEye Library and Client            *
 *                                                         *
 *  Copyright (C) 2018 by it's authors.                    *
 *  Some rights reserved. See license.txt, authors.txt.    *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

using System.ComponentModel;
using ArmaReforgerServerMonitor.Frontend.BattleNET.Helpers;

namespace BattleNET
{
    /// <summary>
    /// Represents the type of disconnection from a BattlEye server.
    /// </summary>
    public enum BattlEyeDisconnectionType
    {
        /// <summary>
        /// Disconnected manually by the user.
        /// </summary>
        [Description("Disconnected manually")]
        Manual,

        /// <summary>
        /// Connection lost due to network issues.
        /// </summary>
        [Description("Connection lost")]
        ConnectionLost,

        /// <summary>
        /// Disconnected due to a socket exception.
        /// </summary>
        [Description("Socket error")]
        SocketException,

        /// <summary>
        /// Disconnected due to server shutdown.
        /// </summary>
        [Description("Server shutdown")]
        ServerShutdown,

        /// <summary>
        /// Disconnected due to authentication failure.
        /// </summary>
        [Description("Authentication failed")]
        AuthenticationFailed,

        /// <summary>
        /// Disconnected due to server timeout.
        /// </summary>
        [Description("Server timeout")]
        ServerTimeout,

        /// <summary>
        /// Disconnected due to client timeout.
        /// </summary>
        [Description("Client timeout")]
        ClientTimeout,

        /// <summary>
        /// Disconnected due to protocol mismatch.
        /// </summary>
        [Description("Protocol mismatch")]
        ProtocolMismatch,

        /// <summary>
        /// Disconnected due to an unknown error.
        /// </summary>
        [Description("Unknown error")]
        Unknown
    }

    /// <summary>
    /// Provides extension methods for BattlEyeDisconnectionType handling.
    /// </summary>
    public static class BattlEyeDisconnectionTypeExtensions
    {
        /// <summary>
        /// Gets a user-friendly message for the disconnection type.
        /// </summary>
        /// <param name="type">The disconnection type to get a message for.</param>
        /// <returns>A user-friendly message describing the disconnection.</returns>
        public static string GetMessage(this BattlEyeDisconnectionType type)
        {
            return Helpers.StringValueOf(type);
        }

        /// <summary>
        /// Determines if the disconnection type indicates a network issue.
        /// </summary>
        /// <param name="type">The disconnection type to check.</param>
        /// <returns>true if the type indicates a network issue; otherwise, false.</returns>
        public static bool IsNetworkIssue(this BattlEyeDisconnectionType type)
        {
            return type == BattlEyeDisconnectionType.ConnectionLost ||
                   type == BattlEyeDisconnectionType.SocketException ||
                   type == BattlEyeDisconnectionType.ClientTimeout;
        }

        /// <summary>
        /// Determines if the disconnection type indicates a server issue.
        /// </summary>
        /// <param name="type">The disconnection type to check.</param>
        /// <returns>true if the type indicates a server issue; otherwise, false.</returns>
        public static bool IsServerIssue(this BattlEyeDisconnectionType type)
        {
            return type == BattlEyeDisconnectionType.ServerShutdown ||
                   type == BattlEyeDisconnectionType.ServerTimeout;
        }

        /// <summary>
        /// Determines if the disconnection type indicates an authentication issue.
        /// </summary>
        /// <param name="type">The disconnection type to check.</param>
        /// <returns>true if the type indicates an authentication issue; otherwise, false.</returns>
        public static bool IsAuthenticationIssue(this BattlEyeDisconnectionType type)
        {
            return type == BattlEyeDisconnectionType.AuthenticationFailed;
        }

        /// <summary>
        /// Determines if the disconnection type indicates a compatibility issue.
        /// </summary>
        /// <param name="type">The disconnection type to check.</param>
        /// <returns>true if the type indicates a compatibility issue; otherwise, false.</returns>
        public static bool IsCompatibilityIssue(this BattlEyeDisconnectionType type)
        {
            return type == BattlEyeDisconnectionType.ProtocolMismatch;
        }

        /// <summary>
        /// Determines if the disconnection type is retryable.
        /// </summary>
        /// <param name="type">The disconnection type to check.</param>
        /// <returns>true if the type is retryable; otherwise, false.</returns>
        public static bool IsRetryable(this BattlEyeDisconnectionType type)
        {
            return type == BattlEyeDisconnectionType.ConnectionLost ||
                   type == BattlEyeDisconnectionType.SocketException ||
                   type == BattlEyeDisconnectionType.ClientTimeout ||
                   type == BattlEyeDisconnectionType.ServerTimeout;
        }

        /// <summary>
        /// Gets the recommended action for handling the disconnection.
        /// </summary>
        /// <param name="type">The disconnection type to get a recommendation for.</param>
        /// <returns>A string describing the recommended action.</returns>
        public static string GetRecommendedAction(this BattlEyeDisconnectionType type)
        {
            return type switch
            {
                BattlEyeDisconnectionType.Manual => "No action required.",
                BattlEyeDisconnectionType.ConnectionLost => "Check your network connection and try again.",
                BattlEyeDisconnectionType.SocketException => "Check your network connection and try again.",
                BattlEyeDisconnectionType.ServerShutdown => "Wait for the server to come back online.",
                BattlEyeDisconnectionType.AuthenticationFailed => "Verify your login credentials and try again.",
                BattlEyeDisconnectionType.ServerTimeout => "Check your network connection and try again.",
                BattlEyeDisconnectionType.ClientTimeout => "Check your network connection and try again.",
                BattlEyeDisconnectionType.ProtocolMismatch => "Update your client to a compatible version.",
                BattlEyeDisconnectionType.Unknown => "Try reconnecting. If the issue persists, contact support.",
                _ => "Unknown disconnection type. Please contact support."
            };
        }

        /// <summary>
        /// Gets the recommended retry delay in milliseconds.
        /// </summary>
        /// <param name="type">The disconnection type to get a retry delay for.</param>
        /// <returns>The recommended retry delay in milliseconds.</returns>
        public static int GetRetryDelay(this BattlEyeDisconnectionType type)
        {
            return type switch
            {
                BattlEyeDisconnectionType.ConnectionLost => 1000,
                BattlEyeDisconnectionType.SocketException => 2000,
                BattlEyeDisconnectionType.ClientTimeout => 3000,
                BattlEyeDisconnectionType.ServerTimeout => 5000,
                _ => 0
            };
        }

        /// <summary>
        /// Gets whether the disconnection type requires user intervention.
        /// </summary>
        /// <param name="type">The disconnection type to check.</param>
        /// <returns>true if the type requires user intervention; otherwise, false.</returns>
        public static bool RequiresUserIntervention(this BattlEyeDisconnectionType type)
        {
            return type == BattlEyeDisconnectionType.AuthenticationFailed ||
                   type == BattlEyeDisconnectionType.ProtocolMismatch ||
                   type == BattlEyeDisconnectionType.Unknown;
        }
    }
}
