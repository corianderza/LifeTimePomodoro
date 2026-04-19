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
    private static EventWaitHandle? _showWindowEvent;
    private TaskbarIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private MenuItem? _menuItemShow;
    private MenuItem? _menuItemSettings;
    private MenuItem? _menuItemExit;

    internal AppSettings Settings { get; private set; } = AppSettings.Load();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(true, "PomodoroTimer_SingleInstance", out bool isFirstInstance);
        if (!isFirstInstance)
        {
            // Сигнализируем первому экземпляру — показать окно
            try
            {
                using var ev = EventWaitHandle.OpenExisting("PomodoroTimer_ShowWindow");
                ev.Set();
            }
            catch { }
            Shutdown();
            return;
        }

        // Слушаем сигнал «показать окно» от последующих запусков
        _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "PomodoroTimer_ShowWindow");
        var listenerThread = new Thread(() =>
        {
            while (true)
            {
                _showWindowEvent.WaitOne();
                Dispatcher.Invoke(ShowMainWindow);
            }
        }) { IsBackground = true };
        listenerThread.Start();

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
        var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/ico/16.ico"))?.Stream;
        if (iconStream != null)
            _trayIcon.Icon = new System.Drawing.Icon(iconStream);
        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowMainWindow();

        Localizer.Apply(Settings.Language);

        var menu = new ContextMenu();

        _menuItemShow = new MenuItem { Header = Localizer.GetString("TrayShow") };
        _menuItemShow.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(_menuItemShow);

        _menuItemSettings = new MenuItem { Header = Localizer.GetString("TraySettings") };
        _menuItemSettings.Click += (_, _) => ShowSettings();
        menu.Items.Add(_menuItemSettings);

        menu.Items.Add(new Separator());

        _menuItemExit = new MenuItem { Header = Localizer.GetString("TrayExit") };
        _menuItemExit.Click += (_, _) => { _trayIcon.Dispose(); Shutdown(); };
        menu.Items.Add(_menuItemExit);

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
            Localizer.Apply(Settings.Language);
            UpdateTrayLocalization();
        }
    }

    private void UpdateTrayLocalization()
    {
        if (_menuItemShow     != null) _menuItemShow.Header     = Localizer.GetString("TrayShow");
        if (_menuItemSettings != null) _menuItemSettings.Header = Localizer.GetString("TraySettings");
        if (_menuItemExit     != null) _menuItemExit.Header     = Localizer.GetString("TrayExit");
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


