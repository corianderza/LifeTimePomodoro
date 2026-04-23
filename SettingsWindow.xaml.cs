using System.IO;
using System.Media;
using System.Windows;

namespace PomodoroTimer;

public partial class SettingsWindow : Window
{
    // ─── Result returned to caller ────────────────────────────────
    public AppSettings? Result { get; private set; }

    // ─── Working copy of settings ─────────────────────────────────
    private readonly AppSettings _working;
    private SoundPlayer? _testPlayer;

    // ─── Sound / language entries for ComboBoxes ─────────────────
    private sealed record SoundEntry(string Name, string Path);
    private sealed record LanguageEntry(string Code, string DisplayName);

    // ─── Constructor ──────────────────────────────────────────────
    public SettingsWindow(AppSettings current)
    {
        InitializeComponent();

        _working = new AppSettings
        {
            SelectedSoundPath = current.SelectedSoundPath,
            StartWithWindows  = current.StartWithWindows,
            AlwaysOnTop       = current.AlwaysOnTop,
            SilentMode        = current.SilentMode,
            Language          = current.Language
        };

        LoadSounds();
        LoadLanguages();
        ChkAutostart.IsChecked    = _working.StartWithWindows;
        ChkAlwaysOnTop.IsChecked  = _working.AlwaysOnTop;
        ChkSilentMode.IsChecked   = _working.SilentMode;

#if DEBUG
        DebugInfoPanel.Visibility  = System.Windows.Visibility.Visible;
        TbDebugAppPath.Text        = AppDomain.CurrentDomain.BaseDirectory;
        TbDebugSettingsPath.Text   = AppSettings.SettingsFilePath;
        SizeToContent              = System.Windows.SizeToContent.Height;
#endif
    }

    // ─── Load .wav files from Windows Media folder ────────────────
    private void LoadSounds()
    {
        var entries = new List<SoundEntry>
        {
            new(Localizer.GetString("SoundDefault"), "")   // default system sound
        };

        var mediaDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media");

        if (Directory.Exists(mediaDir))
        {
            var wavFiles = Directory.GetFiles(mediaDir, "*.wav")
                                    .OrderBy(f => f)
                                    .Select(f => new SoundEntry(Path.GetFileNameWithoutExtension(f), f));
            entries.AddRange(wavFiles);
        }

        CbSounds.ItemsSource = entries;

        // Select the previously saved sound (or default)
        var match = entries.FirstOrDefault(e => e.Path == _working.SelectedSoundPath);
        CbSounds.SelectedItem = match ?? entries[0];
    }

    // ─── Events ───────────────────────────────────────────────────
    private void CbSounds_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (CbSounds.SelectedItem is SoundEntry entry)
        {
            _working.SelectedSoundPath = entry.Path;
            TbHint.Text = string.IsNullOrEmpty(entry.Path)
                ? Localizer.GetString("HintDefault")
                : entry.Path;
        }
    }

    private void BtnTest_Click(object sender, RoutedEventArgs e)
    {
        _testPlayer?.Stop();
        _testPlayer?.Dispose();

        try
        {
            if (!string.IsNullOrEmpty(_working.SelectedSoundPath) &&
                File.Exists(_working.SelectedSoundPath))
            {
                _testPlayer = new SoundPlayer(_working.SelectedSoundPath);
                _testPlayer.Play();
            }
            else
            {
                SystemSounds.Exclamation.Play();
            }
        }
        catch { SystemSounds.Exclamation.Play(); }
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        _working.StartWithWindows = ChkAutostart.IsChecked == true;
        _working.AlwaysOnTop      = ChkAlwaysOnTop.IsChecked == true;
        _working.SilentMode       = ChkSilentMode.IsChecked == true;
        _working.Language         = ((LanguageEntry)CbLanguage.SelectedItem).Code;
        Result = _working;
        DialogResult = true;
    }

    // ─── Load language options ─────────────────────────────────────
    private void LoadLanguages()
    {
        var entries = new List<LanguageEntry>
        {
            new("ru",      "Русский"),
            new("en",      "English"),
            new("es",      "Español"),
            new("zh-Hans", "中文 (简体)"),
            new("zh-Hant", "中文 (繁體)"),
        };
        CbLanguage.ItemsSource       = entries;
        CbLanguage.DisplayMemberPath = "DisplayName";
        CbLanguage.SelectedItem      = entries.FirstOrDefault(e => e.Code == _working.Language) ?? entries[0];
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        _testPlayer?.Stop();
        DialogResult = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        _testPlayer?.Dispose();
        base.OnClosed(e);
    }
}
