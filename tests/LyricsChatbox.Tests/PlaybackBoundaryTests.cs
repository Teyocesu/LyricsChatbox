using Windows.Storage.Streams;

namespace LyricsChatbox.Tests;

public class PlaybackBoundaryTests
{
    [Fact]
    public async Task AppleAdapterRunsThroughHostWithoutChangingItsInvalidationContract()
    {
        var apple = new AppleMusicPlayback();
        await using var host = new PlaybackSourceHost(apple);
        Assert.Equal(PlaybackSourceKind.AppleMusic, host.Kind);
        Assert.Equal("Apple Music", host.DisplayName);
        Assert.NotNull(host.Volume);
        var seen = new List<(string Status, long Revision)>();
        host.Observed += (_, status, revision) => seen.Add((status, revision));
        host.Suspend();
        host.ReanchorAfterResume();
        Assert.Equal(["Playback suspended", "Refreshing playback after resume"], seen.Select(x => x.Status));
        Assert.Equal(apple.Revision, host.Revision);
        Assert.Equal(host.Revision, seen[^1].Revision);
        Assert.Equal(PresentationText.AppleMusicStatus(null, seen[^1].Status),
            PresentationText.PlaybackStatus(host.Kind, null, seen[^1].Status));
    }

    [Fact]
    public void AppleNormalizationDoesNotBecomeSpotifyNormalizationOrChangePersistentKey()
    {
        var apple = TrackIdentity.FromApple("Song", "Artist — Album", "", 120.4);
        var spotify = new TrackIdentity("Song", "Artist", "Album", 120.4);
        Assert.Equal(spotify.Key, apple.Key);
        Assert.Equal(spotify.LegacyKey, apple.LegacyKey);
        Assert.Equal("Artist — Album", new TrackIdentity("Song", "Artist — Album", "", 120.4).Artist);
        var appleObservation = CoreTests.Snapshot(track: apple);
        var spotifyObservation = appleObservation with { Source = PlaybackSourceKind.Spotify };
        Assert.Equal(appleObservation.Track.Key, spotifyObservation.Track.Key);
    }

    [Fact]
    public void TimingPolicyDiffersWithoutAlteringAppleTwoSecondAndIntegerCeilingRules()
    {
        Assert.Equal(2, PlaybackTimingPolicy.For(PlaybackSourceKind.AppleMusic).FreshnessSeconds);
        Assert.Equal(1.25, PlaybackTimingPolicy.AppleMusic.DiscontinuitySeconds);
        Assert.True(PlaybackTimingPolicy.AppleMusic.IntegerPositionCeiling);
        Assert.Equal(5.25, PlaybackTimingPolicy.Spotify.FreshnessSeconds);
        Assert.False(PlaybackTimingPolicy.Spotify.IntegerPositionCeiling);
        var apple = new PlaybackClock();
        var spotify = new PlaybackClock(PlaybackTimingPolicy.Spotify);
        var anchor = CoreTests.Snapshot(10.25, 0, age: 2.5);
        apple.Observe(anchor);
        Assert.Null(apple.Position(0));
        spotify.Observe(anchor with { Source = PlaybackSourceKind.Spotify });
        Assert.Equal(12.75, spotify.Position(0)!.Value, 5);
        Assert.Equal(13.75, spotify.Position(1)!.Value, 5);
        Assert.Null(spotify.Position(3)); // 2.5s age + 3s elapsed exceeds the 5.25s bound.
        apple.Observe(CoreTests.Snapshot(10, 0));
        Assert.InRange(apple.Position(1.5)!.Value, 10, 10.999999);
    }

    [Fact]
    public void SpotifyPolicyRequiresNewPlayingAnchorAfterPausedTimestamp()
    {
        var clock = new PlaybackClock(PlaybackTimingPolicy.Spotify);
        var paused = CoreTests.Snapshot(30.25, 0, PlaybackState.Paused) with { Source = PlaybackSourceKind.Spotify };
        clock.Observe(paused);
        Assert.Equal(30.25, clock.Position(0));
        clock.Observe(paused with { State = PlaybackState.Playing, ObservedMono = 0.25,
            ObservedUtc = paused.ObservedUtc.AddSeconds(0.25) });
        Assert.Null(clock.Position(0.25));
        clock.Observe(paused with { State = PlaybackState.Playing, ObservedMono = 0.4,
            ObservedUtc = paused.ObservedUtc.AddSeconds(0.4) });
        Assert.Null(clock.Position(0.4));
        clock.Observe(paused with { State = PlaybackState.Playing, Position = 30.5, ObservedMono = 0.5,
            ObservedUtc = paused.ObservedUtc.AddSeconds(0.5), LastUpdated = paused.LastUpdated.AddSeconds(0.5) });
        Assert.Equal(30.5, clock.Position(0.5));
    }

    [Fact]
    public async Task SourceSwitchInvalidatesEpochAndDropsOldObservationArtworkAndLookup()
    {
        var old = new FakeSource(PlaybackSourceKind.AppleMusic);
        var next = new FakeSource(PlaybackSourceKind.Spotify);
        await using var host = new PlaybackSourceHost(old);
        var engine = new SynchronizationEngine { Enabled = true };
        var invalidations = 0;
        var artworks = 0;
        host.Observed += (snapshot, _, _) => { if (snapshot is null) invalidations++; engine.Observe(snapshot); };
        host.ArtworkAvailable += (_, _, _) => artworks++;
        var appleSnapshot = CoreTests.Snapshot();
        old.Raise(appleSnapshot);
        var epoch = engine.Epoch;
        await host.SwitchAsync(next);
        Assert.Equal(1, invalidations);
        Assert.Null(engine.Track);
        Assert.False(engine.Complete(epoch, new(LrcParser.Parse("[00:10]old"), "old")));
        Assert.True(host.Revision > old.Revision);
        old.Raise(appleSnapshot);
        old.RaiseArtwork(appleSnapshot.Track);
        Assert.Equal(1, invalidations);
        Assert.Equal(0, artworks);
        var spotifySnapshot = appleSnapshot with { Source = PlaybackSourceKind.Spotify };
        next.Raise(spotifySnapshot);
        Assert.Equal(PlaybackSourceKind.Spotify, engine.Source);
        Assert.True(engine.Epoch > epoch);
        Assert.Equal(PlaybackTimingPolicy.Spotify.FreshnessSeconds,
            PlaybackTimingPolicy.For(engine.Source!.Value).FreshnessSeconds);
        Assert.False(await host.ControlAsync(MediaCommand.Next, host.Revision - 1));
        Assert.True(await host.ControlAsync(MediaCommand.Next, host.Revision));
        Assert.Equal(next.Revision, next.LastControlRevision);
        next.Raise(null);
        Assert.Null(engine.Track);
        Assert.False(engine.Complete(epoch, new(LrcParser.Parse("[00:10]old"), "old")));
    }

    private sealed class FakeSource(PlaybackSourceKind kind) : IPlaybackSource
    {
        public PlaybackSourceKind Kind { get; } = kind;
        public string DisplayName => Kind.ToString();
        public long Revision { get; private set; }
        public long LastControlRevision { get; private set; } = -1;
        public IPlaybackVolume? Volume => null;
        public event Action<PlaybackSnapshot?, string, long>? Observed;
        public event Action<TrackIdentity, long, IRandomAccessStreamReference?>? ArtworkAvailable;
        public void Start() { }
        public void Suspend() => Raise(null);
        public void ReanchorAfterResume() => Raise(null);
        public MediaControls GetControls() => new();
        public Task<bool> ControlAsync(MediaCommand _, long expectedRevision)
        {
            LastControlRevision = expectedRevision;
            return Task.FromResult(expectedRevision == Revision);
        }
        public void Raise(PlaybackSnapshot? snapshot) { Revision++; Observed?.Invoke(snapshot, "test", Revision); }
        public void RaiseArtwork(TrackIdentity track) => ArtworkAvailable?.Invoke(track, Revision, null);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
