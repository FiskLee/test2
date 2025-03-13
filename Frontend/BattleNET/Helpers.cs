/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 * BattleNET v1.3.4 - BattlEye Library and Client            *
 *                                                         *
 *  Copyright (C) 2018 by it's authors.                    *
 *  Some rights reserved. See license.txt, authors.txt.    *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

using Serilog;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace ArmaReforgerServerMonitor.Frontend.BattleNET.Helpers
{
    public enum ServerStatus
    {
        Online,
        Offline,
        Maintenance
    }

    /// <summary>
    /// Provides utility methods for the BattleNET library.
    /// </summary>
    public static class Helpers
    {
        private static readonly Regex HexRegex = new(@"^[0-9A-Fa-f]+$", RegexOptions.Compiled);
        private static readonly Serilog.ILogger _logger = Log.ForContext(typeof(Helpers));

        /// <summary>
        /// Converts a hexadecimal string to ASCII.
        /// </summary>
        /// <param name="hexString">The hexadecimal string to convert.</param>
        /// <returns>The ASCII string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when hexString is null.</exception>
        /// <exception cref="ArgumentException">Thrown when hexString is not a valid hexadecimal string.</exception>
        public static string Hex2Ascii(string hexString)
        {
            ArgumentNullException.ThrowIfNull(hexString, nameof(hexString));
            _logger.Verbose("Converting hex to ASCII - Input length: {Length}, Content: {Content}",
                hexString.Length,
                hexString);

            if (!HexRegex.IsMatch(hexString))
            {
                throw new ArgumentException("Input string is not a valid hexadecimal string.", nameof(hexString));
            }

            if (hexString.Length % 2 != 0)
                throw new ArgumentException("Hexadecimal string must have an even length", nameof(hexString));

            try
            {
                var bytes = new byte[hexString.Length / 2];
                for (int i = 0; i < hexString.Length; i += 2)
                {
                    bytes[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);
                }

                var str = Encoding.ASCII.GetString(bytes);
                _logger.Verbose("Hex converted to ASCII - Output length: {Length}, Content: {Content}, Bytes: {Bytes}",
                    str.Length,
                    str,
                    BitConverter.ToString(bytes));
                return str;
            }
            catch (Exception ex)
            {
                _logger.Verbose(ex, "Error converting hex to ASCII - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name,
                    ex.Message,
                    ex.StackTrace);
                throw;
            }
        }

        /// <summary>
        /// Converts a string to bytes using the specified encoding.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        /// <returns>The byte array.</returns>
        /// <exception cref="ArgumentNullException">Thrown when s is null.</exception>
        public static byte[] String2Bytes(string s)
        {
            ArgumentNullException.ThrowIfNull(s, nameof(s));
            _logger.Verbose("Converting string to bytes - Input length: {Length}, Content: {Content}",
                s.Length,
                s);

            try
            {
                var bytes = Encoding.UTF8.GetBytes(s);
                _logger.Verbose("String converted to bytes - Output length: {Length}, Bytes: {Bytes}",
                    bytes.Length,
                    BitConverter.ToString(bytes));
                return bytes;
            }
            catch (Exception ex)
            {
                _logger.Verbose(ex, "Error converting string to bytes - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name,
                    ex.Message,
                    ex.StackTrace);
                throw;
            }
        }

        /// <summary>
        /// Converts bytes to a string using the specified encoding.
        /// </summary>
        /// <param name="bytes">The bytes to convert.</param>
        /// <returns>The string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when bytes is null.</exception>
        public static string Bytes2String(byte[] bytes)
        {
            ArgumentNullException.ThrowIfNull(bytes, nameof(bytes));
            _logger.Verbose("Converting bytes to string - Input length: {Length}, Bytes: {Bytes}",
                bytes.Length,
                BitConverter.ToString(bytes));

            try
            {
                var str = Encoding.UTF8.GetString(bytes);
                _logger.Verbose("Bytes converted to string - Output length: {Length}, Content: {Content}",
                    str.Length,
                    str);
                return str;
            }
            catch (Exception ex)
            {
                _logger.Verbose(ex, "Error converting bytes to string - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name,
                    ex.Message,
                    ex.StackTrace);
                throw;
            }
        }

        /// <summary>
        /// Converts a portion of bytes to a string using UTF-8 encoding.
        /// </summary>
        /// <param name="bytes">The bytes to convert.</param>
        /// <param name="index">The starting index.</param>
        /// <param name="count">The number of bytes to convert.</param>
        /// <returns>The string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when bytes is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when index or count is out of range.</exception>
        public static string Bytes2String(byte[] bytes, int index, int count)
        {
            _logger.Verbose("Converting bytes to string - Input length: {Length}, Start index: {Index}, Count: {Count}, Bytes: {Bytes}",
                bytes.Length,
                index,
                count,
                BitConverter.ToString(bytes, index, count));

            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            if (index < 0 || index >= bytes.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (count < 0 || index + count > bytes.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            try
            {
                var str = Encoding.UTF8.GetString(bytes, index, count);
                _logger.Verbose("Bytes converted to string - Output length: {Length}, Content: {Content}",
                    str.Length,
                    str);
                return str;
            }
            catch (Exception ex)
            {
                _logger.Verbose(ex, "Error converting bytes to string - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name,
                    ex.Message,
                    ex.StackTrace);
                throw;
            }
        }

        /// <summary>
        /// Gets the string value of an enum value.
        /// </summary>
        /// <param name="value">The enum value.</param>
        /// <returns>The string value.</returns>
        /// <exception cref="ArgumentNullException">Thrown when value is null.</exception>
        public static string StringValueOf(Enum value)
        {
            _logger.Verbose("Getting string value of enum - Type: {Type}, Value: {Value}",
                value?.GetType().Name ?? "null",
                value?.ToString() ?? "null");

            if (value == null)
            {
                _logger.Verbose("Enum value is null");
                throw new ArgumentNullException(nameof(value));
            }

            var fi = value.GetType().GetField(value.ToString());
            if (fi != null)
            {
                var attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
                if (attributes.Length > 0)
                {
                    _logger.Verbose("Found description attribute - Description: {Description}", attributes[0].Description);
                    return attributes[0].Description;
                }
            }

            _logger.Verbose("No description attribute found, using ToString() - Result: {Result}", value.ToString());
            return value.ToString();
        }

        /// <summary>
        /// Gets the enum value from a string description.
        /// </summary>
        /// <param name="value">The string description.</param>
        /// <param name="enumType">The enum type.</param>
        /// <returns>The enum value.</returns>
        /// <exception cref="ArgumentNullException">Thrown when value or enumType is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the string is not a description or value of the specified enum.</exception>
        public static object EnumValueOf(string value, Type enumType)
        {
            _logger.Verbose("Getting enum value from string - Value: {Value}, EnumType: {EnumType}",
                value ?? "null",
                enumType?.Name ?? "null");

            if (value == null)
            {
                _logger.Verbose("Value is null");
                throw new ArgumentNullException(nameof(value));
            }

            if (enumType == null)
            {
                _logger.Verbose("EnumType is null");
                throw new ArgumentNullException(nameof(enumType));
            }

            if (!enumType.IsEnum)
            {
                _logger.Verbose("Type is not an enum - Type: {Type}", enumType.Name);
                throw new ArgumentException("Type must be an enum", nameof(enumType));
            }

            var names = Enum.GetNames(enumType);
            _logger.Verbose("Found {Count} enum names", names.Length);

            foreach (var name in names)
            {
                var enumValue = (Enum)Enum.Parse(enumType, name);
                var description = StringValueOf(enumValue);
                _logger.Verbose("Checking enum name - Name: {Name}, Description: {Description}, Value: {Value}",
                    name,
                    description,
                    value);

                if (description.Equals(value))
                {
                    _logger.Verbose("Found matching enum value - Name: {Name}, Value: {Value}", name, enumValue);
                    return enumValue;
                }
            }

            _logger.Verbose("No matching enum value found for string: {Value}", value);
            throw new ArgumentException("The string is not a description or value of the specified enum.");
        }

        /// <summary>
        /// Validates a string is a valid IP address.
        /// </summary>
        /// <param name="ipAddress">The IP address to validate.</param>
        /// <returns>true if the string is a valid IP address; otherwise, false.</returns>
        public static bool IsValidIPAddress(string ipAddress)
        {
            _logger.Verbose("Validating IP address: {IpAddress}", ipAddress);

            if (string.IsNullOrEmpty(ipAddress))
            {
                _logger.Verbose("IP address is null or empty");
                return false;
            }

            var parts = ipAddress.Split('.');
            if (parts.Length != 4)
            {
                _logger.Verbose("IP address does not have 4 parts");
                return false;
            }

            try
            {
                var result = Array.TrueForAll(parts, part =>
                    byte.TryParse(part, out var number) && number >= 0 && number <= 255);
                if (result)
                {
                    _logger.Verbose("IP address is valid - AddressFamily: {AddressFamily}, IsIPv4MappedToIPv6: {IsIPv4MappedToIPv6}, ScopeId: {ScopeId}",
                        IPAddress.TryParse(ipAddress, out var parsedAddress) ? parsedAddress.AddressFamily : AddressFamily.Unknown,
                        IPAddress.TryParse(ipAddress, out parsedAddress) ? parsedAddress.IsIPv4MappedToIPv6 : false,
                        IPAddress.TryParse(ipAddress, out parsedAddress) ? parsedAddress.ScopeId : 0);
                }
                else
                {
                    _logger.Verbose("IP address is invalid");
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.Verbose(ex, "Error validating IP address - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name,
                    ex.Message,
                    ex.StackTrace);
                return false;
            }
        }

        /// <summary>
        /// Validates a string is a valid port number.
        /// </summary>
        /// <param name="port">The port number to validate.</param>
        /// <returns>true if the string is a valid port number; otherwise, false.</returns>
        public static bool IsValidPort(string port)
        {
            _logger.Verbose("Validating port string: {Port}", port ?? "null");

            if (string.IsNullOrWhiteSpace(port))
            {
                _logger.Verbose("Port string is null or empty");
                return false;
            }

            var result = int.TryParse(port, out var number) && number >= 1 && number <= 65535;
            _logger.Verbose("Port validation result: {Result}, Parsed number: {Number}",
                result,
                number);
            return result;
        }

        /// <summary>
        /// Sanitizes a string for use in a command.
        /// </summary>
        /// <param name="command">The string to sanitize.</param>
        /// <returns>The sanitized string.</returns>
        public static string SanitizeCommand(string command)
        {
            _logger.Verbose("Sanitizing command string - Input length: {Length}, Content: {Content}",
                command?.Length ?? 0,
                command ?? "null");

            if (string.IsNullOrEmpty(command))
            {
                _logger.Verbose("Input is null or empty, returning empty string");
                return string.Empty;
            }

            return command
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal)
                .Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\t", "\\t", StringComparison.Ordinal)
                .Replace("\b", "\\b", StringComparison.Ordinal)
                .Replace("\f", "\\f", StringComparison.Ordinal);
        }

        /// <summary>
        /// Formats a byte array as a hexadecimal string.
        /// </summary>
        /// <param name="data">The bytes to format.</param>
        /// <returns>The formatted hexadecimal string.</returns>
        public static string FormatHex(byte[] data)
        {
            _logger.Verbose("Formatting bytes as hex - Input length: {Length}, Bytes: {Bytes}",
                data?.Length ?? 0,
                data != null ? BitConverter.ToString(data) : "null");

            if (data == null || data.Length == 0)
            {
                _logger.Verbose("Input is null or empty, returning empty string");
                return string.Empty;
            }

            var result = BitConverter.ToString(data)
                .Replace("-", " ", StringComparison.Ordinal)
                .ToUpperInvariant();
            _logger.Verbose("Hex formatting complete - Result length: {Length}, Result: {Result}",
                result.Length,
                result);
            return result;
        }

        /// <summary>
        /// Gets a human-readable file size string.
        /// </summary>
        /// <param name="bytes">The number of bytes.</param>
        /// <returns>The formatted file size string.</returns>
        public static string FormatFileSize(long bytes)
        {
            _logger.Verbose("Formatting file size - Bytes: {Bytes}", bytes);

            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
                _logger.Verbose("Converted to {Unit} - Size: {Size}, Order: {Order}",
                    sizes[order],
                    size,
                    order);
            }

            var result = $"{size:0.##} {sizes[order]}";
            _logger.Verbose("File size formatting complete - Result: {Result}", result);
            return result;
        }

        /// <summary>
        /// Gets a human-readable time span string.
        /// </summary>
        /// <param name="timeSpan">The time span.</param>
        /// <returns>The formatted time span string.</returns>
        public static string FormatTimeSpan(TimeSpan timeSpan)
        {
            _logger.Verbose("Formatting time span - TotalDays: {Days}, TotalHours: {Hours}, TotalMinutes: {Minutes}, TotalSeconds: {Seconds}, TotalMilliseconds: {Milliseconds}",
                timeSpan.TotalDays,
                timeSpan.TotalHours,
                timeSpan.TotalMinutes,
                timeSpan.TotalSeconds,
                timeSpan.TotalMilliseconds);

            string result;
            if (timeSpan.TotalDays >= 1)
                result = $"{timeSpan.TotalDays:F1} days";
            else if (timeSpan.TotalHours >= 1)
                result = $"{timeSpan.TotalHours:F1} hours";
            else if (timeSpan.TotalMinutes >= 1)
                result = $"{timeSpan.TotalMinutes:F1} minutes";
            else if (timeSpan.TotalSeconds >= 1)
                result = $"{timeSpan.TotalSeconds:F1} seconds";
            else
                result = $"{timeSpan.TotalMilliseconds:F0} ms";

            _logger.Verbose("Time span formatting complete - Result: {Result}", result);
            return result;
        }

        /// <summary>
        /// Gets a human-readable server status text.
        /// </summary>
        /// <param name="status">The server status.</param>
        /// <returns>The formatted server status text.</returns>
        public static string GetServerStatusText(ServerStatus status)
        {
            return status switch
            {
                ServerStatus.Online => "Online",
                ServerStatus.Offline => "Offline",
                ServerStatus.Maintenance => "Maintenance",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Formats a byte count as a human-readable string.
        /// </summary>
        /// <param name="bytes">The byte count.</param>
        /// <returns>The formatted byte count string.</returns>
        public static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            var result = $"{size:0.##} {sizes[order]}";
            _logger.Verbose("File size formatting complete - Result: {Result}", result);
            return result;
        }

        /// <summary>
        /// Formats a latency as a human-readable string.
        /// </summary>
        /// <param name="milliseconds">The latency in milliseconds.</param>
        /// <returns>The formatted latency string.</returns>
        public static string FormatLatency(double milliseconds)
        {
            if (milliseconds < 1)
                return $"{milliseconds:F3} ms";
            return $"{milliseconds:F1} ms";
        }

        /// <summary>
        /// Formats a percentage as a human-readable string.
        /// </summary>
        /// <param name="value">The percentage value.</param>
        /// <returns>The formatted percentage string.</returns>
        public static string FormatPercentage(double value)
        {
            return $"{value:F1}%";
        }

        /// <summary>
        /// Formats a date and time as a human-readable string.
        /// </summary>
        /// <param name="dateTime">The date and time.</param>
        /// <returns>The formatted date and time string.</returns>
        public static string FormatDateTime(DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
    }
}
