using System.Windows;
using Forms = System.Windows.Forms;

namespace LyricsChatbox;

public partial class MainWindow
{
    private readonly StartupRegistration startup = new(new WindowsStartupStore());
    private Forms.NotifyIcon? tray;
    private Forms.ToolStripMenuItem? trayOutput, trayCompact;
    private bool exitRequested, changingBehavior;

    private void InitializeLifecycle()
    {
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
        SourceInitialized += (_, _) => FitWorkArea();
        Loaded += (_, _) => FitWorkArea();
        ContentRoot.SizeChanged += (_, _) => WindowLayout.Apply(this, ContentRoot.ActualHeight);
        DpiChanged += (_, _) => _ = Dispatcher.InvokeAsync(FitWorkArea);
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized && LifecyclePolicy.Minimize(settings) == WindowAction.Hide) Hide();
        };
    }
    public void ApplyInitialWindowState() { if (settings.StartMinimized) WindowState = WindowState.Minimized; }
    public void RestoreWindow()
    {
        if (closing) return;
        Show(); WindowState = WindowState.Normal; Activate();
    }
    public void RequestExit() { exitRequested = true; Close(); }
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
        trayOutput!.Checked = settings.Enabled; trayCompact!.Checked = settings.Compact;
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
