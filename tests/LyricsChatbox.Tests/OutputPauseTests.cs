using System.Text.Json;

namespace LyricsChatbox.Tests;

public sealed class OutputPauseTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lyricschatbox-pause-" + Guid.NewGuid().ToString("N"));
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    [Theory]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(30)]
    public void TimedPauseUsesExactAbsoluteUtcExpiration(int minutes)
    {
        var pause = new OutputPauseController(null, Now);
        var change = pause.BeginTimed(Now, TimeSpan.FromMinutes(minutes));
        Assert.True(change.Applied); Assert.True(change.BecamePaused);
        Assert.Equal(OutputPauseKind.Timed, pause.Mode);
        Assert.Equal(Now.AddMinutes(minutes), pause.State.ExpiresUtc);
        Assert.True(pause.IsPaused(Now.AddMinutes(minutes).AddTicks(-1)));
        Assert.True(pause.Evaluate(Now.AddMinutes(minutes), null));
        Assert.False(pause.IsPaused(Now.AddMinutes(minutes)));
    }

    [Fact]
    public void TimedPauseSurvivesRestartBeforeExpiryAndClearsAfterExpiry()
    {
        var pause = new OutputPauseController(null, Now);
        pause.BeginTimed(Now, TimeSpan.FromMinutes(15));
        var restored = new OutputPauseController(pause.State, Now.AddMinutes(7));
        Assert.True(restored.IsPaused(Now.AddMinutes(7)));
        Assert.Contains("8 min", restored.Summary(Now.AddMinutes(7)));
        var expired = new OutputPauseController(pause.State, Now.AddMinutes(20));
        Assert.Equal(OutputPauseKind.None, expired.Mode);
    }

    [Fact]
    public void ImplausiblyFarFuturePersistedTimedPauseIsRejected()
    {
        var restored = new OutputPauseController(
            new(1, nameof(OutputPauseKind.Timed), DateTimeOffset.MaxValue), Now);
        Assert.Equal(OutputPauseKind.None, restored.Mode);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(34.999)]
    [InlineData(35)]
    public void PersistedTimedPauseAtOrWithinClockSkewBoundRestores(double minutes)
    {
        var restored = new OutputPauseController(
            new(1, nameof(OutputPauseKind.Timed), Now.AddMinutes(minutes)), Now);
        Assert.Equal(OutputPauseKind.Timed, restored.Mode);
        Assert.Equal(Now.AddMinutes(minutes), restored.State.ExpiresUtc);
    }

    [Theory]
    [InlineData(35.001)]
    [InlineData(525600)]
    public void PersistedTimedPauseBeyondClockSkewBoundIsRejected(double minutes)
    {
        var restored = new OutputPauseController(
            new(1, nameof(OutputPauseKind.Timed), Now.AddMinutes(minutes)), Now);
        Assert.Equal(OutputPauseKind.None, restored.Mode);
    }

    [Fact]
    public void ExpiredAndOtherPersistedPauseModesKeepTheirExistingSemantics()
    {
        Assert.Equal(OutputPauseKind.None, new OutputPauseController(
            new(1, nameof(OutputPauseKind.Timed), Now), Now).Mode);
        Assert.Equal(OutputPauseKind.None, new OutputPauseController(
            new(1, nameof(OutputPauseKind.Timed), Now.AddTicks(-1)), Now).Mode);
        Assert.Equal(OutputPauseKind.UntilResumed, new OutputPauseController(
            new(1, nameof(OutputPauseKind.UntilResumed)), Now).Mode);
        Assert.Equal(OutputPauseKind.UntilTrackChanges, new OutputPauseController(
            new(1, nameof(OutputPauseKind.UntilTrackChanges), TriggerTrackKey: CoreTests.Track.Key), Now).Mode);
    }

    [Fact]
    public void CorruptUnknownAndIncompletePauseStateFailsSafe()
    {
        Assert.Equal(OutputPauseKind.None, new OutputPauseController(new(2, "UntilResumed"), Now).Mode);
        Assert.Equal(OutputPauseKind.None, new OutputPauseController(new(1, "Unknown"), Now).Mode);
        Assert.Equal(OutputPauseKind.None, new OutputPauseController(new(1, "Timed"), Now).Mode);
        Assert.Equal(OutputPauseKind.None, new OutputPauseController(new(1, "UntilTrackChanges", TriggerTrackKey: "bad"), Now).Mode);
    }

    [Fact]
    public void ReplacingPauseDoesNotStackOrRepeatEntryClear()
    {
        var pause = new OutputPauseController(null, Now);
        var first = pause.BeginTimed(Now, TimeSpan.FromMinutes(15));
        var firstEffects = OutputPauseEntryEffects.For(first, true);
        Assert.True(firstEffects.ClearText); Assert.True(firstEffects.ClearTyping);
        var clears = 0; var typingClears = 0;
        firstEffects.Apply(() => clears++, () => typingClears++);
        var replacement = pause.BeginUntilResumed();
        var replacementEffects = OutputPauseEntryEffects.For(replacement, true);
        Assert.True(replacement.Applied); Assert.False(replacement.BecamePaused);
        Assert.False(replacementEffects.ClearText); Assert.False(replacementEffects.ClearTyping);
        replacementEffects.Apply(() => clears++, () => typingClears++);
        Assert.Equal(1, clears); Assert.Equal(1, typingClears);
        Assert.Equal(OutputPauseKind.UntilResumed, pause.Mode);
        Assert.False(OutputPauseEntryEffects.For(first, false).ClearText);
    }

    [Fact]
    public void UntilNextTrackIgnoresInvalidationHeartbeatPauseScrubReconnectAndSameIdentityAcrossSource()
    {
        var track = CoreTests.Track;
        var pause = new OutputPauseController(null, Now);
        Assert.True(pause.BeginUntilTrackChanges(track).Applied);
        foreach (var observation in new TrackIdentity?[] { null, track, track with { }, track })
        {
            Assert.False(pause.Evaluate(Now.AddSeconds(1), observation));
            Assert.True(pause.IsPaused(Now.AddSeconds(1)));
        }
        var sameMetadataFromSpotify = new TrackIdentity(track.Title, track.Artist, track.Album, track.Duration);
        Assert.Equal(track.Key, sameMetadataFromSpotify.Key);
        Assert.False(pause.Evaluate(Now.AddSeconds(2), sameMetadataFromSpotify));
        Assert.True(pause.Evaluate(Now.AddSeconds(3), track with { Title = "Different" }));
        Assert.Equal(OutputPauseKind.None, pause.Mode);
    }

    [Fact]
    public void UntilNextTrackRequiresBaselineAndSurvivesRestart()
    {
        var pause = new OutputPauseController(null, Now);
        Assert.False(pause.BeginUntilTrackChanges(null).Applied);
        Assert.Equal(OutputPauseKind.None, pause.Mode);
        pause.BeginUntilTrackChanges(CoreTests.Track);
        var restored = new OutputPauseController(pause.State, Now.AddHours(1));
        Assert.False(restored.Evaluate(Now.AddHours(1), null));
        Assert.False(restored.Evaluate(Now.AddHours(1), CoreTests.Track));
        Assert.True(restored.Evaluate(Now.AddHours(1), CoreTests.Track with { Album = "Other" }));
    }

    [Fact]
    public void UntilResumedSurvivesRestartAndOnlyExplicitResumeClears()
    {
        var pause = new OutputPauseController(null, Now);
        pause.BeginUntilResumed();
        var restored = new OutputPauseController(pause.State, Now.AddYears(1));
        Assert.True(restored.IsPaused(Now.AddYears(1)));
        Assert.False(restored.Evaluate(Now.AddYears(1), CoreTests.Track with { Title = "Different" }));
        Assert.True(restored.Resume());
        Assert.False(restored.IsPaused(Now.AddYears(1)));
    }

    [Fact]
    public void DisabledAndPauseRemainIndependentAndExpiryStillClears()
    {
        var pause = new OutputPauseController(null, Now);
        pause.BeginTimed(Now, TimeSpan.FromMinutes(5));
        Assert.False(OutputEligibility.CanSend(false, true));
        Assert.False(OutputEligibility.CanSend(true, true));
        Assert.True(pause.Evaluate(Now.AddMinutes(5), null));
        Assert.False(OutputEligibility.CanSend(false, false));
        Assert.True(OutputEligibility.CanSend(true, false));
    }

    [Fact]
    public void PauseGateDoesNotStopPlaybackClockOrLyricsTracking()
    {
        var engine = new SynchronizationEngine { Enabled = true };
        engine.Observe(CoreTests.Snapshot(10));
        Assert.True(engine.Complete(engine.Epoch, new(LrcParser.Parse("[00:10]current\n[00:12]next"), "loaded")));
        var pause = new OutputPauseController(null, Now); pause.BeginUntilResumed();
        Assert.False(OutputEligibility.CanSend(engine.Enabled, pause.IsPaused(Now)));
        Assert.Equal(CoreTests.Track, engine.Track);
        Assert.Equal("current", engine.Current(0));
        Assert.InRange(engine.Position(1)!.Value, 10.9, 11.1);
    }

    [Fact]
    public void PauseSuppressesManualAndTypingWithoutDestroyingDraftOrOwnership()
    {
        var manual = new ManualChat();
        manual.Edit("draft kept", true, 1); manual.Send();
        Assert.Equal("draft kept", manual.Desired("automatic", 1));
        manual.PauseOutput(1);
        Assert.True(manual.IsManual); Assert.Equal("draft kept", manual.Draft);
        Assert.False(manual.PendingSend); Assert.Null(manual.Desired("automatic", 2));
        var typing = new TypingSignal();
        Assert.True(typing.Take(true, 1));
        typing.Suppress(2);
        Assert.Null(typing.Take(false, 2.1));
        Assert.Null(typing.Take(false, 10));
    }

    [Fact]
    public void PauseFreezesAndResumeContinuesExistingManualHold()
    {
        var manual = new ManualChat(); manual.Edit("sent", false, 0); manual.Send(); manual.Sent(0);
        Assert.InRange(manual.RemainingHold(3)!.Value, 4.9, 5.1);
        manual.PauseOutput(3);
        Assert.Null(manual.Desired("automatic", 100)); Assert.True(manual.IsManual);
        manual.ResumeOutput(100);
        Assert.True(manual.IsManual); Assert.Null(manual.Desired("automatic", 104.9));
        Assert.Equal("automatic", manual.Desired("automatic", 105.1));
        Assert.False(manual.IsManual);
    }

    [Fact]
    public void AutomaticAvailabilityOwnsEveryManualStateAndExpiresHoldAtTheBoundary()
    {
        var manual = new ManualChat();
        Assert.True(manual.AutomaticAvailable(0));

        manual.Focus(true);
        Assert.False(manual.AutomaticAvailable(1));
        manual.Focus(false);
        Assert.False(manual.AutomaticAvailable(2));
        manual.PrepareDraft("prepared");
        Assert.False(manual.AutomaticAvailable(3));

        manual.Edit("sent", false, 4);
        manual.Send();
        Assert.False(manual.AutomaticAvailable(100));
        manual.Sent(100);
        Assert.False(manual.AutomaticAvailable(107.999));
        Assert.True(manual.AutomaticAvailable(108));
        Assert.False(manual.IsManual);

        manual.Edit("paused hold", false, 200);
        manual.Send();
        manual.Sent(200);
        manual.PauseOutput(203);
        Assert.False(manual.AutomaticAvailable(500));
        manual.ResumeOutput(500);
        Assert.False(manual.AutomaticAvailable(504.999));
        Assert.True(manual.AutomaticAvailable(505));

        manual.Edit("indefinite", true, 600);
        Assert.False(manual.AutomaticAvailable(1000));
        manual.Resume();
        Assert.True(manual.AutomaticAvailable(1000));
    }

    [Fact]
    public void ResumeQueuesOnlyLatestAutomaticStateAndNoManualBacklog()
    {
        var scheduler = new ChatboxScheduler();
        scheduler.Set(1, "before", true);
        var before = scheduler.Take(0)!.Value; scheduler.Complete(before, true);
        scheduler.ReceiverChanged();
        scheduler.Set(1, "suppressed old", false);
        Assert.Null(scheduler.Take(2));
        scheduler.Set(1, "latest", true);
        Assert.Equal("latest", scheduler.Take(2)!.Value.Text);

        var manual = new ManualChat(); manual.Edit("manual", true, 0); manual.PauseOutput(0);
        Assert.Null(manual.Desired("automatic latest", 5));
        manual.Send(); Assert.Equal("manual", manual.Desired("automatic latest", 6));
    }

    [Fact]
    public void RuntimePersistenceStoresOnlyHashedTrackIdentity()
    {
        var data = new LocalData(root);
        var pause = new OutputPauseController(null, Now);
        pause.BeginUntilTrackChanges(new("Private title", "Private artist", "Private album", 123.4));
        Assert.True(data.SaveRuntimeState(new(OutputPause: pause.State), Now));
        var json = File.ReadAllText(Path.Combine(root, "runtime-state.json"));
        Assert.DoesNotContain("Private title", json); Assert.DoesNotContain("Private artist", json);
        Assert.DoesNotContain("Private album", json); Assert.Contains(pause.State.TriggerTrackKey!, json);
        Assert.Equal(OutputPauseKind.UntilTrackChanges, data.ReadRuntimeState(Now).OutputPause!.Mode);
        Assert.DoesNotContain("Manual", JsonSerializer.Serialize(data.ReadRuntimeState(Now)));
    }

    [Fact]
    public void DiagnosticsExposePauseStateWithoutTriggerIdentityOrDraft()
    {
        var report = new Diagnostics().Export(null, new(Enabled: true, Message: "private draft"), "idle", "ready", 0,
            outputPaused: true, pauseMode: "UntilTrackChanges", pauseStatus: "Paused · Until next track");
        using var json = JsonDocument.Parse(report);
        var output = json.RootElement.GetProperty("Output");
        Assert.Equal("Paused", output.GetProperty("State").GetString());
        Assert.True(output.GetProperty("Paused").GetBoolean());
        Assert.Equal("UntilTrackChanges", output.GetProperty("PauseMode").GetString());
        Assert.DoesNotContain("private draft", report);
        Assert.DoesNotContain(CoreTests.Track.Key, report);
    }
}
