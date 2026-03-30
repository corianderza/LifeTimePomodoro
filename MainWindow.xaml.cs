using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PixelFormat = System.Windows.Media.PixelFormat;

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

    // ─── DPI scale cache (updated on init and monitor change) ─────
    private double _dpiScaleX = 1.0;
    private double _dpiScaleY = 1.0;

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

    // ─── DWM API for live taskbar thumbnails ───────────────────────
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    [DllImport("dwmapi.dll")] private static extern int DwmSetIconicThumbnail(IntPtr hwnd, IntPtr hBitmap, uint dwSITFlags);
    [DllImport("dwmapi.dll")] private static extern int DwmSetIconicLivePreviewBitmap(IntPtr hwnd, IntPtr hBitmap, IntPtr ptClient, uint dwSITFlags);
    [DllImport("dwmapi.dll")] private static extern int DwmInvalidateIconicBitmaps(IntPtr hwnd);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);

    private const int DWMWA_FORCE_ICONIC_REPRESENTATION = 7;
    private const int DWMWA_HAS_ICONIC_BITMAP           = 10;
    private const int WM_DWMSENDICONICTHUMBNAIL          = 0x0323;
    private const int WM_DWMSENDICONICLIVEPREVIEWBITMAP   = 0x0326;

    // ─── WndProc: minimize on taskbar click when focused ──────────
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        // Add WS_MINIMIZEBOX so the taskbar sends SC_MINIMIZE when
        // the user clicks the taskbar button while the window is active
        SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) | WS_MINIMIZEBOX);
        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);

        // Cache DPI scale for physical pixel calculations
        var ps = HwndSource.FromHwnd(hwnd);
        if (ps?.CompositionTarget != null)
        {
            _dpiScaleX = ps.CompositionTarget.TransformToDevice.M11;
            _dpiScaleY = ps.CompositionTarget.TransformToDevice.M22;
        }

        // Tell DWM this window can provide iconic bitmaps
        int trueVal = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_HAS_ICONIC_BITMAP, ref trueVal, sizeof(int));
        // FORCE_ICONIC is toggled in OnStateChanged — only when minimized
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        // Enable FORCE_ICONIC only when minimized — visible windows use standard DWM compositing
        int forceIconic = WindowState == WindowState.Minimized ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_FORCE_ICONIC_REPRESENTATION, ref forceIconic, sizeof(int));

        if (WindowState == WindowState.Minimized)
        {
            // Proactively push both bitmaps immediately so DWM has them before Alt+Tab
            // or taskbar hover fires — prevents the "squish on first view" race condition
            SendLivePreviewBitmap(hwnd);
            int physW = Math.Max(1, (int)Math.Round((_isCompact ? 230 : 460) * _dpiScaleX));
            int physH = Math.Max(1, (int)Math.Round((_isCompact ? 230 : 460) * _dpiScaleY));
            SendIconicThumbnail(hwnd, physW, physH);
            DwmInvalidateIconicBitmaps(hwnd);
        }
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
        else if (msg == WM_DWMSENDICONICTHUMBNAIL)
        {
            int maxWidth  = (int)((long)lParam >> 16) & 0xFFFF;
            int maxHeight = (int)(long)lParam & 0xFFFF;
            SendIconicThumbnail(hwnd, maxWidth, maxHeight);
            handled = true;
        }
        else if (msg == WM_DWMSENDICONICLIVEPREVIEWBITMAP)
        {
            SendLivePreviewBitmap(hwnd);
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

        // Update DPI cache when window moves to a different monitor
        DpiChanged += (_, args) =>
        {
            _dpiScaleX = args.NewDpi.DpiScaleX;
            _dpiScaleY = args.NewDpi.DpiScaleY;
            InvalidateIconicBitmaps();
        };

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

        InvalidateIconicBitmaps();
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
        InvalidateIconicBitmaps();
    }

    // ─── Stop (pause) ─────────────────────────────────────────────
    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        if (_state != TimerState.Running) return;
        _timer.Stop();
        _state = TimerState.Paused;
        UpdateButtons();
        InvalidateIconicBitmaps();
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
        InvalidateIconicBitmaps();
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
        InvalidateIconicBitmaps();
    }

    private void BtnCompact_Click(object sender, RoutedEventArgs e) => SetCompactMode(!_isCompact);

    // ─── Always on Top ────────────────────────────────────────────
    internal void ApplyAlwaysOnTop(bool value)
    {
        Topmost = value;
        Opacity = value ? 0.70 : 1.0;
        InvalidateIconicBitmaps();
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

    // ─── DWM live thumbnail helpers ─────────────────────────────────
    internal void InvalidateIconicBitmaps()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
            DwmInvalidateIconicBitmaps(hwnd);
    }

    private void SendIconicThumbnail(IntPtr hwnd, int maxWidth, int maxHeight)
    {
        IntPtr hBitmap = IntPtr.Zero;
        try
        {
            hBitmap = RenderContentToHBitmap(maxWidth, maxHeight);
            if (hBitmap != IntPtr.Zero)
                DwmSetIconicThumbnail(hwnd, hBitmap, 0);
        }
        finally
        {
            if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
        }
    }

    private void SendLivePreviewBitmap(IntPtr hwnd)
    {
        IntPtr hBitmap = IntPtr.Zero;
        try
        {
            // Use physical window dimensions — DWM sizes Peek preview container to physical window size
            int physW = Math.Max(1, (int)Math.Round((_isCompact ? 230 : 460) * _dpiScaleX));
            int physH = Math.Max(1, (int)Math.Round((_isCompact ? 230 : 460) * _dpiScaleY));
            hBitmap = RenderContentToHBitmap(physW, physH);
            if (hBitmap != IntPtr.Zero)
                DwmSetIconicLivePreviewBitmap(hwnd, hBitmap, IntPtr.Zero, 0);
        }
        finally
        {
            if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
        }
    }

    private IntPtr RenderContentToHBitmap(int maxWidth, int maxHeight)
    {
        try
        {
            // Render full window content (root Grid) so bitmap matches actual window
            var target = Content as UIElement;
            if (target == null) return IntPtr.Zero;

            // Logical window dimensions (WPF coordinate system)
            int logW = _isCompact ? 230 : 460;
            int logH = _isCompact ? 230 : 460;

            // Physical pixel dimensions — what DWM expects for the thumbnail container size
            // Using 96 DPI with logical dimensions would produce a bitmap smaller than the
            // physical window, causing DWM to stretch it and break proportions at >100% DPI
            int physW = Math.Max(1, (int)Math.Round(logW * _dpiScaleX));
            int physH = Math.Max(1, (int)Math.Round(logH * _dpiScaleY));

            // Ensure layout is valid (window may be minimized, layout still intact but
            // calling Measure/Arrange with logical size keeps it consistent)
            target.Measure(new System.Windows.Size(logW, logH));
            target.Arrange(new Rect(0, 0, logW, logH));

            // Render at device DPI into physical-sized RTB:
            //   physW pixels / (96 * dpiScaleX DPI) = logW inches = logW logical units  ✓
            // At 150% DPI: physW=690, dpiX=144 → 690/144 * 96 = 460 logical units = no crop
            var rtb = new RenderTargetBitmap(physW, physH,
                96 * _dpiScaleX, 96 * _dpiScaleY, PixelFormats.Pbgra32);
            rtb.Render(target);

            // Scale down to fit requested max dimensions if needed (don't upscale)
            double scale = Math.Min((double)maxWidth / physW, (double)maxHeight / physH);
            if (scale > 1.0) scale = 1.0;
            int dstW = Math.Max(1, (int)Math.Round(physW * scale));
            int dstH = Math.Max(1, (int)Math.Round(physH * scale));

            BitmapSource finalBitmap;
            if (Math.Abs(scale - 1.0) < 0.001)
                finalBitmap = rtb;
            else
                finalBitmap = new TransformedBitmap(rtb, new ScaleTransform(scale, scale));

            var frame = BitmapFrame.Create(finalBitmap);

            int stride = dstW * 4;
            byte[] pixels = new byte[stride * dstH];
            frame.CopyPixels(pixels, stride, 0);

            using var bmp = new Bitmap(dstW, dstH, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var bmpData = bmp.LockBits(
                new Rectangle(0, 0, dstW, dstH),
                ImageLockMode.WriteOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Marshal.Copy(pixels, 0, bmpData.Scan0, pixels.Length);
            bmp.UnlockBits(bmpData);

            return bmp.GetHbitmap(System.Drawing.Color.FromArgb(0, 0, 0, 0));
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    // ─── Close → hide to tray ─────────────────────────────────────
    private void Window_Closing(object sender, CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
