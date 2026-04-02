using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace PomodoroTimer;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private TaskbarIcon? _trayIcon;
    private MainWindow? _mainWindow;

    internal AppSettings Settings { get; private set; } = AppSettings.Load();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(true, "PomodoroTimer_SingleInstance", out bool isFirstInstance);
        if (!isFirstInstance)
        {
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
        var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/ico/16.ico"))?.Stream;
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
        try
        {
            var xml = new XmlDocument();
            xml.LoadXml("""
                <toast scenario="alarm">
                  <visual>
                    <binding template="ToastGeneric">
                      <text>Pomodoro Timer</text>
                      <text>Таймер завершён!</text>
                    </binding>
                  </visual>
                  <audio silent="true"/>
                  <actions>
                    <action content="Закрыть" arguments="dismiss" activationType="system"/>
                  </actions>
                </toast>
                """);
            var notification = new ToastNotification(xml)
            {
                Priority = ToastNotificationPriority.High,
                SuppressPopup = false
            };
            ToastNotificationManager.CreateToastNotifier().Show(notification);
        }
        catch {
            _trayIcon?.ShowBalloonTip("Pomodoro Timer", "Таймер завершён!", BalloonIcon.Info);
        }
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

    internal static async void ApplyAutostart(bool enable)
    {
        try
        {
            var startupTask = await global::Windows.ApplicationModel.StartupTask.GetAsync("PomodoroTimerStartup");
            if (enable)
                await startupTask.RequestEnableAsync();
            else
                startupTask.Disable();
        }
        catch { /* ignore */ }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}


