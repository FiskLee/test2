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
    /// Represents the result of attempting to connect to a BattlEye server.
    /// </summary>
    public class BattlEyeConnectionResult
    {
        /// <summary>
        /// Gets whether the connection was successful.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Gets the connection result status.
        /// </summary>
        public BattlEyeConnectionResultStatus Status { get; }

        /// <summary>
        /// Gets an optional message describing the result.
        /// </summary>
        public string? Message { get; }

        /// <summary>
        /// Initializes a new instance of the BattlEyeConnectionResult class.
        /// </summary>
        /// <param name="success">Whether the connection was successful.</param>
        /// <param name="status">The connection result status.</param>
        /// <param name="message">An optional message describing the result.</param>
        public BattlEyeConnectionResult(bool success, BattlEyeConnectionResultStatus status, string? message = null)
        {
            Success = success;
            Status = status;
            Message = message;
        }

        /// <summary>
        /// Creates a successful connection result.
        /// </summary>
        /// <returns>A successful connection result.</returns>
        public static BattlEyeConnectionResult CreateSuccess()
        {
            return new BattlEyeConnectionResult(true, BattlEyeConnectionResultStatus.Success);
        }

        /// <summary>
        /// Creates a failed connection result.
        /// </summary>
        /// <param name="status">The failure status.</param>
        /// <param name="message">An optional message describing the failure.</param>
        /// <returns>A failed connection result.</returns>
        public static BattlEyeConnectionResult CreateFailure(BattlEyeConnectionResultStatus status, string? message = null)
        {
            return new BattlEyeConnectionResult(false, status, message);
        }
    }

    /// <summary>
    /// Represents the status of a BattlEye connection attempt.
    /// </summary>
    public enum BattlEyeConnectionResultStatus
    {
        /// <summary>
        /// Successfully connected to the server.
        /// </summary>
        [Description("Connected successfully")]
        Success,

        /// <summary>
        /// Failed to connect to the server.
        /// </summary>
        [Description("Connection failed")]
        ConnectionFailed,

        /// <summary>
        /// Connection timed out.
        /// </summary>
        [Description("Connection timed out")]
        ConnectionTimeout,

        /// <summary>
        /// Invalid login credentials provided.
        /// </summary>
        [Description("Invalid login credentials")]
        InvalidLogin,

        /// <summary>
        /// Server is not responding.
        /// </summary>
        [Description("Server not responding")]
        ServerNotResponding,

        /// <summary>
        /// Server is full.
        /// </summary>
        [Description("Server is full")]
        ServerFull,

        /// <summary>
        /// Server is locked.
        /// </summary>
        [Description("Server is locked")]
        ServerLocked,

        /// <summary>
        /// Network timeout occurred.
        /// </summary>
        [Description("Connection timed out")]
        Timeout,

        /// <summary>
        /// Server version is incompatible.
        /// </summary>
        [Description("Incompatible server version")]
        IncompatibleVersion,

        /// <summary>
        /// Network diagnostics failed.
        /// </summary>
        [Description("Network diagnostics failed")]
        NetworkError,

        /// <summary>
        /// Invalid credentials provided.
        /// </summary>
        [Description("Invalid credentials")]
        InvalidCredentials,

        /// <summary>
        /// Unknown connection result.
        /// </summary>
        [Description("Unknown")]
        Unknown
    }

    /// <summary>
    /// Provides extension methods for BattlEyeConnectionResult handling.
    /// </summary>
    public static class BattlEyeConnectionResultExtensions
    {
        /// <summary>
        /// Gets a user-friendly message for the connection result.
        /// </summary>
        /// <param name="result">The connection result to get a message for.</param>
        /// <returns>A user-friendly message describing the result.</returns>
        public static string GetMessage(this BattlEyeConnectionResult result)
        {
            return Helpers.StringValueOf(result.Status);
        }

        /// <summary>
        /// Determines if the connection result indicates success.
        /// </summary>
        /// <param name="result">The connection result to check.</param>
        /// <returns>true if the result indicates success; otherwise, false.</returns>
        public static bool IsSuccess(this BattlEyeConnectionResult result)
        {
            return result.Success;
        }

        /// <summary>
        /// Determines if the connection result indicates a network issue.
        /// </summary>
        /// <param name="result">The connection result to check.</param>
        /// <returns>true if the result indicates a network issue; otherwise, false.</returns>
        public static bool IsNetworkIssue(this BattlEyeConnectionResult result)
        {
            return result.Status == BattlEyeConnectionResultStatus.ConnectionFailed ||
                   result.Status == BattlEyeConnectionResultStatus.ServerNotResponding ||
                   result.Status == BattlEyeConnectionResultStatus.Timeout;
        }

        /// <summary>
        /// Determines if the connection result indicates an authentication issue.
        /// </summary>
        /// <param name="result">The connection result to check.</param>
        /// <returns>true if the result indicates an authentication issue; otherwise, false.</returns>
        public static bool IsAuthenticationIssue(this BattlEyeConnectionResult result)
        {
            return result.Status == BattlEyeConnectionResultStatus.InvalidLogin;
        }

        /// <summary>
        /// Determines if the connection result indicates a server availability issue.
        /// </summary>
        /// <param name="result">The connection result to check.</param>
        /// <returns>true if the result indicates a server availability issue; otherwise, false.</returns>
        public static bool IsServerAvailabilityIssue(this BattlEyeConnectionResult result)
        {
            return result.Status == BattlEyeConnectionResultStatus.ServerFull ||
                   result.Status == BattlEyeConnectionResultStatus.ServerLocked;
        }

        /// <summary>
        /// Determines if the connection result indicates a compatibility issue.
        /// </summary>
        /// <param name="result">The connection result to check.</param>
        /// <returns>true if the result indicates a compatibility issue; otherwise, false.</returns>
        public static bool IsCompatibilityIssue(this BattlEyeConnectionResult result)
        {
            return result.Status == BattlEyeConnectionResultStatus.IncompatibleVersion;
        }

        /// <summary>
        /// Gets the recommended action for handling the connection result.
        /// </summary>
        /// <param name="result">The connection result to get a recommendation for.</param>
        /// <returns>A string describing the recommended action.</returns>
        public static string GetRecommendedAction(this BattlEyeConnectionResult result)
        {
            return result.Status switch
            {
                BattlEyeConnectionResultStatus.Success => "No action required.",
                BattlEyeConnectionResultStatus.ConnectionFailed => "Check your network connection and try again.",
                BattlEyeConnectionResultStatus.InvalidLogin => "Verify your login credentials and try again.",
                BattlEyeConnectionResultStatus.ServerNotResponding => "Check if the server is running and try again.",
                BattlEyeConnectionResultStatus.ServerFull => "Try connecting to a different server or wait for a slot to become available.",
                BattlEyeConnectionResultStatus.ServerLocked => "Contact the server administrator for access.",
                BattlEyeConnectionResultStatus.Timeout => "Check your network connection and try again.",
                BattlEyeConnectionResultStatus.IncompatibleVersion => "Update your client to a compatible version.",
                BattlEyeConnectionResultStatus.NetworkError => "Check your network connection and try again.",
                BattlEyeConnectionResultStatus.InvalidCredentials => "Verify your credentials and try again.",
                _ => "Unknown result. Please contact support."
            };
        }

        /// <summary>
        /// Gets whether the connection result is retryable.
        /// </summary>
        /// <param name="result">The connection result to check.</param>
        /// <returns>true if the result is retryable; otherwise, false.</returns>
        public static bool IsRetryable(this BattlEyeConnectionResult result)
        {
            return result.Status == BattlEyeConnectionResultStatus.ConnectionFailed ||
                   result.Status == BattlEyeConnectionResultStatus.ServerNotResponding ||
                   result.Status == BattlEyeConnectionResultStatus.Timeout;
        }

        /// <summary>
        /// Gets the recommended retry delay in milliseconds.
        /// </summary>
        /// <param name="result">The connection result to get a retry delay for.</param>
        /// <returns>The recommended retry delay in milliseconds.</returns>
        public static int GetRetryDelay(this BattlEyeConnectionResult result)
        {
            return result.Status switch
            {
                BattlEyeConnectionResultStatus.ConnectionFailed => 1000,
                BattlEyeConnectionResultStatus.ServerNotResponding => 2000,
                BattlEyeConnectionResultStatus.Timeout => 3000,
                _ => 0
            };
        }
    }
}
