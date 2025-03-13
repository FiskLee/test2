using Serilog;

namespace ArmaReforgerServerMonitor.Frontend.Configuration
{
    /// <summary>
    /// Configuration settings for window position and size.
    /// </summary>
    public class WindowSettings
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext<WindowSettings>();

        private double _width = 800;
        private double _height = 600;
        private double _left = 100;
        private double _top = 100;
        private bool _isMaximized = false;
        private System.Windows.WindowState _windowState = System.Windows.WindowState.Normal;

        /// <summary>
        /// Window width in pixels.
        /// </summary>
        public double Width
        {
            get => _width;
            set
            {
                _logger.Verbose("Updating window width - Old: {OldWidth}, New: {NewWidth}", _width, value);
                _width = value;
            }
        }

        /// <summary>
        /// Window height in pixels.
        /// </summary>
        public double Height
        {
            get => _height;
            set
            {
                _logger.Verbose("Updating window height - Old: {OldHeight}, New: {NewHeight}", _height, value);
                _height = value;
            }
        }

        /// <summary>
        /// Window left position in pixels.
        /// </summary>
        public double Left
        {
            get => _left;
            set
            {
                _logger.Verbose("Updating window left position - Old: {OldLeft}, New: {NewLeft}", _left, value);
                _left = value;
            }
        }

        /// <summary>
        /// Window top position in pixels.
        /// </summary>
        public double Top
        {
            get => _top;
            set
            {
                _logger.Verbose("Updating window top position - Old: {OldTop}, New: {NewTop}", _top, value);
                _top = value;
            }
        }

        /// <summary>
        /// Whether the window is maximized.
        /// </summary>
        public bool IsMaximized
        {
            get => _isMaximized;
            set
            {
                _logger.Verbose("Updating window maximized state - Old: {OldMaximized}, New: {NewMaximized}", _isMaximized, value);
                _isMaximized = value;
            }
        }

        /// <summary>
        /// The window state.
        /// </summary>
        public System.Windows.WindowState WindowState
        {
            get => _windowState;
            set
            {
                _logger.Verbose("Updating window state - Old: {OldState}, New: {NewState}", _windowState, value);
                _windowState = value;
            }
        }

        public WindowSettings()
        {
            _logger.Verbose("Initializing WindowSettings with default values - Width: {Width}, Height: {Height}, Left: {Left}, Top: {Top}, IsMaximized: {IsMaximized}, State: {State}",
                _width,
                _height,
                _left,
                _top,
                _isMaximized,
                _windowState);
        }

        public void LogSettings()
        {
            _logger.Verbose("Current Window Settings - Width: {Width}, Height: {Height}, Left: {Left}, Top: {Top}, IsMaximized: {IsMaximized}, State: {State}",
                _width,
                _height,
                _left,
                _top,
                _isMaximized,
                _windowState);
        }
    }
}