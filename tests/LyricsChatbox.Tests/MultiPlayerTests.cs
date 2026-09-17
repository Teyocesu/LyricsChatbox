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
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("\"Unknown\"")]
    [InlineData("123")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void MalformedPlaybackSourceDefaultsOnlyThatProperty(string? sourceJson)
    {
        Directory.CreateDirectory(root);
        var sourceProperty = sourceJson is null ? "" : ",\"PlaybackSource\":" + sourceJson;
        File.WriteAllText(Path.Combine(root, "settings.json"),
            "{\"Enabled\":true,\"Offset\":0.7,\"Host\":\"192.168.1.40\",\"Port\":9123," +
            "\"Preset\":\"Custom\",\"CustomTemplate\":\"{title}\",\"Message\":\"retained\"," +
            "\"Compact\":true,\"TypingIndicator\":true,\"LiveEdit\":true," +
            "\"StartWithWindows\":true,\"StartMinimized\":true,\"MinimizeToTray\":true," +
            "\"CloseToTray\":true,\"AutomaticUpdateChecks\":true,\"AutoDiscoverOsc\":false," +
            "\"SkippedUpdateVersion\":\"0.5.9\",\"Appearance\":{" +
            "\"AccentPreset\":\"Blue\",\"BackgroundStyle\":\"Graphite\",\"ArtworkTintEnabled\":true}" +
            sourceProperty + "}");
        var restored = new LocalData(root).ReadSettings();
        Assert.Equal("AppleMusic", restored.PlaybackSource);
        Assert.True(restored.Enabled); Assert.Equal(0.7, restored.Offset);
        Assert.Equal("192.168.1.40", restored.Host); Assert.Equal(9123, restored.Port);
        Assert.Equal("Custom", restored.Preset); Assert.Equal("{title}", restored.CustomTemplate);
        Assert.Equal("retained", restored.Message); Assert.True(restored.Compact);
        Assert.True(restored.TypingIndicator); Assert.True(restored.LiveEdit);
        Assert.True(restored.StartWithWindows); Assert.True(restored.StartMinimized);
        Assert.True(restored.MinimizeToTray); Assert.True(restored.CloseToTray);
        Assert.True(restored.AutomaticUpdateChecks); Assert.False(restored.AutoDiscoverOsc);
        Assert.Equal("0.5.9", restored.SkippedUpdateVersion);
        Assert.Equal("Blue", restored.Appearance!.AccentPreset);
        Assert.Equal("Graphite", restored.Appearance.BackgroundStyle);
        Assert.True(restored.Appearance.ArtworkTintEnabled);
    }

    [Theory]
    [InlineData("Spotify")]
    [InlineData("Automatic")]
    public void ValidPlaybackSourceStringsRoundTripCanonically(string source)
    {
        var data = new LocalData(root);
        Assert.True(data.SaveSettings(new(Enabled: true, Message: "retained", PlaybackSource: source)));
        var restored = data.ReadSettings();
        Assert.Equal(source, restored.PlaybackSource); Assert.True(restored.Enabled);
        Assert.Equal("retained", restored.Message);
        Assert.Contains("\"PlaybackSource\": \"" + source + "\"", File.ReadAllText(Path.Combine(root, "settings.json")));
    }

    [Fact]
    public void SyntacticallyInvalidSettingsStillFallBackAsAWhole()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "settings.json"), "{\"Enabled\":true,\"PlaybackSource\":");
        Assert.Equal(new AppSettings(), new LocalData(root).ReadSettings());
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
        var old = SettleInitial(gate, 100, end: 200, title: "Old");
        var mixed = Sample(0.1, 0.75, end: 240, title: "Old", updated: 0.75);
        Assert.Equal(SpotifyGateResult.Invalidate, gate.Observe(mixed));
        Assert.Equal(SpotifyGateResult.Settling, gate.Observe(Sample(0.3, 1, end: 240, title: "Old", updated: 1)));
        Assert.Equal(SpotifyGateResult.Settling, gate.Observe(Sample(0.6, 1.25, end: 240, title: "New", updated: 1.25)));
        Assert.Equal(SpotifyGateResult.Settling, gate.Observe(Sample(0.7, 1.5, end: 240, title: "New", updated: 1.25)));
        var settled = Sample(0.9, 1.75, end: 240, title: "New", updated: 1.75);
        Assert.Equal(SpotifyGateResult.Accept, gate.Observe(settled));
        Assert.NotEqual(old.Track.Key, settled.Track.Key);
    }

    [Fact]
    public void SpotifyGateDoesNotTrustNewSessionFirstSampleOrRepeatedPollOfSameAnchor()
    {
        var gate = new SpotifyTransitionGate();
        Assert.Equal(SpotifyGateResult.Settling, gate.Observe(Sample(10, 0, updated: 0)));
        Assert.Equal(SpotifyGateResult.Settling, gate.Observe(Sample(10, .25, updated: 0)));
        Assert.Equal(SpotifyGateResult.Accept, gate.Observe(Sample(10.5, .5, updated: .5)));
    }

    [Fact]
    public void ColdPausedSpotifyCandidateStaysFailClosedUntilPlayingProof()
    {
        var gate = new SpotifyTransitionGate();
        Assert.Equal(SpotifyGateResult.Settling,
            gate.Observe(Sample(10, 0, end: 325, updated: 0, state: PlaybackState.Paused)));
        Assert.Equal(SpotifyGateResult.Settling,
            gate.Observe(Sample(10, .25, end: 325, updated: .25, state: PlaybackState.Paused)));
        Assert.Equal(SpotifyGateResult.Settling,
            gate.Observe(Sample(10.25, .5, end: 324.73, updated: .5)));
        Assert.Equal(SpotifyGateResult.Accept,
            gate.Observe(Sample(10.75, 1, end: 324.73, updated: 1)));
    }

    [Fact]
    public void ExactSameRecordingCanReconnectWhilePausedWithoutFakePositionProgress()
    {
        var gate = new SpotifyTransitionGate();
        var original = SettleInitial(gate, 10, end: 64.333);
        gate.BeginSession("Spotify:2");
        Assert.Equal(SpotifyGateResult.Settling, gate.Observe(Sample(10, 1, end: 64.333,
            updated: 1, state: PlaybackState.Paused, sessionId: "Spotify:2")));
        var stable = Sample(10, 1.25, end: 64.333, updated: 1,
            state: PlaybackState.Paused, sessionId: "Spotify:2");
        Assert.Equal(SpotifyGateResult.Accept, gate.Observe(stable));
        Assert.Equal(original.Track.Key, stable.Track.Key);
    }

    [Fact]
    public void SpotifyMediaEventCannotLowerProofOrAcceptDelayedOldMetadata()
    {
        var gate = new SpotifyTransitionGate();
        _ = SettleInitial(gate, 100, title: "Old");
        gate.InvalidateForMediaChange(.75);
        Assert.Equal(SpotifyGateResult.Settling, gate.Observe(Sample(100.75, 1, title: "Old", updated: 1)));
        Assert.Equal(SpotifyGateResult.Settling, gate.Observe(Sample(101, 1.25, title: "Old", updated: 1.25)));
        Assert.Equal(SpotifyGateResult.Settling, gate.Observe(Sample(101.25, 1.5, title: "New", updated: 1.5)));
        Assert.Equal(SpotifyGateResult.Settling, gate.Observe(Sample(.2, 1.75, title: "New", updated: 1.75)));
        Assert.Equal(SpotifyGateResult.Accept, gate.Observe(Sample(.45, 2, title: "New", updated: 2)));
    }

    [Fact]
    public void SpotifyReconnectRejectsRoundedIdentityThenSettlesCorrectedIdentity()
    {
        var gate = new SpotifyTransitionGate();
        var original = SettleInitial(gate, 10, end: 64.333);
        gate.BeginSession("Spotify:2");
        Assert.Equal(SpotifyGateResult.Settling,
            gate.Observe(Sample(10, 1, end: 65, updated: 1, sessionId: "Spotify:2")));
        Assert.Equal(SpotifyGateResult.Settling,
            gate.Observe(Sample(10.5, 1.5, end: 65, updated: 1.5, sessionId: "Spotify:2")));
        Assert.Equal(SpotifyGateResult.Settling,
            gate.Observe(Sample(11, 2, end: 64.333, updated: 2, sessionId: "Spotify:2")));
        var corrected = Sample(11.5, 2.5, end: 64.333, updated: 2.5, sessionId: "Spotify:2");
        Assert.Equal(SpotifyGateResult.Accept, gate.Observe(corrected));
        Assert.Equal(original.Track.Key, corrected.Track.Key);
    }

    [Fact]
    public void StaleOldSessionObservationsCannotSettleNewSession()
    {
        var gate = new SpotifyTransitionGate();
        _ = SettleInitial(gate, 10);
        gate.BeginSession("Spotify:2");
        Assert.Equal(SpotifyGateResult.Settling, gate.Observe(Sample(11, 1, updated: 1, sessionId: "Spotify:1")));
        Assert.Equal(SpotifyGateResult.Settling, gate.Observe(Sample(11.5, 1.5, updated: 1.5, sessionId: "Spotify:1")));
        Assert.Equal(SpotifyGateResult.Settling, gate.Observe(Sample(11, 2, updated: 2, sessionId: "Spotify:2")));
        Assert.Equal(SpotifyGateResult.Accept, gate.Observe(Sample(11.5, 2.5, updated: 2.5, sessionId: "Spotify:2")));
    }

    [Theory]
    [InlineData("Natural")]
    [InlineData("Next")]
    public void SpotifyTrackTransitionsRequireCoherentFreshCandidate(string scenario)
    {
        Assert.NotEmpty(scenario);
        var gate = new SpotifyTransitionGate();
        _ = SettleInitial(gate, 190, end: 200, title: "Old");
        Assert.Equal(SpotifyGateResult.Invalidate, gate.Observe(Sample(.1, .75, end: 180, title: "Old", updated: .75)));
        Assert.Equal(SpotifyGateResult.Settling, gate.Observe(Sample(.3, 1, end: 180, title: "New", updated: 1)));
        Assert.Equal(SpotifyGateResult.Accept, gate.Observe(Sample(.55, 1.25, end: 180, title: "New", updated: 1.25)));
    }

    [Fact]
    public void RapidAlternatingMixedSpotifyObservationsNeverSettle()
    {
        var gate = new SpotifyTransitionGate();
        _ = SettleInitial(gate, 100, end: 200, title: "Old");
        Assert.Equal(SpotifyGateResult.Invalidate, gate.Observe(Sample(.1, .75, end: 240, title: "Old", updated: .75)));
        Assert.Equal(SpotifyGateResult.Settling, gate.Observe(Sample(101, 1, end: 200, title: "New", updated: 1)));
        Assert.Equal(SpotifyGateResult.Settling, gate.Observe(Sample(.3, 1.25, end: 240, title: "Old", updated: 1.25)));
        Assert.Equal(SpotifyGateResult.Settling, gate.Observe(Sample(101.5, 1.5, end: 200, title: "New", updated: 1.5)));
    }

    [Fact]
    public void UntilNextTrackIgnoresTransientReconnectIdentityAndExpiresOnAcceptedTrack()
    {
        var gate = new SpotifyTransitionGate();
        var old = SettleInitial(gate, 10, end: 64.333, title: "Old");
        var pause = new OutputPauseController(null, new(2026, 9, 15, 0, 0, 0, TimeSpan.Zero));
        pause.BeginUntilTrackChanges(old.Track);
        gate.BeginSession("Spotify:2");
        var rounded = Sample(10, 1, end: 65, title: "Old", updated: 1, sessionId: "Spotify:2");
        Assert.Equal(SpotifyGateResult.Settling, gate.Observe(rounded));
        Assert.False(pause.Evaluate(rounded.ObservedUtc, null));
        var corrected1 = Sample(11, 2, end: 64.333, title: "Old", updated: 2, sessionId: "Spotify:2");
        var corrected2 = Sample(11.5, 2.5, end: 64.333, title: "Old", updated: 2.5, sessionId: "Spotify:2");
        Assert.Equal(SpotifyGateResult.Settling, gate.Observe(corrected1));
        Assert.Equal(SpotifyGateResult.Accept, gate.Observe(corrected2));
        Assert.False(pause.Evaluate(corrected2.ObservedUtc, corrected2.Track));
        var next1 = Sample(.1, 3, end: 180, title: "New", updated: 3, sessionId: "Spotify:2");
        Assert.Equal(SpotifyGateResult.Invalidate, gate.Observe(next1));
        Assert.False(pause.Evaluate(next1.ObservedUtc, null));
        Assert.Equal(SpotifyGateResult.Settling, gate.Observe(next1 with { Position = .35, ObservedMono = 3.25,
            ObservedUtc = next1.ObservedUtc.AddSeconds(.25), LastUpdated = next1.LastUpdated.AddSeconds(.25) }));
        var next2 = next1 with { Position = .6, ObservedMono = 3.5,
            ObservedUtc = next1.ObservedUtc.AddSeconds(.5), LastUpdated = next1.LastUpdated.AddSeconds(.5) };
        Assert.Equal(SpotifyGateResult.Accept, gate.Observe(next2));
        Assert.True(pause.Evaluate(next2.ObservedUtc, next2.Track));
    }

    [Fact]
    public void SpotifyGateDoesNotAcceptMetadataFirstWithOldTimeline()
    {
        var gate = new SpotifyTransitionGate();
        _ = SettleInitial(gate, 100, end: 200, title: "Old");
        Assert.Equal(SpotifyGateResult.Invalidate,
            gate.Observe(Sample(100.75, .75, end: 200, title: "New", updated: .75)));
        Assert.Equal(SpotifyGateResult.Settling,
            gate.Observe(Sample(101, 1, end: 200, title: "New", updated: 1)));
        Assert.Equal(SpotifyGateResult.Settling,
            gate.Observe(Sample(.2, 1.25, end: 200, title: "New", updated: 1.25)));
        Assert.Equal(SpotifyGateResult.Accept,
            gate.Observe(Sample(.45, 1.5, end: 200, title: "New", updated: 1.5)));
    }

    [Fact]
    public void SpotifyGateTreatsExactIdentityDurationChangeAsTransition()
    {
        var gate = new SpotifyTransitionGate();
        var original = SettleInitial(gate, 10, end: 64.333);
        var rounded = Sample(10.75, .75, end: 65, updated: .75);
        Assert.NotEqual(original.Track.Key, rounded.Track.Key);
        Assert.Equal(SpotifyGateResult.Invalidate, gate.Observe(rounded));
    }

    [Fact]
    public void SpotifyGateInvalidatesScrubThenReanchorsSameTrackWithoutChangingIdentity()
    {
        var gate = new SpotifyTransitionGate();
        var old = SettleInitial(gate, 30.25);
        Assert.Equal(SpotifyGateResult.Invalidate, gate.Observe(Sample(50.25, .75, updated: .75)));
        Assert.Equal(SpotifyGateResult.Settling, gate.Observe(Sample(50.5, 1, updated: 1)));
        var settled = Sample(50.75, 1.25, updated: 1.25);
        Assert.Equal(SpotifyGateResult.Accept, gate.Observe(settled));
        Assert.Equal(old.Track.Key, settled.Track.Key);
    }

    [Fact]
    public void SpotifyGateFailsClosedOnStalePlayingButPermitsLongPausedObservation()
    {
        var gate = new SpotifyTransitionGate();
        _ = SettleInitial(gate, 10.2);
        Assert.Equal(SpotifyGateResult.Invalidate, gate.Observe(Sample(10.2, 6, updated: 0)));
        gate.Reset();
        Assert.Equal(SpotifyGateResult.Settling, gate.Observe(Sample(10.2, 20, updated: 20, state: PlaybackState.Paused)));
        Assert.Equal(SpotifyGateResult.Accept, gate.Observe(Sample(10.2, 21, updated: 21, state: PlaybackState.Paused)));
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

    private static PlaybackSnapshot SettleInitial(SpotifyTransitionGate gate, double position,
        double end = 200, string title = "Song", PlaybackState state = PlaybackState.Playing)
    {
        var first = Sample(position, 0, end, title, 0, state);
        Assert.Equal(SpotifyGateResult.Settling, gate.Observe(first));
        var second = Sample(position + (state == PlaybackState.Playing ? .25 : 0), .25, end, title, .25, state);
        Assert.Equal(SpotifyGateResult.Accept, gate.Observe(second));
        return second;
    }

    private static PlaybackSnapshot Sample(double position, double mono, double end = 200,
        string title = "Song", double updated = 0, PlaybackState state = PlaybackState.Playing,
        string sessionId = "Spotify:1")
    {
        var startUtc = new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero);
        var track = new TrackIdentity(title, "Artist", "Album", end);
        return new(sessionId, track, title, "Artist", "Album", 1, 0, end, position,
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
