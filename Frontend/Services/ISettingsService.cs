using ArmaReforgerServerMonitor.Frontend.Configuration;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// Interface for managing application settings.
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Gets the application settings.
        /// </summary>
        AppSettings Settings { get; }

        /// <summary>
        /// Gets the theme settings.
        /// </summary>
        ThemeSettings Theme { get; }

        /// <summary>
        /// Saves the current settings to disk.
        /// </summary>
        void SaveSettings();

        /// <summary>
        /// Loads settings from disk.
        /// </summary>
        void LoadSettings();
    }
}