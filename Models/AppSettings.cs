using System.IO;
using System.Text.Json;
using Minecraft_Server_Manager.Services;

namespace Minecraft_Server_Manager.Models
{

    public class AppSettings
    {
        public string DefaultServerPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Minecraft Servers Manager");
        public string SelectedTheme { get; set; } = "Dark Blue";
        public int DefaultRam { get; set; } = 4;
        public string Language { get; set; } = "fr-FR";
    }

    public static class ConfigManager
    {
        private static string ConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MSM", "settings.json");

        public static AppSettings Settings { get; private set; } = new AppSettings();

        public static void Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (Exception ex) { System.Diagnostics.Trace.TraceError("Settings load failed: {0}", ex.Message); }
            if (Settings.Language is not ("fr-FR" or "en-US" or "es-ES" or "de-DE")) Settings.Language = "fr-FR";
            if (Settings.DefaultRam < 1) Settings.DefaultRam = 4;
            if (string.IsNullOrWhiteSpace(Settings.DefaultServerPath)) Settings.DefaultServerPath = new AppSettings().DefaultServerPath;
        }

        public static void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(ConfigPath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
                AtomicFile.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message, "Settings"); }
        }
    }
}
