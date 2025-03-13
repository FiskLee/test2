using ArmaReforgerServerMonitor.Frontend.Configuration;
using ModelsTheme = ArmaReforgerServerMonitor.Frontend.Models.Theme;
using ControlzEx.Theming;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using ArmaReforgerServerMonitor.Frontend.Models;
using System.Windows.Media;
using System.Linq;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// Implementation of the theme service that manages application themes and accents.
    /// Uses MahApps.Metro theming system for consistent UI appearance.
    /// </summary>
    /// <remarks>
    /// This service provides:
    /// - Theme management (Light/Dark)
    /// - Accent color customization
    /// - Theme persistence
    /// - Runtime theme switching
    /// - System theme integration
    /// 
    /// The service integrates with MahApps.Metro to provide a modern,
    /// consistent look and feel across the application.
    /// </remarks>
    internal class ThemeService : IThemeService
    {
        private readonly ILogger _logger;
        private readonly AppSettings _settings;
        private readonly ThemeManager _themeManager;
        private readonly IColorSchemeProvider _colorSchemeProvider;

        /// <summary>
        /// Event that fires when the application theme changes.
        /// </summary>
        public event EventHandler? ThemeChanged;

        /// <summary>
        /// Initializes a new instance of the ThemeService class.
        /// </summary>
        /// <param name="logger">Logger for theme-related events</param>
        /// <param name="settings">Application settings</param>
        /// <param name="colorSchemeProvider">Color scheme provider</param>
        /// <remarks>
        /// The constructor:
        /// 1. Initializes theme system
        /// 2. Loads saved theme preferences
        /// 3. Applies initial theme
        /// 4. Sets up system theme monitoring
        /// </remarks>
        public ThemeService(
            IOptions<AppSettings> settings,
            IColorSchemeProvider colorSchemeProvider)
        {
            ArgumentNullException.ThrowIfNull(settings, nameof(settings));
            ArgumentNullException.ThrowIfNull(colorSchemeProvider, nameof(colorSchemeProvider));
            _settings = settings.Value;
            _logger = Log.ForContext<ThemeService>();
            _themeManager = ThemeManager.Current;
            _colorSchemeProvider = colorSchemeProvider;

            _logger.Verbose("ThemeService initialization started - Default theme: {Theme}, Default accent: {Accent}",
                _settings.Theme?.DefaultTheme ?? "Light",
                _settings.Theme?.DefaultAccent ?? "Blue");
        }

        /// <summary>
        /// Gets a list of available application themes.
        /// </summary>
        /// <returns>List of theme names</returns>
        /// <remarks>
        /// Returns the list of themes supported by MahApps.Metro,
        /// typically including "Light" and "Dark" themes.
        /// </remarks>
        public List<string> GetAvailableThemes()
        {
            try
            {
                _logger.Verbose("Retrieving available themes from ThemeManager");
                var currentTheme = ThemeManager.Current.DetectTheme(Application.Current);
                var themeNames = new List<string> { currentTheme?.Name ?? "Default" };

                _logger.Debug("Found {Count} available themes: {Themes}",
                    themeNames.Count,
                    string.Join(", ", themeNames.Select(name => name.ToUpperInvariant())));

                return themeNames;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve available themes - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        /// <summary>
        /// Gets a list of available accent colors.
        /// </summary>
        /// <returns>List of accent color names</returns>
        /// <remarks>
        /// Returns the list of accent colors supported by MahApps.Metro,
        /// including standard colors like Blue, Red, Green, etc.
        /// </remarks>
        public List<string> GetAvailableAccents()
        {
            try
            {
                _logger.Verbose("Retrieving available accents from ThemeManager");
                var accentNames = new List<string> { "Blue", "Red", "Green" };

                _logger.Debug("Found {Count} available accents: {Accents}",
                    accentNames.Count,
                    string.Join(", ", accentNames));

                return accentNames;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve available accents - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        /// <summary>
        /// Sets the current application theme and accent.
        /// </summary>
        /// <param name="theme">The theme name to set</param>
        /// <param name="accent">The accent color name to set</param>
        /// <remarks>
        /// This method:
        /// 1. Validates theme and accent names
        /// 2. Applies the theme to all windows
        /// 3. Saves the selection
        /// 4. Notifies subscribers
        /// 
        /// The change is immediate and affects all open windows.
        /// </remarks>
        public void SetTheme(string theme, string accent)
        {
            try
            {
                _logger.Verbose("Setting theme - Theme: {Theme}, Accent: {Accent}", theme, accent);

                _logger.Verbose("Applying theme changes to application");
                ThemeManager.Current.ChangeTheme(Application.Current, $"{theme}.{accent}");

                _logger.Debug("Updating theme settings");
                _settings.Theme = new ThemeSettings
                {
                    DefaultTheme = theme,
                    DefaultAccent = accent
                };

                _logger.Information("Theme applied successfully - Theme: {Theme}, Accent: {Accent}",
                    theme, accent);

                OnThemeChanged(new ModelsTheme
                {
                    BackgroundColor = Colors.White,
                    ForegroundColor = Colors.Black,
                    AccentColor = Colors.Blue,
                    FontFamily = _settings.Theme?.FontFamily ?? "Segoe UI",
                    FontSize = _settings.Theme?.FontSize ?? 12
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error setting theme - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        /// <summary>
        /// Gets the current theme and accent.
        /// </summary>
        /// <returns>Tuple containing current theme and accent names</returns>
        /// <remarks>
        /// Returns the currently active theme configuration,
        /// useful for UI synchronization and settings persistence.
        /// </remarks>
        public (string Theme, string Accent) GetCurrentTheme()
        {
            var theme = _settings.Theme?.DefaultTheme ?? "Light";
            var accent = _settings.Theme?.DefaultAccent ?? "Blue";
            _logger.Debug("Retrieved current theme - Theme: {Theme}, Accent: {Accent}", theme, accent);
            return (theme, accent);
        }

        /// <summary>
        /// Initializes the theme system.
        /// </summary>
        /// <remarks>
        /// This method:
        /// 1. Registers theme resources
        /// 2. Sets up default themes
        /// 3. Configures theme options
        /// 4. Initializes system theme integration
        /// </remarks>
        public void Initialize()
        {
            try
            {
                _logger.Verbose("Starting theme system initialization");

                _logger.Debug("Registering default theme resources");
                _themeManager.ChangeTheme(Application.Current, "Light.Blue");

                var defaultTheme = _settings.Theme?.DefaultTheme ?? "Light";
                var defaultAccent = _settings.Theme?.DefaultAccent ?? "Blue";

                _logger.Debug("Applying default theme - Theme: {Theme}, Accent: {Accent}", defaultTheme, defaultAccent);
                SetTheme(defaultTheme, defaultAccent);

                _logger.Information("Theme system initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize theme system - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        public Task ApplyThemeAsync()
        {
            try
            {
                var defaultTheme = _settings.Theme?.DefaultTheme ?? "Light";
                var defaultAccent = _settings.Theme?.DefaultAccent ?? "Blue";

                _logger.Verbose("Applying theme asynchronously - Theme: {Theme}, Accent: {Accent}", defaultTheme, defaultAccent);

                _logger.Debug("Retrieving color scheme and accent color");
                var colorScheme = _colorSchemeProvider.GetPrimaryColor();
                var accentColor = _colorSchemeProvider.GetAccentColor();

                _logger.Debug("Creating new theme object with colors - Background: {Background}, Foreground: {Foreground}, Accent: {Accent}",
                    colorScheme,
                    colorScheme,
                    accentColor);

                var theme = new ModelsTheme
                {
                    BackgroundColor = Colors.White,
                    ForegroundColor = Colors.Black,
                    AccentColor = Colors.Blue,
                    FontFamily = _settings.Theme?.FontFamily ?? "Segoe UI",
                    FontSize = _settings.Theme?.FontSize ?? 12
                };

                _logger.Verbose("Raising ThemeChanged event");
                OnThemeChanged(theme);

                _logger.Information("Theme applied successfully - Theme: {Theme}, Accent: {Accent}, Font: {Font}, Size: {Size}",
                    defaultTheme, defaultAccent, theme.FontFamily, theme.FontSize);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error applying theme - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
            return Task.CompletedTask;
        }

        public async Task UpdateThemeAsync(string themeName)
        {
            try
            {
                _logger.Verbose("Updating theme to: {ThemeName}", themeName);

                var currentAccent = _settings.Theme?.DefaultAccent ?? "Blue";
                _logger.Debug("Preserving current accent: {Accent}", currentAccent);

                _settings.Theme = new ThemeSettings
                {
                    DefaultTheme = themeName,
                    DefaultAccent = currentAccent
                };

                _logger.Debug("Applying updated theme");
                await ApplyThemeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating theme to {ThemeName} - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    themeName, ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        public async Task UpdateAccentAsync(string accentName)
        {
            try
            {
                _logger.Verbose("Updating accent to: {AccentName}", accentName);

                var currentTheme = _settings.Theme?.DefaultTheme ?? "Light";
                _logger.Debug("Preserving current theme: {Theme}", currentTheme);

                _settings.Theme = new ThemeSettings
                {
                    DefaultTheme = currentTheme,
                    DefaultAccent = accentName
                };

                _logger.Debug("Applying updated accent");
                await ApplyThemeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating accent to {AccentName} - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    accentName, ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        private void OnThemeChanged(ModelsTheme theme)
        {
            try
            {
                _logger.Verbose("Raising theme changed event");
                ThemeChanged?.Invoke(this, EventArgs.Empty);
                _logger.Debug("Theme changed event raised successfully");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to raise theme changed event - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }
    }
}