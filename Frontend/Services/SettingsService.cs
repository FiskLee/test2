using ArmaReforgerServerMonitor.Frontend.Configuration;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// Implementation of the settings service that manages application settings.
    /// </summary>
    internal class SettingsService : ISettingsService, IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _settingsPath;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly SemaphoreSlim _settingsLock;
        private bool _isDisposed;

        private AppSettings _settings;

        public AppSettings Settings => _settings;
        public ThemeSettings Theme => _settings.Theme;

        public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

        /// <summary>
        /// Initializes a new instance of the SettingsService class.
        /// </summary>
        public SettingsService(IOptions<AppSettings> settings)
        {
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _logger = Log.ForContext<SettingsService>();
            _settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            _logger.Verbose("SettingsService initialized with settings path: {Path}", _settingsPath);

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            _settingsLock = new SemaphoreSlim(1, 1);

            _logger.Debug("Loading initial settings from file");
            LoadSettings();
        }

        /// <summary>
        /// Saves the current settings to disk.
        /// </summary>
        public async Task SaveSettingsAsync()
        {
            try
            {
                _logger.Verbose("Attempting to save settings to {Path}", _settingsPath);
                await _settingsLock.WaitAsync();

                _logger.Debug("Serializing settings to JSON");
                var json = JsonSerializer.Serialize(_settings, _jsonOptions);

                _logger.Verbose("Writing settings to file");
                await File.WriteAllTextAsync(_settingsPath, json).ConfigureAwait(false);

                _logger.Information("Settings saved successfully to {Path}", _settingsPath);
            }
            catch (IOException ex)
            {
                _logger.Error(ex, "Failed to save settings to {Path} - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    _settingsPath, ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
            finally
            {
                _settingsLock.Release();
            }
        }

        /// <summary>
        /// Loads settings from disk.
        /// </summary>
        public void LoadSettings()
        {
            try
            {
                _logger.Verbose("Loading settings from {Path}", _settingsPath);
                if (!File.Exists(_settingsPath))
                {
                    _logger.Warning("Settings file not found at {Path}, using defaults", _settingsPath);
                    return;
                }

                _logger.Debug("Reading settings file");
                var json = File.ReadAllText(_settingsPath);
                _logger.Verbose("Deserializing settings from JSON");
                var loadedSettings = JsonSerializer.Deserialize<AppSettings>(json);

                if (loadedSettings != null)
                {
                    _logger.Debug("Settings loaded successfully");
                    _settings = loadedSettings;
                    _logger.Information("Settings loaded from {Path} - Theme: {Theme}, Accent: {Accent}",
                        _settingsPath, _settings.Theme?.DefaultTheme, _settings.Theme?.DefaultAccent);
                }
                else
                {
                    _logger.Warning("Settings file exists but deserialization returned null");
                }
            }
            catch (IOException ex)
            {
                _logger.Error(ex, "Failed to load settings from {Path} - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    _settingsPath, ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        public void SaveSettings()
        {
            _logger.Verbose("Synchronous save settings called");
            SaveSettingsAsync().GetAwaiter().GetResult();
        }

        public async Task LoadSettingsAsync()
        {
            try
            {
                _logger.Verbose("Loading settings asynchronously from {Path}", _settingsPath);
                if (!File.Exists(_settingsPath))
                {
                    _logger.Information("Settings file not found at {Path}, using defaults", _settingsPath);
                    return;
                }

                _logger.Debug("Reading settings file asynchronously");
                var json = await File.ReadAllTextAsync(_settingsPath);
                _logger.Verbose("Deserializing settings from JSON");
                var loadedSettings = JsonSerializer.Deserialize<AppSettings>(json);

                if (loadedSettings != null)
                {
                    _logger.Debug("Settings loaded successfully");
                    _settings.UpdateFrom(loadedSettings);
                    _logger.Information("Settings loaded from {Path} - Theme: {Theme}, Accent: {Accent}",
                        _settingsPath, _settings.Theme?.DefaultTheme, _settings.Theme?.DefaultAccent);
                }
                else
                {
                    _logger.Warning("Settings file exists but deserialization returned null");
                }
            }
            catch (IOException ex)
            {
                _logger.Error(ex, "Failed to load settings from {Path} - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    _settingsPath, ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        public T GetSetting<T>(string key, T defaultValue)
        {
            try
            {
                _logger.Verbose("Getting setting {Key} with default value {DefaultValue}", key, defaultValue);
                var value = _settings.GetValue(key, defaultValue);
                _logger.Debug("Retrieved setting {Key} = {Value}", key, value);
                return value;
            }
            catch (IOException ex)
            {
                _logger.Error(ex, "Error getting setting {Key} - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    key, ex.GetType().Name, ex.Message, ex.StackTrace);
                return defaultValue;
            }
        }

        public void SetSetting<T>(string key, T value)
        {
            try
            {
                _logger.Verbose("Setting {Key} to {Value}", key, value);
                _settings.SetValue(key, value);
                _logger.Debug("Setting {Key} updated successfully", key);
                OnSettingsChanged(key, value ?? throw new ArgumentNullException(nameof(value)));
            }
            catch (IOException ex)
            {
                _logger.Error(ex, "Error setting value for key {Key} - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    key, ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        private void OnSettingsChanged(string key, object value)
        {
            try
            {
                _logger.Verbose("Raising SettingsChanged event for {Key} = {Value}", key, value);
                SettingsChanged?.Invoke(this, new SettingsChangedEventArgs(key, value));
                _logger.Debug("SettingsChanged event raised successfully");
            }
            catch (IOException ex)
            {
                _logger.Error(ex, "Error raising SettingsChanged event - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
            }
        }

        public void ResetToDefaults()
        {
            try
            {
                _logger.Verbose("Resetting settings to defaults");
                _settings.ResetToDefaults();
                _logger.Debug("Settings reset to defaults, saving to file");
                SaveSettings();
                _logger.Information("Settings reset to defaults successfully");
            }
            catch (IOException ex)
            {
                _logger.Error(ex, "Failed to reset settings to defaults - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _logger.Verbose("Disposing SettingsService");
                    _settingsLock?.Dispose();
                    _logger.Information("SettingsService disposed successfully");
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

    internal class SettingsChangedEventArgs : EventArgs
    {
        public string Key { get; }
        public object Value { get; }

        public SettingsChangedEventArgs(string key, object value)
        {
            Key = key;
            Value = value;
        }
    }
}