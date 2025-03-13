using System.Windows.Media;

namespace ArmaReforgerServerMonitor.Frontend.Models
{
    public interface IColorSchemeProvider
    {
        Color GetPrimaryColor();
        Color GetSecondaryColor();
        Color GetAccentColor();
        Color GetBackgroundColor();
        Color GetForegroundColor();
        Color GetErrorColor();
        Color GetSuccessColor();
        Color GetWarningColor();
        Color GetInfoColor();
    }

    public class ColorScheme : IColorSchemeProvider
    {
        public Color PrimaryColor { get; set; }
        public Color SecondaryColor { get; set; }
        public Color AccentColor { get; set; }
        public Color BackgroundColor { get; set; }
        public Color ForegroundColor { get; set; }
        public Color ErrorColor { get; set; }
        public Color SuccessColor { get; set; }
        public Color WarningColor { get; set; }
        public Color InfoColor { get; set; }

        public Color GetPrimaryColor() => PrimaryColor;
        public Color GetSecondaryColor() => SecondaryColor;
        public Color GetAccentColor() => AccentColor;
        public Color GetBackgroundColor() => BackgroundColor;
        public Color GetForegroundColor() => ForegroundColor;
        public Color GetErrorColor() => ErrorColor;
        public Color GetSuccessColor() => SuccessColor;
        public Color GetWarningColor() => WarningColor;
        public Color GetInfoColor() => InfoColor;

        public Color RetrieveBackgroundColor() => BackgroundColor;
        public Color RetrieveForegroundColor() => ForegroundColor;
        public Color RetrieveInfoColor() => InfoColor;
    }

    public class Theme
    {
        public Color BackgroundColor { get; set; }
        public Color ForegroundColor { get; set; }
        public Color AccentColor { get; set; }
        public string FontFamily { get; set; }
        public double FontSize { get; set; }

        public Theme()
        {
            BackgroundColor = Colors.White;
            ForegroundColor = Colors.Black;
            AccentColor = Colors.Blue;
            FontFamily = "Segoe UI";
            FontSize = 12;
        }

        public Theme(Color backgroundColor, Color foregroundColor, Color accentColor, string fontFamily, double fontSize)
        {
            BackgroundColor = backgroundColor;
            ForegroundColor = foregroundColor;
            AccentColor = accentColor;
            FontFamily = fontFamily;
            FontSize = fontSize;
        }
    }
}