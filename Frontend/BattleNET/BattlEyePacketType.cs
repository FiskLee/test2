/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 * BattleNET v1.3.4 - BattlEye Library and Client            *
 *                                                         *
 *  Copyright (C) 2018 by it's authors.                    *
 *  Some rights reserved. See license.txt, authors.txt.    *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

using System;
using System.ComponentModel;
using ArmaReforgerServerMonitor.Frontend.BattleNET.Helpers;

namespace BattleNET
{
    /// <summary>
    /// Represents the type of packet in the BattlEye protocol.
    /// </summary>
    public enum BattlEyePacketType
    {
        /// <summary>
        /// Login packet for authentication.
        /// </summary>
        [Description("Login")]
        Login,

        /// <summary>
        /// Command packet for sending RCON commands.
        /// </summary>
        [Description("Command")]
        Command,

        /// <summary>
        /// Acknowledge packet for confirming receipt.
        /// </summary>
        [Description("Acknowledge")]
        Acknowledge,

        /// <summary>
        /// Response packet for command results.
        /// </summary>
        [Description("Response")]
        Response,

        /// <summary>
        /// Event packet for server events.
        /// </summary>
        [Description("Event")]
        Event,

        /// <summary>
        /// Ping packet for connection keep-alive.
        /// </summary>
        [Description("Ping")]
        Ping,

        /// <summary>
        /// Pong packet for ping response.
        /// </summary>
        [Description("Pong")]
        Pong,

        /// <summary>
        /// Error packet for error messages.
        /// </summary>
        [Description("Error")]
        Error,

        /// <summary>
        /// Unknown packet type.
        /// </summary>
        [Description("Unknown")]
        Unknown
    }

    /// <summary>
    /// Provides extension methods for BattlEyePacketType handling.
    /// </summary>
    public static class BattlEyePacketTypeExtensions
    {
        /// <summary>
        /// Gets a user-friendly message for the packet type.
        /// </summary>
        /// <param name="type">The packet type to get a message for.</param>
        /// <returns>A user-friendly message describing the packet type.</returns>
        public static string GetMessage(this BattlEyePacketType type)
        {
            return Helpers.StringValueOf(type);
        }

        /// <summary>
        /// Gets the packet type from a byte value.
        /// </summary>
        /// <param name="value">The byte value to convert.</param>
        /// <returns>The corresponding packet type.</returns>
        public static BattlEyePacketType FromByte(byte value)
        {
            return value switch
            {
                0x00 => BattlEyePacketType.Login,
                0x01 => BattlEyePacketType.Command,
                0x02 => BattlEyePacketType.Acknowledge,
                0x03 => BattlEyePacketType.Response,
                0x04 => BattlEyePacketType.Event,
                0x05 => BattlEyePacketType.Ping,
                0x06 => BattlEyePacketType.Pong,
                0x07 => BattlEyePacketType.Error,
                _ => throw new ArgumentException($"Invalid packet type value: {value}", nameof(value))
            };
        }

        /// <summary>
        /// Gets the byte value for the packet type.
        /// </summary>
        /// <param name="type">The packet type to convert.</param>
        /// <returns>The corresponding byte value.</returns>
        public static byte ToByte(this BattlEyePacketType type)
        {
            return type switch
            {
                BattlEyePacketType.Login => 0x00,
                BattlEyePacketType.Command => 0x01,
                BattlEyePacketType.Acknowledge => 0x02,
                BattlEyePacketType.Response => 0x03,
                BattlEyePacketType.Event => 0x04,
                BattlEyePacketType.Ping => 0x05,
                BattlEyePacketType.Pong => 0x06,
                BattlEyePacketType.Error => 0x07,
                _ => throw new ArgumentException($"Invalid packet type: {type}", nameof(type))
            };
        }

        /// <summary>
        /// Determines if the packet type requires a response.
        /// </summary>
        /// <param name="type">The packet type to check.</param>
        /// <returns>true if the packet type requires a response; otherwise, false.</returns>
        public static bool RequiresResponse(this BattlEyePacketType type)
        {
            return type == BattlEyePacketType.Command ||
                   type == BattlEyePacketType.Ping;
        }

        /// <summary>
        /// Determines if the packet type is a response.
        /// </summary>
        /// <param name="type">The packet type to check.</param>
        /// <returns>true if the packet type is a response; otherwise, false.</returns>
        public static bool IsResponse(this BattlEyePacketType type)
        {
            return type == BattlEyePacketType.Response ||
                   type == BattlEyePacketType.Pong;
        }

        /// <summary>
        /// Determines if the packet type is a control packet.
        /// </summary>
        /// <param name="type">The packet type to check.</param>
        /// <returns>true if the packet type is a control packet; otherwise, false.</returns>
        public static bool IsControlPacket(this BattlEyePacketType type)
        {
            return type == BattlEyePacketType.Login ||
                   type == BattlEyePacketType.Ping ||
                   type == BattlEyePacketType.Pong;
        }

        /// <summary>
        /// Determines if the packet type is a data packet.
        /// </summary>
        /// <param name="type">The packet type to check.</param>
        /// <returns>true if the packet type is a data packet; otherwise, false.</returns>
        public static bool IsDataPacket(this BattlEyePacketType type)
        {
            return type == BattlEyePacketType.Command ||
                   type == BattlEyePacketType.Response ||
                   type == BattlEyePacketType.Event;
        }

        /// <summary>
        /// Gets the minimum packet size for the packet type.
        /// </summary>
        /// <param name="type">The packet type to check.</param>
        /// <returns>The minimum packet size in bytes.</returns>
        public static int GetMinPacketSize(this BattlEyePacketType type)
        {
            return type switch
            {
                BattlEyePacketType.Login => 9,
                BattlEyePacketType.Command => 9,
                BattlEyePacketType.Acknowledge => 9,
                BattlEyePacketType.Response => 9,
                BattlEyePacketType.Event => 9,
                BattlEyePacketType.Ping => 9,
                BattlEyePacketType.Pong => 9,
                BattlEyePacketType.Error => 9,
                _ => throw new ArgumentException($"Invalid packet type: {type}", nameof(type))
            };
        }

        /// <summary>
        /// Gets the maximum packet size for the packet type.
        /// </summary>
        /// <param name="type">The packet type to check.</param>
        /// <returns>The maximum packet size in bytes.</returns>
        public static int GetMaxPacketSize(this BattlEyePacketType type)
        {
            return type switch
            {
                BattlEyePacketType.Login => 4096,
                BattlEyePacketType.Command => 4096,
                BattlEyePacketType.Acknowledge => 9,
                BattlEyePacketType.Response => 4096,
                BattlEyePacketType.Event => 4096,
                BattlEyePacketType.Ping => 9,
                BattlEyePacketType.Pong => 9,
                BattlEyePacketType.Error => 4096,
                _ => throw new ArgumentException($"Invalid packet type: {type}", nameof(type))
            };
        }
    }
}
