using System.ComponentModel;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PomodoroTimer;

public partial class MainWindow : Window
{
    // ─── Timer state ───────────────────────────────────────────────
    private enum TimerState { Idle, Running, Paused }
    private TimerState _state = TimerState.Idle;

    private int _minutesSet = 30;       // Minutes configured by the user
    private int _secondsRemaining = 0;  // Seconds remaining during countdown
    private bool _isCompact = false;

    private readonly DispatcherTimer _timer;

    // ─── WinAPI ────────────────────────────────────────────────────
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    private const int GWL_STYLE      = -16;
    private const int WS_MINIMIZEBOX = 0x00020000;

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint   cbSize;
        public IntPtr hwnd;
        public uint   dwFlags;
        public uint   uCount;
        public uint   dwTimeout;
    }
    [DllImport("user32.dll")] private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);
    private const uint FLASHW_ALL       = 0x00000003;

    // ─── WndProc: minimize on taskbar click when focused ──────────
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        // Add WS_MINIMIZEBOX so the taskbar sends SC_MINIMIZE when
        // the user clicks the taskbar button while the window is active
        SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) | WS_MINIMIZEBOX);
        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
    }

    private const int WM_SYSCOMMAND = 0x0112;
    private const int SC_MINIMIZE   = 0xF020;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_SYSCOMMAND && (wParam.ToInt32() & 0xFFF0) == SC_MINIMIZE)
        {
            Dispatcher.BeginInvoke(() => WindowState = WindowState.Minimized);
            handled = true;
        }
        return IntPtr.Zero;
    }

    // ─── Constructor ───────────────────────────────────────────────
    public MainWindow()
    {
        InitializeComponent();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += Timer_Tick;

        UpdateDisplay();
        UpdateButtons();
    }

    // ─── Timer tick ────────────────────────────────────────────────
    private void Timer_Tick(object? sender, EventArgs e)
    {
        _secondsRemaining--;

        if (_secondsRemaining <= 0)
        {
            _secondsRemaining = 0;
            _timer.Stop();
            _state = TimerState.Idle;
            _minutesSet = 30;

            UpdateDisplay();
            UpdateButtons();
            NotifyCompletion();
        }
        else
        {
            UpdateDisplay();
        }
    }

    // ─── Display ───────────────────────────────────────────────────
    private void UpdateDisplay()
    {
        string text = _state == TimerState.Idle
            ? $"{_minutesSet:D2}:00"
            : $"{_secondsRemaining / 60:D2}:{_secondsRemaining % 60:D2}";
        TbDisplay.Text = text;
        TbDisplayCompact.Text = text;
    }

    // ─── Button states ────────────────────────────────────────────
    private void UpdateButtons()
    {
        bool idle    = _state == TimerState.Idle;
        bool running = _state == TimerState.Running;
        bool paused  = _state == TimerState.Paused;

        BtnStart.IsEnabled  = (idle && _minutesSet > 0) || paused;
        BtnStop.IsEnabled   = running;
        BtnReset.IsEnabled  = !idle || _minutesSet > 0;
        BtnMinUp.IsEnabled  = idle;
        BtnMinDown.IsEnabled = idle;
        BtnStartCompact.IsEnabled = (idle && _minutesSet > 0) || paused;
        BtnStopCompact.IsEnabled  = running;

        // Update title bar with remaining time when running/paused
        Title = running || paused
            ? $"Pomodoro Timer — {TbDisplay.Text}"
            : "Pomodoro Timer";
    }

    // ─── Minute increment / decrement ─────────────────────────────
    private void BtnMinUp_Click(object sender, RoutedEventArgs e)
    {
        if (_state != TimerState.Idle) return;
        if (_minutesSet < 99) _minutesSet++;
        UpdateDisplay();
        UpdateButtons();
    }

    private void BtnMinDown_Click(object sender, RoutedEventArgs e)
    {
        if (_state != TimerState.Idle) return;
        if (_minutesSet > 0) _minutesSet--;
        UpdateDisplay();
        UpdateButtons();
    }

    // ─── Preset buttons ───────────────────────────────────────────
    private void BtnPreset_Click(object sender, RoutedEventArgs e)
    {
        if (_state != TimerState.Idle) return;
        if (sender is System.Windows.Controls.Button btn &&
            int.TryParse(btn.Tag?.ToString(), out int minutes))
        {
            _minutesSet = minutes;
            UpdateDisplay();
            UpdateButtons();
        }
    }

    // ─── Start ────────────────────────────────────────────────────
    private void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (_state == TimerState.Idle)
        {
            if (_minutesSet <= 0) return;
            _secondsRemaining = _minutesSet * 60;
        }
        // If Paused — just resume with current _secondsRemaining

        _state = TimerState.Running;
        _timer.Start();
        UpdateDisplay();
        UpdateButtons();
    }

    // ─── Stop (pause) ─────────────────────────────────────────────
    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        if (_state != TimerState.Running) return;
        _timer.Stop();
        _state = TimerState.Paused;
        UpdateButtons();
    }

    // ─── Reset ────────────────────────────────────────────────────
    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _state = TimerState.Idle;
        _minutesSet = 0;
        _secondsRemaining = 0;
        UpdateDisplay();
        UpdateButtons();
    }

    // ─── Spacebar → Start / Stop ──────────────────────────────────
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == Key.Space)
        {
            e.Handled = true;
            if (_state == TimerState.Running)
                BtnStop_Click(this, new RoutedEventArgs());
            else if (_state == TimerState.Paused || (_state == TimerState.Idle && _minutesSet > 0))
                BtnStart_Click(this, new RoutedEventArgs());
        }
    }

    // ─── Compact mode ─────────────────────────────────────────────
    private void SetCompactMode(bool compact)
    {
        _isCompact = compact;
        if (compact)
        {
            MinWidth = MaxWidth = Width = 230;
            MinHeight = MaxHeight = Height = 230;
            ContentGrid.Margin = new Thickness(15);
            ImgBackground.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/bkg/Pom200x200.png"));
            PanelFull.Visibility = Visibility.Collapsed;
            PanelCompact.Visibility = Visibility.Visible;
        }
        else
        {
            MinWidth = MaxWidth = Width = 460;
            MinHeight = MaxHeight = Height = 460;
            ContentGrid.Margin = new Thickness(30);
            ImgBackground.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/bkg/Pom400x400.png"));
            PanelFull.Visibility = Visibility.Visible;
            PanelCompact.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnCompact_Click(object sender, RoutedEventArgs e) => SetCompactMode(!_isCompact);

    // ─── Always on Top ────────────────────────────────────────────
    internal void ApplyAlwaysOnTop(bool value)
    {
        Topmost = value;
        Opacity = value ? 0.70 : 1.0;
    }

    // ─── Settings ─────────────────────────────────────────────────
    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        ((App)Application.Current).ShowSettings();
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void BtnHide_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void DragArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    // ─── Completion notification (sound or silent flash) ──────────
    private void NotifyCompletion()
    {
        var settings = ((App)Application.Current).Settings;
        if (settings.SilentMode)
        {
            StartFlashWindow();
            StartWindowGlow();
            if (!IsVisible)
                ((App)Application.Current).ShowTrayNotification();
        }
        else
        {
            PlayCompletionSound();
        }
    }

    // ─── Мигание иконки в панели задач (WinAPI FlashWindowEx) ──────
    private void StartFlashWindow()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var info = new FLASHWINFO
        {
            cbSize    = (uint)Marshal.SizeOf<FLASHWINFO>(),
            hwnd      = hwnd,
            dwFlags   = FLASHW_ALL,
            uCount    = 6,
            dwTimeout = 0
        };
        FlashWindowEx(ref info);
    }

    // ─── Свечение контура окна (DropShadowEffect на ImgBackground) ─
    private void StartWindowGlow()
    {
        if (ImgBackground.Effect is not DropShadowEffect glow) return;
        var anim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(550))
        {
            AutoReverse    = true,
            RepeatBehavior = new RepeatBehavior(6)
        };
        glow.BeginAnimation(DropShadowEffect.OpacityProperty, anim);
    }

    // ─── Sound ────────────────────────────────────────────────────
    private void PlayCompletionSound()
    {
        var soundPath = ((App)Application.Current).Settings.SelectedSoundPath;
        try
        {
            if (!string.IsNullOrWhiteSpace(soundPath) && File.Exists(soundPath))
            {
                var player = new SoundPlayer(soundPath);
                player.Play();
                return;
            }
        }
        catch { /* fall through to default */ }

        SystemSounds.Exclamation.Play();
    }

    // ─── Close → hide to tray ─────────────────────────────────────
    private void Window_Closing(object sender, CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
