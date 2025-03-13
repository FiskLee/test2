/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 * BattleNET v1.3.4 - BattlEye Library and Client            *
 *                                                         *
 *  Copyright (C) 2018 by it's authors.                    *
 *  Some rights reserved. See license.txt, authors.txt.    *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

using System;
using System.Text.Json.Serialization;

namespace BattleNET
{
    /// <summary>
    /// Represents event arguments for BattlEye messages.
    /// </summary>
    public class BattlEyeMessageEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the message content.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the message identifier.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Gets the timestamp when the message was received.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets the message type.
        /// </summary>
        public BattlEyeMessageType Type { get; }

        /// <summary>
        /// Gets the message severity level.
        /// </summary>
        public BattlEyeMessageSeverity Severity { get; }

        /// <summary>
        /// Gets additional message properties.
        /// </summary>
        [JsonIgnore]
        public object? Properties { get; }

        /// <summary>
        /// Initializes a new instance of the BattlEyeMessageEventArgs class.
        /// </summary>
        /// <param name="message">The message content.</param>
        /// <param name="id">The message identifier.</param>
        /// <param name="type">The message type.</param>
        /// <param name="severity">The message severity level.</param>
        /// <param name="properties">Additional message properties.</param>
        /// <exception cref="ArgumentNullException">Thrown when message is null.</exception>
        public BattlEyeMessageEventArgs(
            string message,
            int id,
            BattlEyeMessageType type = BattlEyeMessageType.Unknown,
            BattlEyeMessageSeverity severity = BattlEyeMessageSeverity.Info,
            object? properties = null)
        {
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Id = id;
            Type = type;
            Severity = severity;
            Properties = properties;
            Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Type}] [{Severity}] {Message}";
        }
    }

    /// <summary>
    /// Represents the type of a BattlEye message.
    /// </summary>
    public enum BattlEyeMessageType
    {
        /// <summary>
        /// Unknown message type.
        /// </summary>
        Unknown,

        /// <summary>
        /// Player connected message.
        /// </summary>
        PlayerConnected,

        /// <summary>
        /// Player disconnected message.
        /// </summary>
        PlayerDisconnected,

        /// <summary>
        /// Chat message.
        /// </summary>
        Chat,

        /// <summary>
        /// Command response.
        /// </summary>
        CommandResponse,

        /// <summary>
        /// Server status message.
        /// </summary>
        ServerStatus,

        /// <summary>
        /// Error message.
        /// </summary>
        Error,

        /// <summary>
        /// Warning message.
        /// </summary>
        Warning,

        /// <summary>
        /// Information message.
        /// </summary>
        Information
    }

    /// <summary>
    /// Represents the severity level of a BattlEye message.
    /// </summary>
    public enum BattlEyeMessageSeverity
    {
        /// <summary>
        /// Debug level message.
        /// </summary>
        Debug,

        /// <summary>
        /// Information level message.
        /// </summary>
        Info,

        /// <summary>
        /// Warning level message.
        /// </summary>
        Warning,

        /// <summary>
        /// Error level message.
        /// </summary>
        Error,

        /// <summary>
        /// Critical level message.
        /// </summary>
        Critical
    }

    /// <summary>
    /// Provides extension methods for BattlEyeMessageEventArgs handling.
    /// </summary>
    public static class BattlEyeMessageEventArgsExtensions
    {
        /// <summary>
        /// Determines if the message is a player-related message.
        /// </summary>
        /// <param name="args">The message event arguments to check.</param>
        /// <returns>true if the message is player-related; otherwise, false.</returns>
        public static bool IsPlayerMessage(this BattlEyeMessageEventArgs args)
        {
            return args.Type == BattlEyeMessageType.PlayerConnected ||
                   args.Type == BattlEyeMessageType.PlayerDisconnected;
        }

        /// <summary>
        /// Determines if the message is a chat message.
        /// </summary>
        /// <param name="args">The message event arguments to check.</param>
        /// <returns>true if the message is a chat message; otherwise, false.</returns>
        public static bool IsChatMessage(this BattlEyeMessageEventArgs args)
        {
            return args.Type == BattlEyeMessageType.Chat;
        }

        /// <summary>
        /// Determines if the message is a command response.
        /// </summary>
        /// <param name="args">The message event arguments to check.</param>
        /// <returns>true if the message is a command response; otherwise, false.</returns>
        public static bool IsCommandResponse(this BattlEyeMessageEventArgs args)
        {
            return args.Type == BattlEyeMessageType.CommandResponse;
        }

        /// <summary>
        /// Determines if the message is a server status message.
        /// </summary>
        /// <param name="args">The message event arguments to check.</param>
        /// <returns>true if the message is a server status message; otherwise, false.</returns>
        public static bool IsServerStatusMessage(this BattlEyeMessageEventArgs args)
        {
            return args.Type == BattlEyeMessageType.ServerStatus;
        }

        /// <summary>
        /// Determines if the message is an error message.
        /// </summary>
        /// <param name="args">The message event arguments to check.</param>
        /// <returns>true if the message is an error message; otherwise, false.</returns>
        public static bool IsErrorMessage(this BattlEyeMessageEventArgs args)
        {
            return args.Type == BattlEyeMessageType.Error ||
                   args.Severity == BattlEyeMessageSeverity.Error ||
                   args.Severity == BattlEyeMessageSeverity.Critical;
        }

        /// <summary>
        /// Determines if the message is a warning message.
        /// </summary>
        /// <param name="args">The message event arguments to check.</param>
        /// <returns>true if the message is a warning message; otherwise, false.</returns>
        public static bool IsWarningMessage(this BattlEyeMessageEventArgs args)
        {
            return args.Type == BattlEyeMessageType.Warning ||
                   args.Severity == BattlEyeMessageSeverity.Warning;
        }

        /// <summary>
        /// Gets a formatted message for display.
        /// </summary>
        /// <param name="args">The message event arguments to format.</param>
        /// <returns>A formatted message string.</returns>
        public static string GetFormattedMessage(this BattlEyeMessageEventArgs args)
        {
            var prefix = args.Type switch
            {
                BattlEyeMessageType.PlayerConnected => "[+]",
                BattlEyeMessageType.PlayerDisconnected => "[-]",
                BattlEyeMessageType.Chat => "[Chat]",
                BattlEyeMessageType.CommandResponse => "[Cmd]",
                BattlEyeMessageType.ServerStatus => "[Status]",
                BattlEyeMessageType.Error => "[Error]",
                BattlEyeMessageType.Warning => "[Warning]",
                BattlEyeMessageType.Information => "[Info]",
                _ => "[Unknown]"
            };

            return $"{prefix} {args.Message}";
        }

        /// <summary>
        /// Gets a color code for the message based on its severity.
        /// </summary>
        /// <param name="args">The message event arguments to get a color for.</param>
        /// <returns>A color code string.</returns>
        public static string GetColorCode(this BattlEyeMessageEventArgs args)
        {
            return args.Severity switch
            {
                BattlEyeMessageSeverity.Debug => "#808080",
                BattlEyeMessageSeverity.Info => "#000000",
                BattlEyeMessageSeverity.Warning => "#FFA500",
                BattlEyeMessageSeverity.Error => "#FF0000",
                BattlEyeMessageSeverity.Critical => "#800000",
                _ => "#000000"
            };
        }
    }
}
