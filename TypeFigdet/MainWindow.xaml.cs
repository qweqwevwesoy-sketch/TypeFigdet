using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation; 
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace TypeFigdet
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DispatcherTimer _clearTimer;
        private DispatcherTimer _statsTimer;
        private SettingsWindow _settingsWindow;
        private SettingsButtonWindow _settingsButtonWindow;
        private bool _isInitialized = false;
        private DateTime _typingStartTime;
        private bool _isTyping = false;
        public bool ShowStats { get; set; } = false;
        private GlobalHotKeyManager _hotKeyManager;

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll")]
        private static extern bool RemoveMenu(IntPtr hMenu, uint uPosition, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private const uint MF_BYCOMMAND = 0x00000000;
        private const uint SC_CLOSE = 0xF060;

        public MainWindow()
        {
            InitializeComponent();
            InitializeClearTimer();
            Loaded += (s, e) => 
            {
                // Load saved settings and apply them
                LoadSavedSettings();

                InputTextBox.Focus();
                RemoveCloseButton();
                CreateSettingsButton();
                _isInitialized = true;

                // Initialize hotkey manager and register hotkey from saved settings
                _hotKeyManager = new GlobalHotKeyManager(this);
                var settings = SettingsManager.Load();
                _hotKeyManager.RegisterHotKey(settings.HotkeyModifiers, settings.HotkeyKey, ToggleWindowVisibility);

                // Register this window with the app
                if (Application.Current is App app)
                {
                    app.RegisterWindow(this);
                }
            };
            Closed += (s, e) =>
            {
                // Save settings on close
                SaveCurrentSettings();

                // Unregister hotkey
                _hotKeyManager?.UnregisterHotKey();

                // Unregister when closed
                if (Application.Current is App app)
                {
                    app.UnregisterWindow(this);
                }
            };
        }

        public bool RegisterGlobalHotKey(ModifierKeys modifier, Key key)
        {
            if (_hotKeyManager != null)
            {
                _hotKeyManager.UnregisterHotKey();
                return _hotKeyManager.RegisterHotKey(modifier, key, ToggleWindowVisibility);
            }
            return false;
        }

        private void ToggleWindowVisibility()
        {
            if (this.IsVisible && this.IsActive)
            {
                this.Hide();
            }
            else
            {
                this.Show();
                this.Activate();
                this.Focus();
                this.WindowState = WindowState.Normal;
            }
        }

        private void CreateSettingsButton()
        {
            _settingsButtonWindow = new SettingsButtonWindow(this);
            _settingsButtonWindow.Show();
        }

        private void RemoveCloseButton()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            IntPtr hMenu = GetSystemMenu(hwnd, false);
            RemoveMenu(hMenu, SC_CLOSE, MF_BYCOMMAND);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void InitializeClearTimer() 
        {
            _clearTimer = new DispatcherTimer();
            _clearTimer.Interval = TimeSpan.FromSeconds(0.4);
            _clearTimer.Tick += (s, e) => ClearTextBox();

            _statsTimer = new DispatcherTimer();
            _statsTimer.Interval = TimeSpan.FromMilliseconds(100);
            _statsTimer.Tick += (s, e) => UpdateStats();
        }

        private void UpdateStats()
        {
            if (!_isTyping || !ShowStats) return;

            var elapsedSeconds = (DateTime.Now - _typingStartTime).TotalSeconds;
            if (elapsedSeconds < 1) return;

            int charCount = InputTextBox.Text.Length;
            int wordCount = InputTextBox.Text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;

            double wpm = (wordCount / elapsedSeconds) * 60;
            double cpm = (charCount / elapsedSeconds) * 60;

            StatsTextBlock.Text = $"WPM: {wpm:F0}  CPM: {cpm:F0}";
        }

        private void ClearTextBox()
        {
            InputTextBox.Clear();
            _statsTimer.Stop();
            _isTyping = false;
            StatsTextBlock.Text = "WPM: 0  CPM: 0";
            _clearTimer.Stop();
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void TextBox_TextChanged_1(object sender, TextChangedEventArgs e)
        {
            if (!_isTyping)
            {
                _isTyping = true;
                _typingStartTime = DateTime.Now;
                _statsTimer.Start();
            }

            _clearTimer.Stop();
            _clearTimer.Start();
        }



        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            OpenSettings();
        }

        public void OpenSettings()
        {
            if (_settingsWindow == null || !_settingsWindow.IsLoaded)
            {
                _settingsWindow = new SettingsWindow(this);
                _settingsWindow.Show();
            }
            else
            {
                _settingsWindow.Activate();
                _settingsWindow.Focus();
            }
        }

        private void LoadSavedSettings()
        {
            var settings = SettingsManager.Load();

            // Font settings
            try { InputTextBox.FontFamily = new FontFamily(settings.FontFamily); } catch { }
            InputTextBox.FontSize = settings.FontSize;
            try { InputTextBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.FontColor)); } catch { }
            if (Enum.TryParse<TextAlignment>(settings.TextAlignment, out var alignment))
                InputTextBox.TextAlignment = alignment;

            // Window settings
            Width = settings.WindowWidth;
            Height = settings.WindowHeight;
            Opacity = settings.WindowOpacity;
            try { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.BackgroundColor)); } catch { }

            // Stats
            ShowStats = settings.ShowStats;
            StatsTextBlock.Visibility = settings.ShowStats ? Visibility.Visible : Visibility.Collapsed;
        }

        public void SaveCurrentSettings()
        {
            var settings = new Settings
            {
                // Font settings
                FontFamily = InputTextBox.FontFamily.Source,
                FontSize = InputTextBox.FontSize,
                FontColor = (InputTextBox.Foreground as SolidColorBrush)?.Color.ToString() ?? "#FF000000",
                TextAlignment = InputTextBox.TextAlignment.ToString(),

                // Window settings
                WindowWidth = Width,
                WindowHeight = Height,
                WindowOpacity = Opacity,
                BackgroundColor = (Background as SolidColorBrush)?.Color.ToString() ?? "#FFFFFFFF",

                // Stats
                ShowStats = ShowStats,

                // Hotkey is saved separately via SettingsWindow
                HotkeyModifiers = _hotKeyManager?.CurrentModifiers ?? (ModifierKeys.Control | ModifierKeys.Alt),
                HotkeyKey = _hotKeyManager?.CurrentKey ?? Key.T
            };

            SettingsManager.Save(settings);
        }
    }
}
