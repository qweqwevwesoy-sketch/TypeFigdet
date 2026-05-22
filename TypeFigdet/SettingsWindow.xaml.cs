using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace TypeFigdet
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    // -----------------------------------------------------------------
    // COM interop types for creating .lnk files without IWshRuntimeLibrary
    // -----------------------------------------------------------------

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    internal class ShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    internal interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxLen, out IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxLen);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxLen);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxLen);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out ushort pwHotkey);
        void SetHotkey(ushort wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchMaxLen, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    internal interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        void IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, int dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder ppszFileName);
    }

    public partial class SettingsWindow : Window
    {
        private MainWindow _mainWindow;
        private ModifierKeys _currentModifier = ModifierKeys.Control | ModifierKeys.Alt;
        private Key _currentKey = Key.T;
        private bool _isRecording = false;

        // Constants for the global launch shortcut
        private const string ShortcutFileName = "TypeFigdet.lnk";
        private const string ShortcutDescription = "Launch TypeFigdet";

        public SettingsWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            InitializeSettings();

            Loaded += (s, e) =>
            {
                if (Application.Current is App app)
                    app.RegisterWindow(this);
            };
            Closed += (s, e) =>
            {
                if (Application.Current is App app)
                    app.UnregisterWindow(this);
            };
        }

        private void InitializeSettings()
        {
            // Populate font families
            foreach (var family in Fonts.SystemFontFamilies)
            {
                FontFamilyComboBox.Items.Add(family.Source);
            }
            FontFamilyComboBox.SelectedItem = _mainWindow.InputTextBox.FontFamily.Source;
            FontFamilyComboBox.SelectionChanged += FontFamilyComboBox_SelectionChanged;

            // Font size
            FontSizeSlider.Value = _mainWindow.InputTextBox.FontSize;
            FontSizeSlider.ValueChanged += FontSizeSlider_ValueChanged;
            FontSizeLabel.Text = $"{(int)FontSizeSlider.Value} pt";

            // Font color
            var fontColor = (_mainWindow.InputTextBox.Foreground as SolidColorBrush)?.Color ?? Colors.Black;
            ColorLabel.Text = fontColor.ToString();
            ColorLabel.Background = new SolidColorBrush(fontColor);

            // Text alignment
            TextAlignmentComboBox.SelectedValuePath = "Tag";
            TextAlignmentComboBox.SelectedValue = _mainWindow.InputTextBox.TextAlignment.ToString();
            TextAlignmentComboBox.SelectionChanged += TextAlignmentComboBox_SelectionChanged;

            // Window size
            WindowWidthSlider.Value = _mainWindow.Width;
            WindowWidthSlider.ValueChanged += WindowWidthSlider_ValueChanged;
            WindowWidthLabel.Text = $"{(int)WindowWidthSlider.Value}";

            WindowHeightSlider.Value = _mainWindow.Height;
            WindowHeightSlider.ValueChanged += WindowHeightSlider_ValueChanged;
            WindowHeightLabel.Text = $"{(int)WindowHeightSlider.Value}";

            // Background color
            var bgColor = (_mainWindow.Background as SolidColorBrush)?.Color ?? Colors.White;
            BackgroundColorLabel.Text = bgColor.ToString();
            BackgroundColorLabel.Background = new SolidColorBrush(bgColor);

            // Opacity
            OpacitySlider.Value = _mainWindow.Opacity;
            OpacitySlider.ValueChanged += OpacitySlider_ValueChanged;
            OpacityLabel.Text = $"{(int)(OpacitySlider.Value * 100)}%";

            // Stats
            ShowStatsCheckBox.IsChecked = _mainWindow.ShowStats;
            ShowStatsCheckBox.Checked += (s, e) => { _mainWindow.ShowStats = true; _mainWindow.StatsTextBlock.Visibility = Visibility.Visible; };
            ShowStatsCheckBox.Unchecked += (s, e) => { _mainWindow.ShowStats = false; _mainWindow.StatsTextBlock.Visibility = Visibility.Collapsed; };
        }

        private void FontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontFamilyComboBox.SelectedItem != null)
            {
                _mainWindow.InputTextBox.FontFamily = new FontFamily(FontFamilyComboBox.SelectedItem.ToString());
            }
        }

        private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _mainWindow.InputTextBox.FontSize = e.NewValue;
            FontSizeLabel.Text = $"{(int)e.NewValue} pt";
        }

        private void TextAlignmentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TextAlignmentComboBox.SelectedValue is string alignment)
            {
                if (Enum.TryParse<TextAlignment>(alignment, out var textAlignment))
                {
                    _mainWindow.InputTextBox.TextAlignment = textAlignment;
                }
            }
        }

        private void ColorPickerButton_Click(object sender, RoutedEventArgs e)
        {
            var colorPicker = new ColorPickerWindow();
            if (colorPicker.ShowDialog() == true)
            {
                var selectedColor = colorPicker.SelectedColor;
                _mainWindow.InputTextBox.Foreground = new SolidColorBrush(selectedColor);
                ColorLabel.Text = selectedColor.ToString();
                ColorLabel.Background = new SolidColorBrush(selectedColor);
            }
        }

        private void WindowWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _mainWindow.Width = e.NewValue;
            WindowWidthLabel.Text = $"{(int)e.NewValue}";
        }

        private void WindowHeightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _mainWindow.Height = e.NewValue;
            WindowHeightLabel.Text = $"{(int)e.NewValue}";
        }

        private void BackgroundColorPickerButton_Click(object sender, RoutedEventArgs e)
        {
            var colorPicker = new ColorPickerWindow();
            if (colorPicker.ShowDialog() == true)
            {
                var selectedColor = colorPicker.SelectedColor;
                _mainWindow.Background = new SolidColorBrush(selectedColor);
                BackgroundColorLabel.Text = selectedColor.ToString();
                BackgroundColorLabel.Background = new SolidColorBrush(selectedColor);
            }
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _mainWindow.Opacity = e.NewValue;
            OpacityLabel.Text = $"{(int)(e.NewValue * 100)}%";
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            // Save all settings to disk
            _mainWindow.SaveCurrentSettings();
            _mainWindow.InputTextBox.Focus();
            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void RecordHotKeyButton_Click(object sender, RoutedEventArgs e)
        {
            _isRecording = true;
            RecordHotKeyButton.Content = "🎤 Listening...";
            RecordHotKeyButton.IsEnabled = false;
            this.PreviewKeyDown += HotKeyRecorder_PreviewKeyDown;
            this.PreviewMouseDown += HotKeyRecorder_PreviewMouseDown;
        }

        private void HotKeyRecorder_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_isRecording) return;

            // Ignore modifier-only keys
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LWin || e.Key == Key.RWin)
            {
                return;
            }

            e.Handled = true;
            RecordHotKey(e.Key);
        }

        private void HotKeyRecorder_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isRecording) return;

            e.Handled = true;
            MessageBox.Show("Mouse buttons cannot be used as global hotkeys. Please use a keyboard key with modifiers (e.g., Ctrl+Alt+K).", "Mouse Button Not Supported", MessageBoxButton.OK, MessageBoxImage.Information);

            HotKeyTextBlock.Text = "No hotkey recorded";
            RecordHotKeyButton.IsEnabled = true;
            _isRecording = false;
            this.PreviewKeyDown -= HotKeyRecorder_PreviewKeyDown;
            this.PreviewMouseDown -= HotKeyRecorder_PreviewMouseDown;
        }

        private void RecordHotKey(Key key)
        {
            _currentModifier = ModifierKeys.None;
            _currentKey = key;

            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                _currentModifier |= ModifierKeys.Control;
            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                _currentModifier |= ModifierKeys.Alt;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                _currentModifier |= ModifierKeys.Shift;
            if ((Keyboard.Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows)
                _currentModifier |= ModifierKeys.Windows;

            // Build display string
            string keyText = "";
            if ((_currentModifier & ModifierKeys.Control) == ModifierKeys.Control) keyText += "Ctrl+";
            if ((_currentModifier & ModifierKeys.Alt) == ModifierKeys.Alt) keyText += "Alt+";
            if ((_currentModifier & ModifierKeys.Shift) == ModifierKeys.Shift) keyText += "Shift+";
            if ((_currentModifier & ModifierKeys.Windows) == ModifierKeys.Windows) keyText += "Win+";
            keyText += key.ToString();

            HotKeyTextBlock.Text = keyText;

            StopRecording();
        }

        private void StopRecording()
        {
            _isRecording = false;
            this.PreviewKeyDown -= HotKeyRecorder_PreviewKeyDown;
            this.PreviewMouseDown -= HotKeyRecorder_PreviewMouseDown;
            RecordHotKeyButton.Content = "🎙️ Record Hotkey";
            RecordHotKeyButton.IsEnabled = true;
        }

        private void SaveHotKeyButton_Click(object sender, RoutedEventArgs e)
        {
            // Validation
            if (_currentKey == Key.None)
            {
                MessageBox.Show("Please record a hotkey first by clicking the Record Hotkey button.", "No Hotkey Recorded", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_currentModifier == ModifierKeys.None)
            {
                MessageBox.Show("Please select at least one modifier key (Ctrl, Alt, Shift, or Win).", "No Modifier Key", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_currentKey == Key.LWin || _currentKey == Key.RWin || _currentKey == Key.Apps ||
                _currentKey == Key.BrowserBack || _currentKey == Key.BrowserForward)
            {
                MessageBox.Show("This key cannot be used as a global hotkey. Please use a regular keyboard key.", "Invalid Key", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 1. Register the hotkey with the running application
            bool success = _mainWindow.RegisterGlobalHotKey(_currentModifier, _currentKey);

            // 2. If registration succeeded, create/update the Windows shortcut (.lnk) so the hotkey works even when the app is closed
            if (success)
            {
                bool shortcutSuccess = CreateGlobalShortcut(_currentModifier, _currentKey);
                if (!shortcutSuccess)
                {
                    MessageBox.Show("Hotkey registered, but the Windows shortcut (launch from closed state) could not be created. Try running as administrator.",
                                    "Shortcut Creation Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    // Show success message with explanation
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show($"Hotkey registered successfully!\n\nYou can now launch the app from anywhere using {HotKeyTextBlock.Text} – even when the app is completely closed.",
                                        "Hotkey Applied", MessageBoxButton.OK, MessageBoxImage.Information);
                    }), DispatcherPriority.Background);
                }
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBox.Show("Failed to register hotkey. It might already be in use by another application.",
                                    "Hotkey Registration Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }), DispatcherPriority.Background);
            }
        }

        // -----------------------------------------------------------------
        // Windows Shortcut (Global Launch Hotkey) Methods
        // -----------------------------------------------------------------

        /// <summary>
        /// Creates or updates a .lnk shortcut in the user's Start Menu folder
        /// and assigns the given hotkey to it.
        /// </summary>
        private bool CreateGlobalShortcut(ModifierKeys modifiers, Key key)
        {
            try
            {
                string shortcutPath = GetShortcutPath();
                string targetPath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;

                string hotkeyString = FormatHotkeyForShortcut(modifiers, key);
                if (string.IsNullOrEmpty(hotkeyString))
                {
                    Console.WriteLine("[WARN] Cannot create shortcut: invalid hotkey format.");
                    return false;
                }

                // Create the shortcut using direct COM interop (IShellLink / IPersistFile)
                // instead of the IWshRuntimeLibrary which may not be available.
                IShellLinkW shellLink = (IShellLinkW)new ShellLink();

                shellLink.SetPath(targetPath);
                shellLink.SetWorkingDirectory(Path.GetDirectoryName(targetPath));
                shellLink.SetDescription(ShortcutDescription);

                // Set the hotkey (converts e.g. "Ctrl+Alt+T" to the Windows hotkey keys)
                ushort hotkey = ParseHotkeyString(hotkeyString);
                shellLink.SetHotkey(hotkey);

                // Save to file via IPersistFile
                IPersistFile persistFile = (IPersistFile)shellLink;
                persistFile.Save(shortcutPath, false);
                Marshal.ReleaseComObject(persistFile);
                Marshal.ReleaseComObject(shellLink);

                Console.WriteLine($"[INFO] Global shortcut created at {shortcutPath} with hotkey {hotkeyString}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to create global shortcut: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Parses a hotkey string like "Ctrl+Alt+T" into a Windows shortcut hotkey WORD.
        /// Lower byte is the virtual-key code; upper byte indicates modifiers.
        /// </summary>
        private static ushort ParseHotkeyString(string hotkeyString)
        {
            // Split on '+' and determine modifiers and key
            string[] parts = hotkeyString.Split('+');
            if (parts.Length < 2) return 0;

            // The last part is the key, everything before is modifiers
            string keyPart = parts[^1];
            byte modByte = 0;

            for (int i = 0; i < parts.Length - 1; i++)
            {
                switch (parts[i].ToLowerInvariant())
                {
                    case "alt":   modByte |= 0x04; break;
                    case "ctrl":  modByte |= 0x02; break;
                    case "shift": modByte |= 0x01; break;
                }
            }

            // Convert the key string to a virtual-key code
            byte vk = MapKeyStringToVK(keyPart);
            return (ushort)((modByte << 8) | vk);
        }

        /// <summary>
        /// Maps a shortcut key string back to a Windows virtual-key code.
        /// </summary>
        private static byte MapKeyStringToVK(string key)
        {
            string upperKey = key.ToUpperInvariant();

            // Single letters
            if (upperKey.Length == 1 && upperKey[0] >= 'A' && upperKey[0] <= 'Z')
                return (byte)upperKey[0];

            // Digits
            if (upperKey.Length == 1 && upperKey[0] >= '0' && upperKey[0] <= '9')
                return (byte)(0x30 + (upperKey[0] - '0'));

            // Function keys
            if (upperKey.StartsWith("F") && int.TryParse(upperKey[1..], out int fNum) && fNum >= 1 && fNum <= 24)
                return (byte)(0x70 + fNum - 1);

            // Numpad keys (e.g., "Num 0")
            if (upperKey.StartsWith("NUM ") && int.TryParse(upperKey[4..], out int numDigit) && numDigit >= 0 && numDigit <= 9)
                return (byte)(0x60 + numDigit);

            return upperKey switch
            {
                "~" => (byte)0xC0,  // OEM tilde
                "-" => 0xBD,        // OEM minus
                "=" => 0xBB,        // OEM plus
                "[" => 0xDB,        // OEM open bracket
                "]" => 0xDD,        // OEM close bracket
                ";" => 0xBA,        // OEM semicolon
                "'" => 0xDE,        // OEM quotes
                "," => 0xBC,        // OEM comma
                "." => 0xBE,        // OEM period
                "/" => 0xBF,        // OEM question
                "\\" => 0xDC,       // OEM backslash
                _ => 0x00,          // fallback (invalid)
            };
        }

        /// <summary>
        /// Removes the global shortcut if it exists.
        /// </summary>
        private void RemoveGlobalShortcut()
        {
            string path = GetShortcutPath();
            if (System.IO.File.Exists(path))
            {
                try
                {
                    System.IO.File.Delete(path);
                    Console.WriteLine("[INFO] Global shortcut removed.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Failed to remove shortcut: {ex.Message}");
                }
            }
        }

        private string GetShortcutPath()
        {
            // Place the shortcut in the user's Start Menu -> Programs folder
            string startMenuFolder = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            return Path.Combine(startMenuFolder, "Programs", ShortcutFileName);
        }

        private string FormatHotkeyForShortcut(ModifierKeys modifiers, Key key)
        {
            // Windows shortcut hotkeys MUST include Ctrl. If not present, we add it.
            if ((modifiers & ModifierKeys.Control) == 0)
            {
                Console.WriteLine("[INFO] Shortcut hotkey requires Ctrl. Adding it automatically.");
                modifiers |= ModifierKeys.Control;
            }

            // Build modifier part
            string modStr = "";
            if ((modifiers & ModifierKeys.Control) != 0) modStr += "Ctrl+";
            if ((modifiers & ModifierKeys.Alt) != 0) modStr += "Alt+";
            if ((modifiers & ModifierKeys.Shift) != 0) modStr += "Shift+";
            if ((modifiers & ModifierKeys.Windows) != 0)
            {
                // Win key is not supported in .lnk hotkeys – ignore it
                Console.WriteLine("[WARN] Win key is ignored in Windows shortcut hotkeys.");
            }

            modStr = modStr.TrimEnd('+');
            if (string.IsNullOrEmpty(modStr)) return null;

            // Convert the Key enum to the string Windows expects (e.g., "T", "F1", "Num 1")
            string keyString = MapKeyToShortcutString(key);
            return $"{modStr}+{keyString}";
        }

        private string MapKeyToShortcutString(Key key)
        {
            // Function keys
            if (key >= Key.F1 && key <= Key.F24)
                return key.ToString();

            // Numeric pad
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
                return "Num " + key.ToString().Substring(6); // "Num 0" .. "Num 9"

            // Letters and digits
            string s = key.ToString();
            if (s.Length == 1)
                return s.ToUpper();

            // Common OEM keys
            switch (key)
            {
                case Key.OemTilde: return "~";
                case Key.OemMinus: return "-";
                case Key.OemPlus: return "=";
                case Key.OemOpenBrackets: return "[";
                case Key.OemCloseBrackets: return "]";
                case Key.OemSemicolon: return ";";
                case Key.OemQuotes: return "'";
                case Key.OemComma: return ",";
                case Key.OemPeriod: return ".";
                case Key.OemQuestion: return "/";
                case Key.OemBackslash: return "\\";
                default:
                    return s; // fallback
            }
        }
    }
}