using System.Text.Json;
using Windows.Storage.Streams;

namespace LyricsChatbox.Tests;

public sealed class MultiPlayerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lyricschatbox-multiplayer-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    [Fact]
    public void SettingsMigratePersistAndCorruptSourceFallsBackWithoutLosingOtherValues()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "settings.json"), "{\"Enabled\":true,\"Offset\":0.7,\"Message\":\"retained\"}");
        var data = new LocalData(root);
        var old = data.ReadSettings();
        Assert.Equal("AppleMusic", old.PlaybackSource);
        Assert.True(old.Enabled); Assert.Equal(0.7, old.Offset); Assert.Equal("retained", old.Message);
        foreach (var mode in new[] { PlaybackSourceMode.AppleMusic, PlaybackSourceMode.Spotify, PlaybackSourceMode.Automatic })
        {
            Assert.True(data.SaveSettings(old with { PlaybackSource = mode.ToString() }));
            Assert.Equal(mode.ToString(), data.ReadSettings().PlaybackSource);
        }
        Assert.True(data.SaveSettings(old with { PlaybackSource = "unexpected" }));
        Assert.Equal("AppleMusic", data.ReadSettings().PlaybackSource);
        File.WriteAllText(Path.Combine(root, "settings.json"), "{\"Enabled\":true,\"Offset\":0.7,\"Message\":\"retained\",\"PlaybackSource\":\"Unknown\"}");
        var corrupt = data.ReadSettings();
        Assert.Equal("AppleMusic", corrupt.PlaybackSource);
        Assert.Equal("retained", corrupt.Message);
    }

    [Theory]
    [InlineData(true, "Playing", false, null, "AppleMusic", false)]
    [InlineData(false, null, true, "Playing", "Spotify", false)]
    [InlineData(true, "Playing", true, "Paused", "AppleMusic", false)]
    [InlineData(true, "Paused", true, "Playing", "Spotify", false)]
    [InlineData(true, "Playing", true, "Playing", null, true)]
    [InlineData(true, "Paused", true, "Paused", null, true)]
    [InlineData(true, "Paused", false, null, "AppleMusic", false)]
    [InlineData(false, null, true, "Paused", "Spotify", false)]
    [InlineData(false, null, false, null, null, false)]
    public void AutomaticSelectionIsDeterministic(bool applePresent, string? appleState,
        bool spotifyPresent, string? spotifyState, string? expected, bool ambiguous)
    {
        var decision = AutomaticPlaybackSelector.Choose(
            new(applePresent, Enum.TryParse<PlaybackState>(appleState, out var a) ? a : null),
            new(spotifyPresent, Enum.TryParse<PlaybackState>(spotifyState, out var s) ? s : null), null);
        Assert.Equal(expected, decision.Source?.ToString());
        Assert.Equal(ambiguous, decision.Ambiguous);
    }

    [Fact]
    public void AutomaticRetainsBoundPausedSourceAndSwitchesWhenAlternatePlays()
    {
        Assert.Equal(PlaybackSourceKind.Spotify, AutomaticPlaybackSelector.Choose(
            new(true, PlaybackState.Paused), new(true, PlaybackState.Paused), PlaybackSourceKind.Spotify).Source);
        Assert.Equal(PlaybackSourceKind.AppleMusic, AutomaticPlaybackSelector.Choose(
            new(true, PlaybackState.Playing), new(), PlaybackSourceKind.Spotify).Source);
    }

    [Fact]
    public void SpotifyRecognizerIsExactAndIdentityRemainsSourceIndependent()
    {
        Assert.True(SpotifyPlayback.IsVerifiedSource(SpotifyPlayback.VerifiedSource));
        Assert.False(SpotifyPlayback.IsVerifiedSource("SpotifyAB.SpotifyMusic_other!Spotify"));
        Assert.False(SpotifyPlayback.IsVerifiedSource("Other.SpotifyMusic_zpdnekdrzrea0!Spotify"));
        Assert.False(SpotifyPlayback.IsVerifiedSource("something Spotify"));
        var apple = CoreTests.Snapshot();
        var spotify = apple with { Source = PlaybackSourceKind.Spotify };
        Assert.Equal(apple.Track.Key, spotify.Track.Key);
        Assert.Equal(apple.Track.LegacyKey, spotify.Track.LegacyKey);
    }

    [Fact]
    public void SpotifySessionSelectionRejectsZeroMultipleAndSimilarIdentifiers()
    {
        var session = new object();
        Assert.Null(SpotifyPlayback.SelectUniqueVerifiedSession(Array.Empty<(string, object)>()));
        Assert.Null(SpotifyPlayback.SelectUniqueVerifiedSession(new[]
        {
            ("SpotifyAB.SpotifyMusic_similar!Spotify", session),
            ("Other.App!Spotify", new object())
        }));
        Assert.Same(session, SpotifyPlayback.SelectUniqueVerifiedSession(new[]
            { (SpotifyPlayback.VerifiedSource, session) }));
        Assert.Null(SpotifyPlayback.SelectUniqueVerifiedSession(new[]
        {
            (SpotifyPlayback.VerifiedSource, session),
            (SpotifyPlayback.VerifiedSource, new object())
        }));
    }

    [Fact]
    public void SpotifyVolumeRequiresOneOwnedSessionAndRejectsStaleAssociation()
    {
        var first = new MusicVolume(10, "spotify-1", 0.4f);
        var second = new MusicVolume(11, "spotify-2", 0.6f);
        Assert.Null(SpotifyMusicVolume.SelectSession(Array.Empty<(MusicVolume, bool)>()));
        Assert.Equal(first, SpotifyMusicVolume.SelectSession(new[] { (first, true) }));
        Assert.Null(SpotifyMusicVolume.SelectSession(new[] { (first, true), (second, true) }));
        Assert.Null(SpotifyMusicVolume.SelectSession(new[] { (first, true) }, second));
    }

    [Fact]
    public void SpotifyGateRejectsTimelineBeforeMetadataAndLaterSettlesNewRecording()
    {
        var gate = new SpotifyTransitionGate();
        var old = Sample(100, 0, end: 200, title: "Old", updated: 0);
        Assert.Equal(SpotifyGateResult.Accept, gate.Observe(old));
        var mixed = Sample(0.1, 0.25, end: 240, title: "Old", updated: 0.25);
        Assert.Equal(SpotifyGateResult.Invalidate, gate.Observe(mixed));
        Assert.Equal(SpotifyGateResult.Settling, gate.Observe(Sample(0.3, 0.5, end: 240, title: "Old", updated: 0.5)));
        Assert.Equal(SpotifyGateResult.Settling, gate.Observe(Sample(0.6, 0.75, end: 240, title: "New", updated: 0.75)));
        Assert.Equal(SpotifyGateResult.Accept, gate.Observe(Sample(0.9, 1, end: 240, title: "New", updated: 1)));
    }

    [Fact]
    public void SpotifyGateInvalidatesScrubThenReanchorsSameTrackWithoutChangingIdentity()
    {
        var gate = new SpotifyTransitionGate();
        var old = Sample(30.25, 0, updated: 0);
        Assert.Equal(SpotifyGateResult.Accept, gate.Observe(old));
        Assert.Equal(SpotifyGateResult.Invalidate, gate.Observe(Sample(50.25, 0.25, updated: 0.25)));
        Assert.Equal(SpotifyGateResult.Settling, gate.Observe(Sample(50.25, 0.5, updated: 0.25)));
        var settled = Sample(50.25, 0.75, updated: 0.25);
        Assert.Equal(SpotifyGateResult.Accept, gate.Observe(settled));
        Assert.Equal(old.Track.Key, settled.Track.Key);
    }

    [Fact]
    public void SpotifyGateFailsClosedOnStalePlayingButPermitsLongPausedObservation()
    {
        var gate = new SpotifyTransitionGate();
        Assert.Equal(SpotifyGateResult.Accept, gate.Observe(Sample(10.2, 0)));
        Assert.Equal(SpotifyGateResult.Invalidate, gate.Observe(Sample(10.2, 6, updated: 0)));
        gate.Reset();
        Assert.Equal(SpotifyGateResult.Accept, gate.Observe(Sample(10.2, 20, updated: 0, state: PlaybackState.Paused)));
    }

    [Fact]
    public async Task ExplicitModesIgnoreOtherSourceAndSwitchDropsOldArtworkAndLookup()
    {
        var apple = new FakeSource(PlaybackSourceKind.AppleMusic);
        var spotify = new FakeSource(PlaybackSourceKind.Spotify);
        await using var coordinator = new PlaybackSourceCoordinator(PlaybackSourceMode.AppleMusic, apple, spotify);
        var engine = new SynchronizationEngine { Enabled = true };
        var artwork = 0;
        coordinator.Observed += (snapshot, _, _) => engine.Observe(snapshot);
        coordinator.ArtworkAvailable += (_, _, _) => artwork++;
        var a = CoreTests.Snapshot();
        apple.Raise(a);
        var epoch = engine.Epoch;
        spotify.Raise(a with { Source = PlaybackSourceKind.Spotify });
        Assert.Equal(PlaybackSourceKind.AppleMusic, engine.Source);
        await coordinator.SetModeAsync(PlaybackSourceMode.Spotify);
        Assert.Null(engine.Track);
        Assert.False(engine.Complete(epoch, new(LrcParser.Parse("[00:01]old"), "old")));
        apple.Raise(a); apple.RaiseArtwork(a.Track);
        Assert.Equal(0, artwork);
        spotify.Raise(a with { Source = PlaybackSourceKind.Spotify });
        Assert.Equal(PlaybackSourceKind.Spotify, engine.Source);
        Assert.False(await coordinator.ControlAsync(MediaCommand.Next, coordinator.Revision - 1));
        await coordinator.SetModeAsync(PlaybackSourceMode.AppleMusic);
        spotify.Raise(a with { Source = PlaybackSourceKind.Spotify });
        Assert.Null(engine.Track);
    }

    [Fact]
    public async Task BothPlayingBlocksActivePipelineAndManualDraftSurvives()
    {
        var apple = new FakeSource(PlaybackSourceKind.AppleMusic);
        var spotify = new FakeSource(PlaybackSourceKind.Spotify);
        await using var coordinator = new PlaybackSourceCoordinator(PlaybackSourceMode.Automatic, apple, spotify);
        var engine = new SynchronizationEngine();
        var manual = new ManualChat(); manual.PrepareDraft("my unsent text");
        coordinator.Observed += (snapshot, _, _) => engine.Observe(snapshot);
        var a = CoreTests.Snapshot();
        apple.Raise(null, "No Apple Music session");
        spotify.Raise(a with { Source = PlaybackSourceKind.Spotify });
        Assert.True(SpinWait.SpinUntil(() => coordinator.ActiveKind == PlaybackSourceKind.Spotify, 2000));
        spotify.Raise(a with { Source = PlaybackSourceKind.Spotify });
        Assert.Equal(PlaybackSourceKind.Spotify, engine.Source);
        apple.Raise(a);
        Assert.True(coordinator.Ambiguous);
        Assert.Null(coordinator.ActiveKind);
        Assert.Null(engine.Track);
        Assert.Null(coordinator.Volume);
        Assert.False(await coordinator.ControlAsync(MediaCommand.Next, coordinator.Revision));
        spotify.Raise(a with { Source = PlaybackSourceKind.Spotify });
        Assert.Null(engine.Track);
        Assert.True(manual.IsManual);
        Assert.Equal("my unsent text", manual.Draft);
    }

    [Fact]
    public async Task AutomaticWaitsForBothCandidatesBeforeBindingPausedColdStart()
    {
        var apple = new FakeSource(PlaybackSourceKind.AppleMusic);
        var spotify = new FakeSource(PlaybackSourceKind.Spotify);
        await using var coordinator = new PlaybackSourceCoordinator(PlaybackSourceMode.Automatic, apple, spotify);
        var paused = CoreTests.Snapshot(state: PlaybackState.Paused);
        apple.Raise(paused);
        Assert.Null(coordinator.ActiveKind);
        spotify.Raise(paused with { Source = PlaybackSourceKind.Spotify });
        Assert.Null(coordinator.ActiveKind);
        Assert.True(coordinator.Ambiguous);
    }

    [Fact]
    public async Task AutomaticSourceLossSwitchesOnlyToFreshPlayingAlternate()
    {
        var apple = new FakeSource(PlaybackSourceKind.AppleMusic);
        var spotify = new FakeSource(PlaybackSourceKind.Spotify);
        await using var coordinator = new PlaybackSourceCoordinator(PlaybackSourceMode.Automatic, apple, spotify);
        var seen = new List<PlaybackSourceKind?>();
        coordinator.Observed += (snapshot, _, _) => seen.Add(snapshot?.Source);
        var applePlaying = CoreTests.Snapshot();
        apple.Raise(applePlaying);
        spotify.Raise(null, "No Spotify session");
        Assert.True(SpinWait.SpinUntil(() => coordinator.ActiveKind == PlaybackSourceKind.AppleMusic, 2000));
        apple.Raise(applePlaying);
        var beforeLoss = seen.Count;
        apple.Raise(null, "No Apple Music session");
        Assert.Null(coordinator.ActiveKind);
        spotify.Raise(applePlaying with { Source = PlaybackSourceKind.Spotify });
        Assert.True(SpinWait.SpinUntil(() => coordinator.ActiveKind == PlaybackSourceKind.Spotify, 2000));
        spotify.Raise(applePlaying with { Source = PlaybackSourceKind.Spotify });
        Assert.Equal(PlaybackSourceKind.Spotify, seen.Last());
        Assert.DoesNotContain(PlaybackSourceKind.AppleMusic, seen.Skip(beforeLoss));
    }

    [Fact]
    public void SourcePresentationAndDiagnosticsStayTruthfulAndPrivate()
    {
        Assert.Contains("both playing", PresentationText.PlaybackStatus(PlaybackSourceMode.Automatic,
            null, null, "Apple Music and Spotify are both playing. Choose a playback source."));
        Assert.Equal("Spotify · not detected", PresentationText.PlaybackStatus(PlaybackSourceMode.Spotify,
            PlaybackSourceKind.Spotify, null, "No Spotify session"));
        var report = new Diagnostics().Export(null, new(PlaybackSource: "Automatic", Message: "secret draft"),
            "none", "ready", 0, selectedSource: PlaybackSourceKind.Spotify,
            sourceStatus: "Spotify · playing", sourceAmbiguous: false);
        using var json = JsonDocument.Parse(report);
        Assert.Equal("Automatic", json.RootElement.GetProperty("PlaybackSource").GetProperty("Configured").GetString());
        Assert.Equal("Spotify", json.RootElement.GetProperty("PlaybackSource").GetProperty("Selected").GetString());
        Assert.DoesNotContain("secret draft", report);
    }

    private static PlaybackSnapshot Sample(double position, double mono, double end = 200,
        string title = "Song", double updated = 0, PlaybackState state = PlaybackState.Playing)
    {
        var startUtc = new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero);
        var track = new TrackIdentity(title, "Artist", "Album", end);
        return new("Spotify:1", track, title, "Artist", "Album", 1, 0, end, position,
            startUtc.AddSeconds(updated), state, 1, startUtc.AddSeconds(mono), mono, PlaybackSourceKind.Spotify);
    }
    private sealed class FakeSource(PlaybackSourceKind kind) : IPlaybackSource
    {
        public PlaybackSourceKind Kind { get; } = kind;
        public string DisplayName => Kind.ToString();
        public long Revision { get; private set; }
        public IPlaybackVolume? Volume => null;
        public event Action<PlaybackSnapshot?, string, long>? Observed;
        public event Action<TrackIdentity, long, IRandomAccessStreamReference?>? ArtworkAvailable;
        public void Start() { }
        public void Suspend() => Raise(null);
        public void ReanchorAfterResume() => Raise(null);
        public MediaControls GetControls() => new(Next: true);
        public Task<bool> ControlAsync(MediaCommand _, long expectedRevision) => Task.FromResult(expectedRevision == Revision);
        public void Raise(PlaybackSnapshot? snapshot, string status = "test")
        { Revision++; Observed?.Invoke(snapshot, status, Revision); }
        public void RaiseArtwork(TrackIdentity track) => ArtworkAvailable?.Invoke(track, Revision, null);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
