using System.IO;
using System.Text.Json;

namespace TypeFigdet
{
    public static class SettingsManager
    {
        private static readonly string SettingsFolder;
        private static readonly string SettingsFilePath;

        static SettingsManager()
        {
            SettingsFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TypeFigdet"
            );
            SettingsFilePath = Path.Combine(SettingsFolder, "settings.json");
        }

        public static void Save(Settings settings)
        {
            try
            {
                // Ensure the directory exists
                Directory.CreateDirectory(SettingsFolder);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsManager] Failed to save settings: {ex.Message}");
            }
        }

        public static Settings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<Settings>(json);
                    return settings ?? new Settings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsManager] Failed to load settings: {ex.Message}");
            }

            return new Settings();
        }
    }
}