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
    /// Represents the result of executing a BattlEye command.
    /// </summary>
    public enum BattlEyeCommandResult
    {
        /// <summary>
        /// Command executed successfully.
        /// </summary>
        [Description("Command executed successfully")]
        Success,

        /// <summary>
        /// Command execution failed.
        /// </summary>
        [Description("Command execution failed")]
        Error,

        /// <summary>
        /// Not connected to the server.
        /// </summary>
        [Description("Not connected to the server")]
        NotConnected,

        /// <summary>
        /// Command is not supported by the server.
        /// </summary>
        [Description("Command is not supported by the server")]
        NotSupported,

        /// <summary>
        /// Command execution timed out.
        /// </summary>
        [Description("Command execution timed out")]
        Timeout,

        /// <summary>
        /// Invalid command parameters.
        /// </summary>
        [Description("Invalid command parameters")]
        InvalidParameters,

        /// <summary>
        /// Insufficient permissions to execute the command.
        /// </summary>
        [Description("Insufficient permissions to execute the command")]
        InsufficientPermissions
    }

    /// <summary>
    /// Provides extension methods for BattlEyeCommandResult handling.
    /// </summary>
    public static class BattlEyeCommandResultExtensions
    {
        /// <summary>
        /// Gets a user-friendly message for the command result.
        /// </summary>
        /// <param name="result">The command result to get a message for.</param>
        /// <returns>A user-friendly message describing the result.</returns>
        public static string GetMessage(this BattlEyeCommandResult result)
        {
            return Helpers.StringValueOf(result);
        }

        /// <summary>
        /// Determines if the command result indicates success.
        /// </summary>
        /// <param name="result">The command result to check.</param>
        /// <returns>true if the result indicates success; otherwise, false.</returns>
        public static bool IsSuccess(this BattlEyeCommandResult result)
        {
            return result == BattlEyeCommandResult.Success;
        }

        /// <summary>
        /// Determines if the command result indicates a connection issue.
        /// </summary>
        /// <param name="result">The command result to check.</param>
        /// <returns>true if the result indicates a connection issue; otherwise, false.</returns>
        public static bool IsConnectionIssue(this BattlEyeCommandResult result)
        {
            return result == BattlEyeCommandResult.NotConnected ||
                   result == BattlEyeCommandResult.Timeout;
        }

        /// <summary>
        /// Determines if the command result indicates a permission issue.
        /// </summary>
        /// <param name="result">The command result to check.</param>
        /// <returns>true if the result indicates a permission issue; otherwise, false.</returns>
        public static bool IsPermissionIssue(this BattlEyeCommandResult result)
        {
            return result == BattlEyeCommandResult.InsufficientPermissions;
        }

        /// <summary>
        /// Determines if the command result indicates a parameter issue.
        /// </summary>
        /// <param name="result">The command result to check.</param>
        /// <returns>true if the result indicates a parameter issue; otherwise, false.</returns>
        public static bool IsParameterIssue(this BattlEyeCommandResult result)
        {
            return result == BattlEyeCommandResult.InvalidParameters;
        }

        /// <summary>
        /// Determines if the command result indicates a compatibility issue.
        /// </summary>
        /// <param name="result">The command result to check.</param>
        /// <returns>true if the result indicates a compatibility issue; otherwise, false.</returns>
        public static bool IsCompatibilityIssue(this BattlEyeCommandResult result)
        {
            return result == BattlEyeCommandResult.NotSupported;
        }

        /// <summary>
        /// Gets the recommended action for handling the command result.
        /// </summary>
        /// <param name="result">The command result to get a recommendation for.</param>
        /// <returns>A string describing the recommended action.</returns>
        public static string GetRecommendedAction(this BattlEyeCommandResult result)
        {
            return result switch
            {
                BattlEyeCommandResult.Success => "No action required.",
                BattlEyeCommandResult.Error => "Check the server logs for detailed error information.",
                BattlEyeCommandResult.NotConnected => "Attempt to reconnect to the server.",
                BattlEyeCommandResult.NotSupported => "Update the client to a compatible version.",
                BattlEyeCommandResult.Timeout => "Check network connectivity and try again.",
                BattlEyeCommandResult.InvalidParameters => "Review the command parameters and try again.",
                BattlEyeCommandResult.InsufficientPermissions => "Request elevated permissions from the server administrator.",
                _ => "Unknown result. Please contact support."
            };
        }
    }
}