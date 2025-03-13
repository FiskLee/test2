using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BattleNET
{
    public class SecurityManager
    {
        private readonly ILogger _logger;
        private readonly RateLimiter _rateLimiter;
        private readonly CommandValidator _validator;
        private readonly ConcurrentDictionary<string, int> _failedAttempts;
        private readonly ConcurrentDictionary<string, DateTime> _blockedIPs;
        private const int MAX_FAILED_ATTEMPTS = 5;
        private const int BLOCK_DURATION_MINUTES = 15;

        public SecurityManager(ILogger logger)
        {
            _logger = logger ?? Log.ForContext<SecurityManager>();
            _rateLimiter = new RateLimiter(_logger);
            _validator = new CommandValidator(_logger);
            _failedAttempts = new ConcurrentDictionary<string, int>();
            _blockedIPs = new ConcurrentDictionary<string, DateTime>();
        }

        public Task<bool> ValidateCommandAsync(string command, string ipAddress)
        {
            try
            {
                // Check if IP is blocked
                if (IsIPBlocked(ipAddress))
                {
                    _logger.Warning("Blocked IP {IP} attempted to execute command", ipAddress);
                    return Task.FromResult(false);
                }

                // Check rate limits
                if (!_rateLimiter.CheckLimitAsync(ipAddress).Result)
                {
                    _logger.Warning("Rate limit exceeded for IP {IP}", ipAddress);
                    return Task.FromResult(false);
                }

                // Validate command syntax
                if (!_validator.Validate(command))
                {
                    _logger.Warning("Invalid command syntax from IP {IP}", ipAddress);
                    return Task.FromResult(false);
                }

                // Check for potentially dangerous commands
                if (_validator.IsDangerous(command))
                {
                    _logger.Warning("Potentially dangerous command detected from IP {IP}", ipAddress);
                    RecordFailedAttempt(ipAddress);
                    return Task.FromResult(false);
                }

                // Reset failed attempts on successful command
                _failedAttempts.TryRemove(ipAddress, out _);
                return Task.FromResult(true);
            }
            catch (ArgumentNullException ex)
            {
                _logger.Error(ex, "ArgumentNullException occurred while validating command");
                return Task.FromResult(false);
            }
            catch (InvalidOperationException ex)
            {
                _logger.Error(ex, "InvalidOperationException occurred while validating command");
                return Task.FromResult(false);
            }
        }

        public string HashPassword(string password)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));
            byte[] salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hash);
        }

        public bool VerifyPassword(string password, string hashedPassword)
        {
            var hashedInput = HashPassword(password);
            return hashedInput == hashedPassword;
        }

        private bool IsIPBlocked(string ipAddress)
        {
            if (_blockedIPs.TryGetValue(ipAddress, out var blockTime))
            {
                if (DateTime.UtcNow < blockTime)
                {
                    return true;
                }
                else
                {
                    // Remove expired block
                    _blockedIPs.TryRemove(ipAddress, out _);
                    return false;
                }
            }
            return false;
        }

        private void RecordFailedAttempt(string ipAddress)
        {
            var attempts = _failedAttempts.AddOrUpdate(
                ipAddress,
                1,
                (_, count) => count + 1);

            if (attempts >= MAX_FAILED_ATTEMPTS)
            {
                BlockIP(ipAddress);
            }
        }

        private void BlockIP(string ipAddress)
        {
            var blockTime = DateTime.UtcNow.AddMinutes(BLOCK_DURATION_MINUTES);
            _blockedIPs.TryAdd(ipAddress, blockTime);
            _logger.Warning("IP {IP} blocked for {Minutes} minutes due to multiple failed attempts",
                ipAddress, BLOCK_DURATION_MINUTES);
        }

        public class RateLimiter
        {
            private readonly ILogger _logger;
            private readonly ConcurrentDictionary<string, Queue<DateTime>> _requestHistory;
            private const int MAX_REQUESTS_PER_MINUTE = 60;

            public RateLimiter(ILogger logger)
            {
                _logger = logger;
                _requestHistory = new ConcurrentDictionary<string, Queue<DateTime>>();
            }

            public Task<bool> CheckLimitAsync(string ipAddress)
            {
                var now = DateTime.UtcNow;
                var history = _requestHistory.GetOrAdd(ipAddress, _ => new Queue<DateTime>());

                // Remove old requests
                while (history.Count > 0 && (now - history.Peek()).TotalMinutes >= 1)
                {
                    history.Dequeue();
                }

                // Check if limit exceeded
                if (history.Count >= MAX_REQUESTS_PER_MINUTE)
                {
                    _logger.Warning("Rate limit exceeded for IP {IP}", ipAddress);
                    return Task.FromResult(false);
                }

                // Add new request
                history.Enqueue(now);
                return Task.FromResult(true);
            }
        }

        public class CommandValidator
        {
            private readonly ILogger _logger;
            private readonly HashSet<string> _dangerousCommands;

            public CommandValidator(ILogger logger)
            {
                _logger = logger;
                _dangerousCommands = new HashSet<string>
                {
                    "shutdown",
                    "restart",
                    "quit",
                    "exit",
                    "kill",
                    "delete",
                    "remove",
                    "drop",
                    "alter",
                    "update",
                    "insert",
                    "create"
                };
            }

            public bool Validate(string command)
            {
                if (string.IsNullOrWhiteSpace(command))
                {
                    return false;
                }

                // Basic syntax validation
                if (!command.StartsWith("/") && !command.StartsWith("\\"))
                {
                    return false;
                }

                // Remove leading slash
                command = command.TrimStart('/', '\\');

                // Check for minimum length
                if (command.Length < 2)
                {
                    return false;
                }

                // Check for valid characters
                if (!command.All(c => char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-' || c == '.'))
                {
                    return false;
                }

                return true;
            }

            public bool IsDangerous(string command)
            {
                if (string.IsNullOrWhiteSpace(command))
                {
                    return false;
                }

                // Remove leading slash
                command = command.TrimStart('/', '\\');

                // Split into command and arguments
                var parts = command.Split(new[] { ' ' }, 2);
                var cmd = parts[0].ToLowerInvariant();

                return _dangerousCommands.Contains(cmd);
            }
        }
    }
}