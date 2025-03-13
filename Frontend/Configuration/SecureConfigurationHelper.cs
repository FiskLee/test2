using Microsoft.Win32;
using Serilog;
using System;
using System.Security.Cryptography;
using System.Text;

namespace ArmaReforgerServerMonitor.Frontend.Configuration
{
    /// <summary>
    /// Helper class for securely storing and retrieving sensitive configuration values.
    /// Uses Windows Data Protection API (DPAPI) for encryption and Windows Registry for storage.
    /// </summary>
    /// <remarks>
    /// Security features:
    /// - Uses DPAPI for encryption, which is tied to the current user's credentials
    /// - Stores encrypted values in the current user's registry hive
    /// - Automatically handles encryption/decryption of sensitive data
    /// - Provides secure storage for API keys and other secrets
    /// 
    /// Note: This implementation is Windows-specific. For cross-platform support,
    /// additional implementations would be needed for other operating systems.
    /// </remarks>
    public static class SecureConfigurationHelper
    {
        /// <summary>
        /// Registry key path where encrypted values are stored
        /// </summary>
        private const string REGISTRY_KEY_PATH = @"SOFTWARE\ArmaReforgerServerMonitor";

        /// <summary>
        /// Registry value name for the Pastebin API key
        /// </summary>
        private const string PASTEBIN_API_KEY_NAME = "PastebinApiKey";

        /// <summary>
        /// Logger instance for this class
        /// </summary>
        private static readonly ILogger _logger = Log.ForContext(typeof(SecureConfigurationHelper));

        /// <summary>
        /// Securely stores the Pastebin API key using Windows DPAPI.
        /// The key is encrypted before being stored in the registry.
        /// </summary>
        /// <param name="apiKey">The API key to store</param>
        /// <exception cref="Exception">Thrown when storage fails</exception>
        /// <remarks>
        /// Storage process:
        /// 1. Convert API key to bytes
        /// 2. Encrypt using DPAPI
        /// 3. Convert to Base64 string
        /// 4. Store in registry
        /// </remarks>
        public static void StorePastebinApiKey(string apiKey)
        {
            try
            {
                // Encrypt the API key using DPAPI
                var encryptedData = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(apiKey),  // Convert string to bytes
                    null,                            // Optional entropy for additional security
                    DataProtectionScope.CurrentUser  // Tie encryption to current user
                );

                // Store the encrypted value in the registry
                using var key = Registry.CurrentUser.CreateSubKey(REGISTRY_KEY_PATH);
                key.SetValue(PASTEBIN_API_KEY_NAME, Convert.ToBase64String(encryptedData));

                _logger.Debug("Pastebin API key stored securely");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to store Pastebin API key");
                throw;
            }
        }

        /// <summary>
        /// Retrieves the securely stored Pastebin API key from the registry.
        /// The key is automatically decrypted using DPAPI.
        /// </summary>
        /// <returns>The decrypted API key, or null if not found or decryption fails</returns>
        /// <remarks>
        /// Retrieval process:
        /// 1. Read encrypted value from registry
        /// 2. Convert from Base64 to bytes
        /// 3. Decrypt using DPAPI
        /// 4. Convert back to string
        /// </remarks>
        public static string? GetPastebinApiKey()
        {
            try
            {
                // Open the registry key
                using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY_PATH);
                if (key == null) return null;

                // Get the encrypted value
                var encryptedData = key.GetValue(PASTEBIN_API_KEY_NAME) as string;
                if (string.IsNullOrEmpty(encryptedData)) return null;

                // Decrypt the value using DPAPI
                var decryptedBytes = ProtectedData.Unprotect(
                    Convert.FromBase64String(encryptedData),  // Convert Base64 to bytes
                    null,                                     // Same entropy as encryption
                    DataProtectionScope.CurrentUser           // Same scope as encryption
                );

                // Convert back to string
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve Pastebin API key");
                return null;
            }
        }
    }
}