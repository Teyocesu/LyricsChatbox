namespace LyricsChatbox;

public sealed record WindowPlacementState(double X, double Y, double Width, double Height, string State = "Normal");
public sealed record RuntimeState(int Version = 1, WindowPlacementState? Window = null,
    string Section = "Home", OutputPauseState? OutputPause = null);
public sealed record MonitorWorkArea(double X, double Y, double Width, double Height, bool Primary = false)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}
public sealed record WindowRestorePlan(double X, double Y, double Width, double Height, string State);

public static class RuntimeStatePolicy
{
    public static readonly string[] Sections = ["Home", "Display", "Manual", "Settings"];

    public static RuntimeState Normalize(RuntimeState? state, DateTimeOffset nowUtc)
    {
        if (state is not { Version: 1 }) return new(OutputPause: OutputPauseState.None);
        var section = Sections.Contains(state.Section, StringComparer.Ordinal) ? state.Section : "Home";
        var window = state.Window is { } value && Finite(value.X, value.Y, value.Width, value.Height) && value.Width > 0 && value.Height > 0
            ? value with { State = value.State == "Maximized" ? "Maximized" : "Normal" }
            : null;
        return new(1, window, section, OutputPauseState.Normalize(state.OutputPause, nowUtc));
    }

    private static bool Finite(params double[] values) => values.All(double.IsFinite);
}

public static class WindowPlacementPolicy
{
    public static WindowRestorePlan? Restore(WindowPlacementState? stored, IReadOnlyList<MonitorWorkArea> monitors,
        double minimumWidth, double minimumHeight)
    {
        if (stored is null || !Finite(stored.X, stored.Y, stored.Width, stored.Height) ||
            stored.Width <= 0 || stored.Height <= 0) return null;
        var usable = monitors.Where(m => Finite(m.X, m.Y, m.Width, m.Height) && m.Width >= 320 && m.Height >= 240).ToArray();
        if (usable.Length == 0) return null;
        var intersections = usable.Select(m => (Monitor: m, Width: Math.Max(0, Math.Min(stored.X + stored.Width, m.Right) - Math.Max(stored.X, m.X)),
            Height: Math.Max(0, Math.Min(stored.Y + stored.Height, m.Bottom) - Math.Max(stored.Y, m.Y)))).ToArray();
        var substantial = intersections.Where(i => i.Width >= 64 && i.Height >= 64)
            .OrderByDescending(i => i.Width * i.Height).FirstOrDefault();
        var target = substantial.Monitor ?? usable.FirstOrDefault(m => m.Primary) ?? usable[0];
        var width = Math.Clamp(stored.Width, Math.Min(minimumWidth, target.Width), target.Width);
        var height = Math.Clamp(stored.Height, Math.Min(minimumHeight, target.Height), target.Height);
        var x = substantial.Monitor is null ? target.X + (target.Width - width) / 2 : stored.X;
        var y = substantial.Monitor is null ? target.Y + (target.Height - height) / 2 : stored.Y;
        x = Math.Clamp(x, target.X, target.Right - width);
        y = Math.Clamp(y, target.Y, target.Bottom - height);
        return new(x, y, width, height, stored.State == "Maximized" ? "Maximized" : "Normal");
    }

    public static WindowPlacementState Capture(WindowPlacementState current, string visibleState,
        WindowPlacementState? previous) => visibleState == "Minimized"
        ? previous ?? current with { State = "Normal" }
        : current with { State = visibleState == "Maximized" ? "Maximized" : "Normal" };

    private static bool Finite(params double[] values) => values.All(double.IsFinite);
}

public static class StartupWindowPolicy
{
    public static string InitialState(bool startMinimized, string desired) => startMinimized ? "Minimized" : VisibleState(desired);
    public static string VisibleState(string desired) => desired == "Maximized" ? "Maximized" : "Normal";
}
