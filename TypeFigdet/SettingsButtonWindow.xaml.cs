using System.Windows;

namespace TypeFigdet
{
    /// <summary>
    /// Interaction logic for SettingsButtonWindow.xaml
    /// </summary>
    public partial class SettingsButtonWindow : Window
    {
        private MainWindow _mainWindow;

        public SettingsButtonWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;

            // Position at top-left corner
            Left = 5;
            Top = 5;

            Loaded += (s, e) =>
            {
                // Register this window with the app
                if (Application.Current is App app)
                {
                    app.RegisterWindow(this);
                }
            };
            Closed += (s, e) =>
            {
                // Unregister when closed
                if (Application.Current is App app)
                {
                    app.UnregisterWindow(this);
                }
            };
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.OpenSettings();
        }
    }
}
