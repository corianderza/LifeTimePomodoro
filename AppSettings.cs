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
        public string Language { get; set; } = "ru";

        private static readonly string SettingsDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PomodoroTimer");

        private static readonly string SettingsFile =
            Path.Combine(SettingsDir, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch { /* ignore */ }
            return new AppSettings();
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
