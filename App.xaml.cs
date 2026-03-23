using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;

namespace PomodoroTimer;

public partial class App : Application
{
    private TaskbarIcon? _trayIcon;
    private MainWindow? _mainWindow;

    internal AppSettings Settings { get; private set; } = AppSettings.Load();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
        var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/16.ico"))?.Stream;
        if (iconStream != null)
            _trayIcon.Icon = new System.Drawing.Icon(iconStream);
        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowMainWindow();

        var menu = new ContextMenu();

        var showItem = new MenuItem { Header = "Показать" };
        showItem.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(showItem);

        var settingsItem = new MenuItem { Header = "Настройки" };
        settingsItem.Click += (_, _) => ShowSettings();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "Выход" };
        exitItem.Click += (_, _) => { _trayIcon.Dispose(); Shutdown(); };
        menu.Items.Add(exitItem);

        _trayIcon.ContextMenu = menu;

        // Apply autostart setting on first run
        ApplyAutostart(Settings.StartWithWindows);

        _mainWindow = new MainWindow();
        _mainWindow.ApplyAlwaysOnTop(Settings.AlwaysOnTop);
        _mainWindow.Show();
    }

    internal void ShowMainWindow()
    {
        if (_mainWindow == null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    internal void ShowTrayNotification()
    {
        _trayIcon?.ShowBalloonTip("Pomodoro Timer", "Таймер завершён!", BalloonIcon.Info);
    }

    internal void ShowSettings()
    {
        var dlg = new SettingsWindow(Settings);
        dlg.Owner = _mainWindow;
        if (dlg.ShowDialog() == true && dlg.Result != null)
        {
            Settings = dlg.Result;
            Settings.Save();
            ApplyAutostart(Settings.StartWithWindows);
            _mainWindow?.ApplyAlwaysOnTop(Settings.AlwaysOnTop);
        }
    }

    internal static void ApplyAutostart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key == null) return;

            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (enable)
                key.SetValue("PomodoroTimer", $"\"{exePath}\"");
            else
                key.DeleteValue("PomodoroTimer", throwOnMissingValue: false);
        }
        catch { /* ignore registry errors */ }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}


