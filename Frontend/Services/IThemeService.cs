using System;
using System.Collections.Generic;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// Interface for managing application themes and visual styles.
    /// Handles theme switching, accent colors, and theme-related events.
    /// </summary>
    /// <remarks>
    /// Key responsibilities:
    /// - Managing application themes (Light/Dark)
    /// - Handling accent color changes
    /// - Notifying UI components of theme changes
    /// - Persisting theme preferences
    /// 
    /// This service integrates with MahApps.Metro for WPF theming.
    /// Theme changes are applied immediately and persisted between sessions.
    /// 
    /// Usage example:
    /// ```csharp
    /// _themeService.ThemeChanged += OnThemeChanged;
    /// _themeService.SetTheme("Dark", "Blue");
    /// ```
    /// </remarks>
    public interface IThemeService
    {
        /// <summary>
        /// Event that fires when the application theme changes.
        /// Subscribers can update their visual appearance in response.
        /// </summary>
        /// <remarks>
        /// This event is raised:
        /// - When SetTheme is called with new values
        /// - When theme is changed via system settings (if enabled)
        /// - When accent color is changed
        /// 
        /// Event handlers should update their visual elements accordingly.
        /// </remarks>
        event EventHandler? ThemeChanged;

        /// <summary>
        /// Gets a list of available application themes.
        /// </summary>
        /// <returns>
        /// List of theme names (e.g., "Light", "Dark") that can be used with SetTheme.
        /// The list is populated from MahApps.Metro available themes.
        /// </returns>
        /// <remarks>
        /// These themes control the overall light/dark appearance of the application.
        /// The list is typically static and defined by the theming framework.
        /// </remarks>
        List<string> GetAvailableThemes();

        /// <summary>
        /// Gets a list of available accent colors.
        /// </summary>
        /// <returns>
        /// List of accent color names (e.g., "Blue", "Red", "Green") that can be used with SetTheme.
        /// The list is populated from MahApps.Metro available accents.
        /// </returns>
        /// <remarks>
        /// Accent colors are used for:
        /// - Highlighting active elements
        /// - Buttons and controls
        /// - Selection indicators
        /// - Other accent UI elements
        /// </remarks>
        List<string> GetAvailableAccents();

        /// <summary>
        /// Sets the current application theme and accent color.
        /// </summary>
        /// <param name="theme">The theme name (from GetAvailableThemes)</param>
        /// <param name="accent">The accent color name (from GetAvailableAccents)</param>
        /// <remarks>
        /// This method:
        /// 1. Validates the theme and accent names
        /// 2. Applies the new theme to the application
        /// 3. Updates accent colors
        /// 4. Raises the ThemeChanged event
        /// 5. Persists the selection for future sessions
        /// 
        /// The change is immediate and affects all windows/controls.
        /// </remarks>
        void SetTheme(string theme, string accent);

        /// <summary>
        /// Gets the current theme and accent color.
        /// </summary>
        /// <returns>
        /// A tuple containing:
        /// - Theme: Current theme name
        /// - Accent: Current accent color name
        /// </returns>
        /// <remarks>
        /// Use this method to:
        /// - Check current theme settings
        /// - Synchronize UI controls with current theme
        /// - Save theme preferences
        /// 
        /// The values returned match the names from GetAvailableThemes/Accents.
        /// </remarks>
        (string Theme, string Accent) GetCurrentTheme();
    }
}