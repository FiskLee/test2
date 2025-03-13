using ArmaReforgerServerMonitor.Frontend.Configuration;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// Service for interacting with Pastebin API to share logs and error reports
    /// </summary>
    internal class PastebinService : IPastebinService, IDisposable
    {
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _apiUrl;
        private readonly AppSettings _settings;
        private bool _isDisposed;

        public PastebinService(IOptions<AppSettings> settings)
        {
            _logger = Log.ForContext<PastebinService>();
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _httpClient = new HttpClient();
            _apiKey = _settings.PastebinSettings.ApiKey ?? throw new InvalidOperationException("Pastebin API key is not configured.");
            _apiUrl = "https://pastebin.com/api/api_post.php";

            _logger.Verbose("PastebinService initialized with API URL: {ApiUrl}", _apiUrl);
        }

        /// <inheritdoc/>
        public async Task<string> CreatePasteAsync(string content, string title = "", string format = "text")
        {
            if (string.IsNullOrEmpty(content))
            {
                _logger.Warning("Attempted to create paste with null or empty content");
                throw new ArgumentException("Content cannot be null or empty", nameof(content));
            }

            try
            {
                _logger.Verbose("Creating paste with title: {Title}, format: {Format}, content length: {ContentLength}",
                    title, format, content.Length);

                var parameters = new Dictionary<string, string>
                {
                    ["api_dev_key"] = _apiKey,
                    ["api_option"] = "paste",
                    ["api_paste_code"] = content,
                    ["api_paste_name"] = title,
                    ["api_paste_format"] = format
                };

                _logger.Debug("Preparing POST request to Pastebin API");
                using var formContent = new FormUrlEncodedContent(parameters);
                using var response = await _httpClient.PostAsync(new Uri(_apiUrl), formContent).ConfigureAwait(false);

                _logger.Verbose("Received response with status code: {StatusCode}", response.StatusCode);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (result.StartsWith("Bad API request", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Error("Pastebin API returned error: {Error}", result);
                    throw new PastebinException($"Pastebin API error: {result}", new Exception("API error"));
                }

                _logger.Information("Successfully created paste with key: {PasteKey}", result);
                return result;
            }
            catch (HttpRequestException ex)
            {
                _logger.Error(ex, "HTTP request failed while creating paste - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
            catch (TaskCanceledException ex)
            {
                _logger.Error(ex, "Task was canceled while creating paste - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error creating paste - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw new PastebinException("Error message", ex);
            }
        }

        public async Task<string> GetPasteAsync(string pasteKey)
        {
            if (string.IsNullOrEmpty(pasteKey))
            {
                _logger.Warning("Attempted to get paste with null or empty paste key");
                throw new ArgumentException("Paste key cannot be null or empty", nameof(pasteKey));
            }

            try
            {
                _logger.Verbose("Retrieving paste with key: {PasteKey}", pasteKey);
                var url = $"https://pastebin.com/raw/{pasteKey}";

                _logger.Debug("Sending GET request to {Url}", url);
                using var response = await _httpClient.GetAsync(url).ConfigureAwait(false);

                _logger.Verbose("Received response with status code: {StatusCode}", response.StatusCode);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.Information("Successfully retrieved paste with key: {PasteKey}, content length: {ContentLength}",
                    pasteKey, content.Length);
                return content;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving paste {PasteKey} - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    pasteKey, ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        public async Task DeletePasteAsync(string pasteKey)
        {
            if (string.IsNullOrEmpty(pasteKey))
            {
                _logger.Warning("Attempted to delete paste with null or empty paste key");
                throw new ArgumentException("Paste key cannot be null or empty", nameof(pasteKey));
            }

            try
            {
                _logger.Verbose("Deleting paste with key: {PasteKey}", pasteKey);
                var parameters = new Dictionary<string, string>
                {
                    ["api_dev_key"] = _apiKey,
                    ["api_option"] = "delete",
                    ["api_paste_key"] = pasteKey
                };

                _logger.Debug("Preparing POST request to delete paste");
                using var formContent = new FormUrlEncodedContent(parameters);
                using var response = await _httpClient.PostAsync(_apiUrl, formContent).ConfigureAwait(false);

                _logger.Verbose("Received response with status code: {StatusCode}", response.StatusCode);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (result.StartsWith("Bad API request", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Error("Pastebin API returned error: {Error}", result);
                    throw new PastebinException($"Pastebin API error: {result}", new Exception("API error"));
                }

                _logger.Information("Successfully deleted paste with key: {PasteKey}", pasteKey);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error deleting paste {PasteKey} - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    pasteKey, ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        private string GeneratePasteKey()
        {
            _logger.Verbose("Generating new paste key");
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[8];
            rng.GetBytes(bytes);

            var result = new StringBuilder(8);
            foreach (var b in bytes)
            {
                result.Append(chars[b % chars.Length]);
            }
            _logger.Debug("Generated paste key: {PasteKey}", result.ToString());
            return result.ToString();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _logger.Verbose("Disposing PastebinService");
                    _httpClient?.Dispose();
                    _logger.Information("PastebinService disposed successfully");
                }
                _isDisposed = true;
                GC.SuppressFinalize(this);
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }

    public class PastebinException : Exception
    {
        public PastebinException() { }
        public PastebinException(string message, Exception innerException) : base(message, innerException) { }
    }
}