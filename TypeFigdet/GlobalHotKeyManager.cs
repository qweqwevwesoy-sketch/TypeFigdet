using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace TypeFigdet
{
    internal class GlobalHotKeyManager
    {
        private const int WM_HOTKEY = 0x0312;
        private int _hotKeyId = 9000;
        private IntPtr _windowHandle;
        private HwndSource _hwndSource;
        private Action _onHotKeyPressed;
        public ModifierKeys CurrentModifiers => _currentModifier;
        public Key CurrentKey => _currentKey;
        private ModifierKeys _currentModifier;
        private Key _currentKey;
        private DateTime _lastRegistrationTime = DateTime.MinValue;
        private const int HOTKEY_GRACE_PERIOD_MS = 500;
        private bool _isHookAdded = false;

        // Reserved key combinations that often cause issues
        private static readonly HashSet<(ModifierKeys, Key)> ReservedCombinations = new()
        {
            (ModifierKeys.Control | ModifierKeys.Alt, Key.Delete), // Ctrl+Alt+Del
            (ModifierKeys.Windows, Key.R),                          // Win+R
            (ModifierKeys.Windows, Key.E),                          // Win+E
            (ModifierKeys.Windows, Key.D),                          // Win+D
            (ModifierKeys.Windows, Key.L),                          // Win+L
            (ModifierKeys.Windows, Key.M),                          // Win+M
        };

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("kernel32.dll")]
        private static extern uint GetLastError();

        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;

        public GlobalHotKeyManager(Window window)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));
            _windowHandle = new WindowInteropHelper(window).Handle;
            LogInfo($"GlobalHotKeyManager initialized with window handle: {_windowHandle}");

            // Add the hook once – it stays for the entire lifetime of the window
            _hwndSource = HwndSource.FromHwnd(_windowHandle);
            if (_hwndSource != null)
            {
                _hwndSource.AddHook(HotKeyHook);
                _isHookAdded = true;
                LogInfo("HwndSource hook added successfully.");
            }
            else
            {
                LogError("Failed to obtain HwndSource. Window handle may be invalid.");
                throw new InvalidOperationException("Unable to get HwndSource for the given window.");
            }
        }

        public bool RegisterHotKey(ModifierKeys modifier, Key key, Action onHotKeyPressed)
        {
            try
            {
                LogInfo($"Attempting to register hotkey: Modifiers = {modifier}, Key = {key}");

                string validationError = ValidateHotKey(modifier, key);
                if (validationError != null)
                {
                    LogError($"Invalid hotkey: {validationError}");
                    MessageBox.Show($"Cannot register hotkey: {validationError}", "Hotkey Error",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                // Unregister any previously registered hotkey (same ID)
                UnregisterHotKey();

                _currentModifier = modifier;
                _currentKey = key;
                _onHotKeyPressed = onHotKeyPressed;
                _lastRegistrationTime = DateTime.UtcNow;

                uint modifiers = ConvertModifiers(modifier);
                uint keyCode = (uint)KeyInterop.VirtualKeyFromKey(key);

                LogInfo($"RegisterHotKey params: modifiers=0x{modifiers:X}, keyCode=0x{keyCode:X}, hotKeyId={_hotKeyId}");

                bool success = RegisterHotKey(_windowHandle, _hotKeyId, modifiers, keyCode);

                if (!success)
                {
                    uint errorCode = GetLastError();
                    string errorMessage = GetErrorMessage(errorCode);
                    LogError($"RegisterHotKey failed with error code {errorCode}: {errorMessage}");
                    MessageBox.Show($"Failed to register hotkey ({modifier}+{key}).\nError: {errorMessage}\n\n" +
                                    "Possible causes:\n- Hotkey already in use by another application\n" +
                                    "- Invalid key combination\n- System reserved hotkey",
                                    "Hotkey Registration Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                LogSuccess($"Hotkey {modifier}+{key} registered successfully (ID: {_hotKeyId})");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Unexpected exception in RegisterHotKey: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"Unexpected error while registering hotkey: {ex.Message}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public void UnregisterHotKey()
        {
            try
            {
                if (_windowHandle != IntPtr.Zero)
                {
                    bool success = UnregisterHotKey(_windowHandle, _hotKeyId);
                    if (success)
                        LogInfo($"Hotkey {_currentModifier}+{_currentKey} unregistered successfully (ID: {_hotKeyId})");
                    else
                        LogWarning($"Failed to unregister hotkey (ID: {_hotKeyId}) - maybe it wasn't registered");
                }
            }
            catch (Exception ex)
            {
                LogError($"Exception during unregister: {ex.Message}");
            }
        }

        // Call this when the window is closed to clean up the hook
        public void Dispose()
        {
            try
            {
                UnregisterHotKey();
                if (_isHookAdded && _hwndSource != null)
                {
                    _hwndSource.RemoveHook(HotKeyHook);
                    _isHookAdded = false;
                    LogInfo("HwndSource hook removed.");
                }
                // Do NOT dispose _hwndSource – it belongs to the window itself.
            }
            catch (Exception ex)
            {
                LogError($"Error during Dispose: {ex.Message}");
            }
        }

        private IntPtr HotKeyHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == _hotKeyId)
            {
                // Grace period check
                if ((DateTime.UtcNow - _lastRegistrationTime).TotalMilliseconds < HOTKEY_GRACE_PERIOD_MS)
                {
                    LogDebug($"Hotkey ignored due to grace period ({(DateTime.UtcNow - _lastRegistrationTime).TotalMilliseconds:F0}ms after registration)");
                    handled = true;
                    return IntPtr.Zero;
                }

                LogInfo($"Hotkey triggered: {_currentModifier}+{_currentKey}");
                try
                {
                    _onHotKeyPressed?.Invoke();
                }
                catch (Exception ex)
                {
                    LogError($"Error in hotkey callback: {ex.Message}");
                }
                handled = true;
            }
            return IntPtr.Zero;
        }

        private string ValidateHotKey(ModifierKeys modifier, Key key)
        {
            if (modifier == ModifierKeys.None)
                return "At least one modifier key (Ctrl, Alt, Shift, or Win) is required.";

            if (key == Key.None || key == Key.System)
                return "Invalid key specified.";

            if (key == Key.PrintScreen || key == Key.Snapshot)
                return "PrintScreen/Snapshot cannot be used as a global hotkey.";

            var combination = (modifier, key);
            if (ReservedCombinations.Contains(combination))
                return $"The combination {modifier}+{key} is reserved by Windows.";

            if (modifier.HasFlag(ModifierKeys.Windows) && key == Key.Tab)
                return "Win+Tab is reserved by Windows Task View.";

            if (modifier.HasFlag(ModifierKeys.Alt) && key == Key.F4)
                return "Alt+F4 is reserved for closing windows.";

            return null;
        }

        private uint ConvertModifiers(ModifierKeys modifier)
        {
            uint mod = 0;
            if ((modifier & ModifierKeys.Alt) == ModifierKeys.Alt) mod |= MOD_ALT;
            if ((modifier & ModifierKeys.Control) == ModifierKeys.Control) mod |= MOD_CONTROL;
            if ((modifier & ModifierKeys.Shift) == ModifierKeys.Shift) mod |= MOD_SHIFT;
            if ((modifier & ModifierKeys.Windows) == ModifierKeys.Windows) mod |= MOD_WIN;
            return mod;
        }

        private string GetErrorMessage(uint errorCode)
        {
            return errorCode switch
            {
                0x575 => "ERROR_HOTKEY_ALREADY_REGISTERED (0x575) - The hotkey is already in use.",
                0x5 => "ERROR_ACCESS_DENIED (0x5) - Try running as administrator for Win+ combinations.",
                0x57 => "ERROR_INVALID_PARAMETER (0x57) - Invalid key or modifier.",
                _ => $"Unknown error code: {errorCode}"
            };
        }

        #region Logging
        private static void LogInfo(string msg) => Console.WriteLine($"[INFO] [{DateTime.Now:HH:mm:ss.fff}] {msg}");
        private static void LogWarning(string msg) => Console.WriteLine($"[WARN] [{DateTime.Now:HH:mm:ss.fff}] {msg}");
        private static void LogError(string msg) => Console.WriteLine($"[ERROR] [{DateTime.Now:HH:mm:ss.fff}] {msg}");
        private static void LogSuccess(string msg) => Console.WriteLine($"[SUCCESS] [{DateTime.Now:HH:mm:ss.fff}] {msg}");
        private static void LogDebug(string msg) => Console.WriteLine($"[DEBUG] [{DateTime.Now:HH:mm:ss.fff}] {msg}");
        #endregion
    }
}