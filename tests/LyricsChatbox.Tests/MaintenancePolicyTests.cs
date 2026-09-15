using System.Windows;

namespace LyricsChatbox.Tests;

public class MaintenancePolicyTests
{
    [Fact]
    public void PresentationGateAppliesOnlyEffectiveChanges()
    {
        var gate = new PresentationChangeGate<(string Text, bool Enabled)>();
        for (var i = 0; i < 20; i++) Assert.Equal(i == 0, gate.ShouldApply(("same", true)));
        Assert.Equal(1, gate.AppliedCount);
        Assert.True(gate.ShouldApply(("changed", true)));
        Assert.Equal(2, gate.AppliedCount);
        gate.Reset();
        Assert.True(gate.ShouldApply(("changed", true)));
    }

    [Fact]
    public void ProgressCadenceKeepsFastTicksButLimitsVisualUpdates()
    {
        var cadence = new PresentationCadence(.1);
        Assert.True(cadence.IsDue(10));
        Assert.False(cadence.IsDue(10.05));
        Assert.True(cadence.IsDue(10.10));
        cadence.Reset();
        Assert.True(cadence.IsDue(10.11));
    }

    [Fact]
    public void IdenticalNullPlaybackEventsAreDeduplicatedWithoutHidingTransitions()
    {
        var dedupe = new NullPlaybackObservationDeduper();
        Assert.True(dedupe.ShouldEmit(null, "No Apple Music session", 4));
        Assert.False(dedupe.ShouldEmit(null, "No Apple Music session", 4));
        Assert.True(dedupe.ShouldEmit(null, "Multiple Apple Music sessions · waiting", 4));
        Assert.True(dedupe.ShouldEmit(null, "Multiple Apple Music sessions · waiting", 5));
        Assert.True(dedupe.ShouldEmit(CoreTests.Snapshot(), "Apple Music · playing", 5));
        Assert.True(dedupe.ShouldEmit(null, "No Apple Music session", 5));
    }

    [Fact]
    public void SuspendAndResumeSelectDifferentPlaybackActions()
    {
        Assert.Equal(PlaybackRefresh.Suspend, LifecyclePolicy.Playback(PowerTransition.Suspend));
        Assert.Equal(PlaybackRefresh.Resume, LifecyclePolicy.Playback(PowerTransition.Resume));
    }

    [Fact]
    public void WindowRestoreRemembersNormalOrMaximizedButNeverMinimized()
    {
        var state = new WindowRestoreState();
        Assert.Equal(WindowState.Normal, state.Desired);
        state.Observe(WindowState.Maximized); Assert.Equal(WindowState.Maximized, state.Desired);
        state.Observe(WindowState.Minimized); Assert.Equal(WindowState.Maximized, state.Desired);
        state.Observe(WindowState.Normal); Assert.Equal(WindowState.Normal, state.Desired);
        state.Observe(WindowState.Minimized); Assert.Equal(WindowState.Normal, state.Desired);
    }

    [Fact]
    public void FinalVolumeCommitConsumesPendingDebounceOnlyOnce()
    {
        var state = new DebouncedCommitState();
        state.Schedule();
        Assert.True(state.Consume());
        Assert.False(state.Pending);
        Assert.False(state.Consume());
        state.Schedule();
        Assert.True(state.Pending);
    }
}
