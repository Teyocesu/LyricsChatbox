using System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace LyricsChatbox;

public partial class MainWindow
{
    private readonly StartupRegistration startup = new(new WindowsStartupStore());
    private Forms.NotifyIcon? tray;
    private Forms.ToolStripMenuItem? trayOutput, trayCompact;
    private bool exitRequested, changingBehavior;
    private readonly WindowRestoreState restoreState = new();
    private string activeSection = "Home";
    private WindowPlacementState? normalPlacement;
    private bool initialMaximizePending;

    private void InitializeLifecycle()
    {
        runtimeSaveTimer.Tick += (_, _) => { runtimeSaveTimer.Stop(); SaveRuntimeStateNow(); };
        activeSection = runtimeState.Section;
        var placement = WindowPlacementPolicy.Restore(runtimeState.Window, CurrentMonitorWorkAreas(), MinWidth, MinHeight);
        if (placement is not null)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = placement.X; Top = placement.Y; Width = placement.Width; Height = placement.Height;
            normalPlacement = new(placement.X, placement.Y, placement.Width, placement.Height, "Normal");
            restoreState.Restore(placement.State == "Maximized" ? WindowState.Maximized : WindowState.Normal);
        }
        if (settings.StartWithWindows && !startup.Set(true, Environment.ProcessPath!))
        {
            settings = settings with { StartWithWindows = false };
            ErrorText.Text = "Could not register startup. Enable it again in Settings.";
            Save();
        }
        StartWindowsBox.IsChecked = settings.StartWithWindows;
        StartMinimizedBox.IsChecked = settings.StartMinimized;
        MinimizeTrayBox.IsChecked = settings.MinimizeToTray;
        CloseTrayBox.IsChecked = settings.CloseToTray;
        UpdateTray();
        SourceInitialized += (_, _) =>
        {
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WindowBoundsHook);
            FitWorkArea();
            if (initialMaximizePending) { initialMaximizePending = false; WindowState = WindowState.Maximized; }
        };
        Loaded += (_, _) => FitWorkArea();
        LocationChanged += (_, _) => QueueRuntimeStateSave();
        SizeChanged += (_, _) => QueueRuntimeStateSave();
        ContentRoot.SizeChanged += (_, _) => WindowLayout.Apply(this, ContentRoot.ActualHeight);
        DpiChanged += (_, _) => _ = Dispatcher.InvokeAsync(FitWorkArea);
        StateChanged += (_, _) =>
        {
            restoreState.Observe(WindowState);
            MaximizeCaptionButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
            if (WindowState == WindowState.Minimized && LifecyclePolicy.Minimize(settings) == WindowAction.Hide) Hide();
            QueueRuntimeStateSave();
        };
    }
    public bool ApplyInitialWindowState()
    {
        var startsHidden = settings.StartMinimized && settings.MinimizeToTray;
        if (startsHidden) { ShowActivated = false; ShowInTaskbar = false; }
        var initial = StartupWindowPolicy.InitialState(settings.StartMinimized, restoreState.Desired.ToString());
        initialMaximizePending = initial == "Maximized";
        WindowState = initial switch
        {
            "Minimized" => WindowState.Minimized,
            _ => WindowState.Normal
        };
        return startsHidden;
    }
    public void RestoreWindow()
    {
        if (closing) return;
        ShowInTaskbar = true; ShowActivated = true; Show(); WindowState = restoreState.Desired; Activate();
    }
    private void RestoreRuntimeSection() => ShowPage(RuntimeStatePolicy.Sections.Contains(activeSection) ? activeSection : "Home");

    private void QueueRuntimeStateSave()
    {
        if (!ready || closing) return;
        runtimeSaveTimer.Stop(); runtimeSaveTimer.Start();
    }

    private void SaveRuntimeStateNow()
    {
        runtimeSaveTimer.Stop();
        WindowPlacementState? placement = runtimeState.Window;
        if (WindowState == WindowState.Normal && double.IsFinite(Left) && double.IsFinite(Top) &&
            double.IsFinite(ActualWidth) && double.IsFinite(ActualHeight) && ActualWidth > 0 && ActualHeight > 0)
        {
            normalPlacement = new(Left, Top, ActualWidth, ActualHeight, "Normal");
            placement = normalPlacement;
        }
        else if (WindowState == WindowState.Maximized)
        {
            if (normalPlacement is null && RestoreBounds is { } bounds &&
                double.IsFinite(bounds.X) && double.IsFinite(bounds.Y) && bounds.Width > 0 && bounds.Height > 0)
                normalPlacement = new(bounds.X, bounds.Y, bounds.Width, bounds.Height, "Normal");
            if (normalPlacement is not null) placement = normalPlacement with { State = "Maximized" };
        }
        else if (placement is not null)
            placement = placement with { State = restoreState.Desired == WindowState.Maximized ? "Maximized" : "Normal" };
        runtimeState = new(1, placement, RuntimeStatePolicy.Sections.Contains(activeSection) ? activeSection : "Home", outputPause.State);
        if (!data.SaveRuntimeState(runtimeState) && ready) ErrorText.Text = "Could not save window and pause state.";
    }

    private static IReadOnlyList<MonitorWorkArea> CurrentMonitorWorkAreas()
    {
        var result = new List<MonitorWorkArea>();
        foreach (var screen in Forms.Screen.AllScreens)
        {
            var bounds = screen.WorkingArea;
            var point = new MonitorPoint(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
            var monitor = MonitorFromPoint(point, 2);
            var scaleX = 1d; var scaleY = 1d;
            if (monitor != IntPtr.Zero && GetDpiForMonitor(monitor, 0, out var dpiX, out var dpiY) == 0)
            { scaleX = Math.Max(1, dpiX) / 96d; scaleY = Math.Max(1, dpiY) / 96d; }
            result.Add(new(bounds.Left / scaleX, bounds.Top / scaleY, bounds.Width / scaleX, bounds.Height / scaleY, screen.Primary));
        }
        return result;
    }

    [StructLayout(LayoutKind.Sequential)] private readonly record struct MonitorPoint(int X, int Y);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(MonitorPoint point, uint flags);
    [DllImport("Shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint dpiX, out uint dpiY);
    public void RequestExit() { exitRequested = true; Close(); }
    private IntPtr WindowBoundsHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != 0x0024 || lParam == IntPtr.Zero) return IntPtr.Zero; // WM_GETMINMAXINFO
        var screen = Forms.Screen.FromHandle(hwnd);
        var bounds = screen.Bounds;
        var work = screen.WorkingArea;
        var limits = Marshal.PtrToStructure<WindowMinMaxInfo>(lParam);
        limits.MaxPosition = new() { X = work.Left - bounds.Left, Y = work.Top - bounds.Top };
        limits.MaxSize = new() { X = work.Width, Y = work.Height };
        // Handling this message also bypasses WPF's normal MinWidth/MinHeight processing.
        // Native tracking sizes use physical pixels, while WPF minimums are in DIPs.
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        limits.MinTrackSize = new()
        {
            X = (int)Math.Min(work.Width, Math.Ceiling(MinWidth * dpi.DpiScaleX)),
            Y = (int)Math.Min(work.Height, Math.Ceiling(MinHeight * dpi.DpiScaleY))
        };
        Marshal.StructureToPtr(limits, lParam, false);
        handled = true;
        return IntPtr.Zero;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct WindowMinMaxInfo
    {
        public WindowPoint Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize;
    }
    private void FitWorkArea()
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        var area = Forms.Screen.FromHandle(handle).WorkingArea;
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        var availableWidth = Math.Max(320, area.Width / dpi.DpiScaleX - 16);
        var availableHeight = Math.Max(320, area.Height / dpi.DpiScaleY - 16);
        MinWidth = Math.Min(820, availableWidth); MinHeight = Math.Min(650, availableHeight);
        Width = Math.Min(Width, availableWidth); Height = Math.Min(Height, availableHeight);
        // CenterScreen can place an initially oversized window above the work area before Loaded.
        if (WindowState == WindowState.Normal)
        {
            Left = Math.Clamp(Left, area.Left / dpi.DpiScaleX, Math.Max(area.Left / dpi.DpiScaleX, area.Right / dpi.DpiScaleX - Width));
            Top = Math.Clamp(Top, area.Top / dpi.DpiScaleY, Math.Max(area.Top / dpi.DpiScaleY, area.Bottom / dpi.DpiScaleY - Height));
        }
    }

    private void UpdateTray()
    {
        if (!(settings.CloseToTray || settings.MinimizeToTray))
        {
            DisposeTray();
            return;
        }
        if (tray is null)
        {
            using var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/AppIcon.ico"))!.Stream;
            using var source = new System.Drawing.Icon(resource);
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Open LyricsChatbox", null, (_, _) => RestoreWindow());
            trayOutput = new("Output") { CheckOnClick = false };
            trayOutput.Click += (_, _) => EnabledBox.IsChecked = !settings.Enabled;
            trayCompact = new("Compact / Floating") { CheckOnClick = false };
            trayCompact.Click += (_, _) => CompactBox.IsChecked = !settings.Compact;
            menu.Items.Add(trayOutput); menu.Items.Add(trayCompact);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, _) => RequestExit());
            tray = new() { Icon = (System.Drawing.Icon)source.Clone(), Text = "LyricsChatbox", ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += (_, _) => RestoreWindow();
        }
        trayOutput!.Checked = settings.Enabled;
        trayOutput.Text = !settings.Enabled ? "Output (off)" : IsOutputPaused(DateTimeOffset.UtcNow) ? "Output (paused)" : "Output";
        trayCompact!.Checked = settings.Compact;
    }

    private void BehaviorChanged(object sender, RoutedEventArgs e)
    {
        if (!ready || changingBehavior) return;
        changingBehavior = true;
        try
        {
            var enabled = StartWindowsBox.IsChecked == true;
            if (enabled != settings.StartWithWindows && !startup.Set(enabled, Environment.ProcessPath!))
            {
                StartWindowsBox.IsChecked = settings.StartWithWindows;
                ErrorText.Text = "Could not change Windows startup. Your previous setting is retained.";
                return;
            }
            settings = settings with
            {
                StartWithWindows = enabled,
                StartMinimized = StartMinimizedBox.IsChecked == true,
                MinimizeToTray = MinimizeTrayBox.IsChecked == true,
                CloseToTray = CloseTrayBox.IsChecked == true
            };
            UpdateTray(); Save();
        }
        finally { changingBehavior = false; }
    }
    private void DisposeTray()
    {
        if (tray is null) return;
        tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Icon?.Dispose(); tray.Dispose(); tray = null;
    }
}
