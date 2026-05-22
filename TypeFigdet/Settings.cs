using System.Windows.Input;
using System.Windows.Media;

namespace TypeFigdet
{
    public class Settings
    {
        // Font settings
        public string FontFamily { get; set; } = "Arial";
        public double FontSize { get; set; } = 48;
        public string FontColor { get; set; } = "#FF000000"; // Black
        public string TextAlignment { get; set; } = "Left";

        // Window settings
        public double WindowWidth { get; set; } = 600;
        public double WindowHeight { get; set; } = 300;
        public double WindowOpacity { get; set; } = 1.0;
        public string BackgroundColor { get; set; } = "#FFFFFFFF"; // White

        // Stats
        public bool ShowStats { get; set; } = false;

        // Global hotkey
        public ModifierKeys HotkeyModifiers { get; set; } = ModifierKeys.Control | ModifierKeys.Alt;
        public Key HotkeyKey { get; set; } = Key.T;
    }
}