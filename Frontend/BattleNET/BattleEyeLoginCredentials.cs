/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 * BattleNET v1.3.4 - BattlEye Library and Client            *
 *                                                         *
 *  Copyright (C) 2018 by it's authors.                    *
 *  Some rights reserved. See license.txt, authors.txt.    *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

using Serilog;
using System;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;

namespace BattleNET
{
    /// <summary>
    /// Represents the login credentials for connecting to a BattlEye server.
    /// This class is immutable to ensure thread safety and data consistency.
    /// </summary>
    public sealed class BattlEyeLoginCredentials : IEquatable<BattlEyeLoginCredentials>
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext<BattlEyeLoginCredentials>();

        /// <summary>
        /// Gets the server host IP address.
        /// </summary>
        [Required(ErrorMessage = "Host IP address is required")]
        public IPAddress Host { get; }

        /// <summary>
        /// Gets the server port number.
        /// </summary>
        [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
        public int Port { get; }

        /// <summary>
        /// Gets the RCON password.
        /// </summary>
        [Required(ErrorMessage = "Password is required")]
        [StringLength(32, MinimumLength = 1, ErrorMessage = "Password must be between 1 and 32 characters")]
        public string Password { get; }

        /// <summary>
        /// Initializes a new instance of the BattlEyeLoginCredentials class.
        /// </summary>
        /// <param name="host">The server host IP address.</param>
        /// <param name="port">The server port number.</param>
        /// <param name="password">The RCON password.</param>
        /// <exception cref="ArgumentNullException">Thrown when host or password is null.</exception>
        /// <exception cref="ArgumentException">Thrown when port is invalid or password is empty.</exception>
        public BattlEyeLoginCredentials(IPAddress host, int port, string password)
        {
            _logger.Verbose("Creating BattlEyeLoginCredentials with - Host: {Host}, Port: {Port}, Password length: {PasswordLength}",
                host,
                port,
                password.Length);

            if (host == null)
            {
                _logger.Verbose("Host cannot be null");
                throw new ArgumentNullException(nameof(host));
            }

            _logger.Verbose("Validating host IP address - AddressFamily: {AddressFamily}, IsIPv4MappedToIPv6: {IsIPv4MappedToIPv6}",
                host.AddressFamily,
                host.IsIPv4MappedToIPv6);

            // Only log IPv6-specific properties if it's an IPv6 address
            if (host.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                _logger.Verbose("IPv6 address detected - ScopeId: {ScopeId}", host.ScopeId);
            }
            else if (host.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                _logger.Verbose("IPv4 address detected");
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                _logger.Verbose("Password cannot be null or empty");
                throw new ArgumentException("Password cannot be null or empty", nameof(password));
            }

            if (port < 1 || port > 65535)
            {
                _logger.Verbose("Invalid port number: {Port}", port);
                throw new ArgumentException("Port must be between 1 and 65535", nameof(port));
            }

            _logger.Verbose("Setting credentials - Host: {Host}, Port: {Port}, Password length: {PasswordLength}, Password hash: {PasswordHash}",
                host,
                port,
                password.Length,
                BitConverter.ToString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(password))));

            Host = host;
            Port = port;
            Password = password;
        }

        /// <summary>
        /// Creates a new instance of BattlEyeLoginCredentials from a host string.
        /// </summary>
        /// <param name="hostString">The host string in the format "ip:port".</param>
        /// <param name="password">The RCON password.</param>
        /// <returns>A new BattlEyeLoginCredentials instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when hostString or password is null.</exception>
        /// <exception cref="ArgumentException">Thrown when hostString format is invalid.</exception>
        public static BattlEyeLoginCredentials FromHostString(string hostString, string password)
        {
            _logger.Verbose("Creating BattlEyeLoginCredentials from host string: {HostString}, Password length: {PasswordLength}",
                hostString,
                password.Length);

            if (string.IsNullOrWhiteSpace(hostString))
            {
                _logger.Verbose("Host string cannot be null or empty");
                throw new ArgumentException("Host string cannot be null or empty", nameof(hostString));
            }

            var parts = hostString.Split(':');
            if (parts.Length != 2)
            {
                _logger.Verbose("Invalid host string format: {HostString}, Parts count: {PartsCount}",
                    hostString,
                    parts.Length);
                throw new ArgumentException("Invalid host string format. Expected 'ip:port'", nameof(hostString));
            }

            _logger.Verbose("Parsing host string parts - IP part: {IpPart}, Port part: {PortPart}",
                parts[0],
                parts[1]);

            if (!IPAddress.TryParse(parts[0], out var host))
            {
                _logger.Verbose("Invalid IP address in host string: {IpAddress}", parts[0]);
                throw new ArgumentException("Invalid IP address", nameof(hostString));
            }

            _logger.Verbose("Successfully parsed IP address - AddressFamily: {AddressFamily}, IsIPv4MappedToIPv6: {IsIPv4MappedToIPv6}",
                host.AddressFamily,
                host.IsIPv4MappedToIPv6);

            // Only log IPv6-specific properties if it's an IPv6 address
            if (host.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                _logger.Verbose("IPv6 address detected - ScopeId: {ScopeId}", host.ScopeId);
            }
            else if (host.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                _logger.Verbose("IPv4 address detected");
            }

            if (!int.TryParse(parts[1], out var port) || port < 1 || port > 65535)
            {
                _logger.Verbose("Invalid port number in host string: {Port}", parts[1]);
                throw new ArgumentException("Invalid port number", nameof(hostString));
            }

            _logger.Verbose("Successfully parsed host string - Host: {Host}, Port: {Port}, Password length: {PasswordLength}",
                host,
                port,
                password.Length);

            return new BattlEyeLoginCredentials(host, port, password);
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        public override bool Equals(object? obj)
        {
            _logger.Verbose("Comparing BattlEyeLoginCredentials with object of type: {Type}",
                obj?.GetType().Name ?? "null");
            return Equals(obj as BattlEyeLoginCredentials);
        }

        /// <summary>
        /// Determines whether the specified BattlEyeLoginCredentials is equal to the current BattlEyeLoginCredentials.
        /// </summary>
        /// <param name="other">The BattlEyeLoginCredentials to compare with the current BattlEyeLoginCredentials.</param>
        /// <returns>true if the specified BattlEyeLoginCredentials is equal to the current BattlEyeLoginCredentials; otherwise, false.</returns>
        public bool Equals(BattlEyeLoginCredentials? other)
        {
            if (other is null)
            {
                _logger.Verbose("Comparing with null BattlEyeLoginCredentials");
                return false;
            }

            _logger.Verbose("Comparing BattlEyeLoginCredentials - Host: {Host1} vs {Host2}, Port: {Port1} vs {Port2}, Password length: {PasswordLength1} vs {PasswordLength2}",
                Host,
                other.Host,
                Port,
                other.Port,
                Password.Length,
                other.Password.Length);

            var result = Host.Equals(other.Host) &&
                        Port == other.Port &&
                        Password == other.Password;

            _logger.Verbose("Comparison result: {Result}", result);
            return result;
        }

        /// <summary>
        /// Serves as the default hash function.
        /// </summary>
        /// <returns>A hash code for the current object.</returns>
        public override int GetHashCode()
        {
            var hashCode = HashCode.Combine(Host, Port, Password);
            _logger.Verbose("Generated hash code: {HashCode}", hashCode);
            return hashCode;
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            var result = $"{Host}:{Port}";
            _logger.Verbose("Generated string representation: {Result}", result);
            return result;
        }
    }
}