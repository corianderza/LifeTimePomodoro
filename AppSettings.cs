using System;
using System.IO;
using System.Text.Json;

namespace PomodoroTimer
{
    public class AppSettings
    {
        public string SelectedSoundPath { get; set; } = "";
        public bool StartWithWindows { get; set; } = true;
        public bool AlwaysOnTop { get; set; } = false;
        public bool SilentMode { get; set; } = false;
        public string Language { get; set; } = "";

        private static readonly string SettingsDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PomodoroTimer");

        private static readonly string SettingsFile =
            Path.Combine(SettingsDir, "settings.json");

        public static AppSettings Load()
        {
            AppSettings settings;
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    settings = new AppSettings();
                }
            }
            catch { settings = new AppSettings(); }

            // Empty language means it was never saved — detect from system locale and persist
            if (string.IsNullOrEmpty(settings.Language))
            {
                settings.Language = Localizer.DetectSystemLanguage();
                settings.Save();
            }

            return settings;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFile, json);
            }
            catch { /* ignore */ }
        }
    }
}
