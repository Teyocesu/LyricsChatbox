namespace LyricsChatbox.Tests;

public class CoreTests
{
    internal static readonly TrackIdentity Track = new("Song", "Artist", "Album", 120);
    internal static PlaybackSnapshot Snapshot(double position = 10, double mono = 0, PlaybackState state = PlaybackState.Playing,
        double age = 0, TrackIdentity? track = null, string session = "apple:1")
    {
        var utc = DateTimeOffset.UnixEpoch.AddSeconds(1000 + mono);
        return new(session, track ?? Track, "Song", "Artist — Album", "", 0, 0, 120, position, utc.AddSeconds(-age), state, 1, utc, mono);
    }

    [Theory]
    [InlineData(0, 0, "")]
    [InlineData(1, 0, "first")]
    [InlineData(2, 0, "first")]
    [InlineData(4, 0, "second")]
    [InlineData(4, 1, "first")]
    [InlineData(3, -1, "second")]
    [InlineData(14, 0, "")]
    [InlineData(31, 0, "last")]
    [InlineData(40, 0, "")]
    public void TimelineBoundariesOffsetsAndFiniteGaps(double position, double offset, string expected)
    {
        var timeline = LrcParser.Parse("[00:01]first\n[00:04.00]second\n[00:30.000]last");
        Assert.Equal(expected, timeline.Current(position, offset));
    }

    [Fact]
    public void LrcNormalizesMultipleTagsDuplicateTimesMetadataAndExplicitGaps()
    {
        var timeline = LrcParser.Parse("[ar:Artist]\n[offset:500]\n[00:03.5][00:01.50]日本語\n[00:01.500]translation\n[00:02]\n[00:99]bad\nplain lyrics");
        Assert.Equal(3, timeline.Lines.Count);
        Assert.Equal("日本語\ntranslation", timeline.Current(1));
        Assert.Equal("", timeline.Current(1.5));
        Assert.Equal("日本語", timeline.Current(3));
        Assert.Empty(LrcParser.Parse("unsynchronized words").Lines);
        Assert.Empty(LrcParser.Parse("[٠١:٠٢]bad\n[offset:１２]bad").Lines);
    }

    [Fact]
    public void ClockInterpolatesFreezesResumesAndRejectsStaleResumeAnchor()
    {
        var clock = new PlaybackClock();
        clock.Observe(Snapshot(age: 0.2));
        Assert.Equal(10.7, clock.Position(0.5)!.Value, 5);
        clock.Observe(Snapshot(11, 1, PlaybackState.Paused, 10));
        Assert.Equal(11, clock.Position(2));
        clock.Observe(Snapshot(11, 2, PlaybackState.Playing, 11));
        Assert.Null(clock.Position(2));
        clock.Observe(Snapshot(11, 2.3));
        Assert.Equal(11.5, clock.Position(2.8)!.Value, 5);
        Assert.Null(clock.Position(5));
    }

    [Fact]
    public void ReanchorsWithoutCumulativeDriftAndRespondsToSeeksAndRestart()
    {
        var clock = new PlaybackClock();
        for (var i = 0; i < 400; i++)
        {
            var now = i * 0.25;
            clock.Observe(Snapshot(Math.Floor(now), now));
            Assert.InRange(clock.Position(now)!.Value - now, -1, 1.25);
        }
        foreach (var position in new[] { 110d, 30d, 0d })
        {
            clock.Observe(Snapshot(position, 101));
            Assert.True(clock.Discontinuity);
            Assert.Equal(position, clock.Position(101));
        }
    }

    [Fact]
    public async Task LateLookupCannotChangeNewTrackAndSessionLossInvalidates()
    {
        var engine = new SynchronizationEngine { Enabled = true };
        engine.Observe(Snapshot());
        var a = engine.Epoch;
        var gate = new TaskCompletionSource<LyricsResolution>(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> FinishA() => engine.Complete(a, await gate.Task);
        var pending = FinishA();
        engine.Observe(Snapshot(track: Track with { Title = "B" }));
        Assert.Null(engine.Timeline);
        var b = engine.Epoch;
        engine.Observe(Snapshot(track: Track with { Title = "C" }));
        var current = new LyricsResolution(LrcParser.Parse("[00:10]C"), "loaded");
        Assert.True(engine.Complete(engine.Epoch, current));
        gate.SetResult(new(LrcParser.Parse("[00:10]A"), "old failure"));
        Assert.False(await pending);
        Assert.False(engine.Complete(b, new(null, "B error")));
        Assert.Equal("C", engine.Output(0));
        var epoch = engine.Epoch;
        Assert.False(engine.Observe(Snapshot(track: Track with { Title = "C" }, position: 0)));
        Assert.Equal(epoch, engine.Epoch);
        Assert.Same(current.Timeline, engine.Timeline);
        engine.Enabled = false;
        Assert.Equal("", engine.Output(0));
        engine.Observe(null);
        Assert.Null(engine.Timeline);
        Assert.False(engine.Complete(epoch, current));
        engine.Observe(Snapshot(session: "apple:2"));
        Assert.True(engine.Epoch > epoch);
    }

    [Fact]
    public void RepeatedIntegerAnchorsCannotAccumulateLeadOrSelectUpcomingLyricsEarly()
    {
        var engine = new SynchronizationEngine { Enabled = true };
        engine.Observe(Snapshot(10, 0, age: 0.1));
        engine.Complete(engine.Epoch, new(LrcParser.Parse("[00:10]current\n[00:11]upcoming"), "loaded"));
        // Real regression: Apple republishes the same integer Position with a new LastUpdatedTime ~280ms apart.
        var previous = 10d;
        foreach (var now in new[] { 0.28, 0.56, 0.84, 1.12, 1.40 })
        {
            engine.Observe(Snapshot(10, now, age: 0.1));
            var current = engine.Position(now)!.Value;
            Assert.InRange(current, previous, 10.999999);
            previous = current;
            Assert.Equal("current", engine.Output(now));
        }
        engine.Observe(Snapshot(11, 1.68, age: 0.1));
        Assert.InRange(engine.Position(1.68)!.Value, 11, 11.11);
        Assert.Equal("upcoming", engine.Output(1.68));
        engine.Observe(Snapshot(10, 1.96, age: 0.1));
        Assert.Equal("current", engine.Output(1.96)); // Actual backward seeks still take effect.
    }

    [Fact]
    public void InvalidSourcePlaceholdersFailClosedAndLaterValidPlaybackRecovers()
    {
        var clock = new PlaybackClock();
        foreach (var bad in new[] { Snapshot(-1), Snapshot(121), Snapshot() with { End = double.NaN }, Snapshot() with { Rate = 0 } })
        {
            clock.Observe(bad);
            Assert.Null(clock.Position(0));
        }
        clock.Observe(Snapshot());
        Assert.Equal(10, clock.Position(0));
    }

    [Fact]
    public void AppleMetadataSplitIsNarrowAndRawSnapshotIsPreserved()
    {
        Assert.Equal(Track, TrackIdentity.FromApple("Song", "Artist — Album", "", 120));
        Assert.Equal("Artist — Name", TrackIdentity.FromApple("Song", "Artist — Name", "Album", 120).Artist);
        Assert.Equal("A — B — C", TrackIdentity.FromApple("Song", "A — B — C", "", 120).Artist);
        Assert.Equal("Artist — Album", Snapshot().RawArtist);
    }
}
