namespace LyricsChatbox.Tests;

public sealed class RuntimeStateTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lyricschatbox-runtime-" + Guid.NewGuid().ToString("N"));
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    [Theory]
    [InlineData("Home")]
    [InlineData("Display")]
    [InlineData("Manual")]
    [InlineData("Settings")]
    public void KnownNavigationSectionsRestore(string section) =>
        Assert.Equal(section, RuntimeStatePolicy.Normalize(new(Section: section), Now).Section);

    [Theory]
    [InlineData("")]
    [InlineData("Unknown")]
    [InlineData("settings")]
    public void UnknownNavigationFallsBackHome(string section) =>
        Assert.Equal("Home", RuntimeStatePolicy.Normalize(new(Section: section), Now).Section);

    [Fact]
    public void ValidNormalAndMaximizedBoundsRestore()
    {
        var monitors = new[] { new MonitorWorkArea(0, 0, 1920, 1040, true) };
        var normal = WindowPlacementPolicy.Restore(new(200, 100, 1000, 800), monitors, 820, 650)!;
        Assert.Equal(new WindowRestorePlan(200, 100, 1000, 800, "Normal"), normal);
        var maximized = WindowPlacementPolicy.Restore(new(200, 100, 1000, 800, "Maximized"), monitors, 820, 650)!;
        Assert.Equal("Maximized", maximized.State);
        Assert.Equal(1000, maximized.Width);
    }

    [Fact]
    public void MinimizedIsNeverCapturedAsDesiredVisibleState()
    {
        var previous = new WindowPlacementState(10, 20, 900, 700, "Maximized");
        Assert.Equal(previous, WindowPlacementPolicy.Capture(new(0, 0, 1, 1), "Minimized", previous));
        Assert.Equal("Minimized", StartupWindowPolicy.InitialState(true, "Maximized"));
        Assert.Equal("Maximized", StartupWindowPolicy.VisibleState("Maximized"));
        Assert.Equal("Normal", StartupWindowPolicy.VisibleState("Minimized"));
        var visible = new WindowRestoreState(); visible.Restore(System.Windows.WindowState.Maximized);
        visible.Observe(System.Windows.WindowState.Minimized);
        Assert.Equal(System.Windows.WindowState.Maximized, visible.Desired);
    }

    [Fact]
    public void OffscreenRemovedMonitorMovesToPrimaryAndOversizedWindowFits()
    {
        var monitors = new[] { new MonitorWorkArea(0, 0, 1920, 1040, true) };
        var plan = WindowPlacementPolicy.Restore(new(5000, 2000, 3000, 2000), monitors, 820, 650)!;
        Assert.InRange(plan.X, 0, 1920 - plan.Width); Assert.InRange(plan.Y, 0, 1040 - plan.Height);
        Assert.Equal(1920, plan.Width); Assert.Equal(1040, plan.Height);
    }

    [Fact]
    public void PartiallyVisibleBoundsClampToIntersectingRightMonitor()
    {
        var monitors = new[]
        {
            new MonitorWorkArea(0, 0, 1920, 1040, true),
            new MonitorWorkArea(1920, 0, 1600, 900)
        };
        var plan = WindowPlacementPolicy.Restore(new(3400, 100, 500, 700), monitors, 320, 240)!;
        Assert.Equal(3020, plan.X); Assert.Equal(500, plan.Width);
        Assert.InRange(plan.Y, 0, 200);
    }

    [Fact]
    public void NegativeCoordinatesOnLeftMonitorRemainValid()
    {
        var monitors = new[]
        {
            new MonitorWorkArea(0, 0, 1920, 1040, true),
            new MonitorWorkArea(-1600, 0, 1600, 900)
        };
        var plan = WindowPlacementPolicy.Restore(new(-1500, 80, 1000, 700), monitors, 820, 650)!;
        Assert.Equal(-1500, plan.X); Assert.Equal(80, plan.Y);
    }

    [Fact]
    public void CorruptCoordinatesAndBadSizesUseSafeDefault()
    {
        var monitors = new[] { new MonitorWorkArea(0, 0, 1920, 1040, true) };
        Assert.Null(WindowPlacementPolicy.Restore(new(double.NaN, 0, 900, 700), monitors, 820, 650));
        Assert.Null(WindowPlacementPolicy.Restore(new(0, 0, double.PositiveInfinity, 700), monitors, 820, 650));
        Assert.Null(WindowPlacementPolicy.Restore(new(0, 0, 0, -1), monitors, 820, 650));
    }

    [Fact]
    public void MinimumConstraintsAreRespectedWithinSmallWorkArea()
    {
        var plan = WindowPlacementPolicy.Restore(new(20, 20, 100, 100),
            new[] { new MonitorWorkArea(0, 0, 700, 500, true) }, 820, 650)!;
        Assert.Equal(700, plan.Width); Assert.Equal(500, plan.Height);
        Assert.Equal(0, plan.X); Assert.Equal(0, plan.Y);
    }

    [Fact]
    public void MissingCorruptAndTruncatedRuntimeStateFailSafelyWithoutChangingSettings()
    {
        var data = new LocalData(root);
        var settings = new AppSettings(Enabled: true, Offset: .7, Message: "retained", PlaybackSource: "Spotify");
        Assert.True(data.SaveSettings(settings));
        Assert.Equal("Home", data.ReadRuntimeState(Now).Section);
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "runtime-state.json"), "{\"Version\":1,\"Window\":");
        Assert.Equal("Home", data.ReadRuntimeState(Now).Section);
        File.WriteAllText(Path.Combine(root, "runtime-state.json"), "not json");
        Assert.Equal(OutputPauseKind.None, data.ReadRuntimeState(Now).OutputPause!.Mode);
        var unchanged = data.ReadSettings();
        Assert.True(unchanged.Enabled); Assert.Equal(.7, unchanged.Offset);
        Assert.Equal("retained", unchanged.Message); Assert.Equal("Spotify", unchanged.PlaybackSource);
    }

    [Fact]
    public void RuntimeStateSavesAtomicallyAndNormalizesState()
    {
        var data = new LocalData(root);
        Assert.True(data.SaveRuntimeState(new(Window: new(10, 20, 900, 700, "Minimized"), Section: "Manual"), Now));
        Assert.True(data.SaveRuntimeState(new(Window: new(20, 30, 1000, 800, "Maximized"), Section: "Settings"), Now));
        var restored = data.ReadRuntimeState(Now);
        Assert.Equal("Settings", restored.Section); Assert.Equal("Maximized", restored.Window!.State);
        Assert.Empty(Directory.EnumerateFiles(root, "*.tmp"));
    }
}
