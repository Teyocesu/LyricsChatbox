using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace LyricsChatbox.Tests;

public sealed class PresentationTests : IDisposable
{
    [Fact]
    public void TrackLyricsDurationShowsSourceAndCandidateTotals()
    {
        Assert.Equal("Track / lyrics duration", PresentationText.TrackLyricsDurationLabel);
        Assert.Equal("4:53 / 4:54", PresentationText.TrackLyricsDuration(293, 294));
        Assert.NotEqual(PresentationText.TrackLyricsDuration(293, 294), DurationFormatter.Format(42));
    }

    [Theory]
    [InlineData("🎵")]
    [InlineData("é")]
    [InlineData("🇯🇵")]
    public void DuplicatedProfileDoesNotSplitTheLastTextElement(string element)
    {
        var prefix = new string('a', 33);
        var source = new DisplayProfile("original", prefix + element + "end", ContextMode: "Adaptive", Compact: true);
        var library = new ProfileLibrary(1, source.Id, [source]);
        var copy = library.Duplicate(source)!;
        Assert.Equal(prefix + " copy", copy.Selected.Name);
        Assert.Equal(source.ContextMode, copy.Selected.ContextMode);
        Assert.Equal(source.Compact, copy.Selected.Compact);
        Assert.NotEqual(source.Id, copy.SelectedId);
        var data = new LocalData(root);
        Assert.True(data.SaveProfiles(copy));
        Assert.Equal(JsonSerializer.Serialize(copy.Selected), JsonSerializer.Serialize(data.ReadProfiles(new()).Selected));
    }
    private readonly string root = Path.Combine(Path.GetTempPath(),"LyricsChatbox.Tests",Guid.NewGuid().ToString("N"));
    [Fact]
    public void ProfilesMigrateExactPresentationAndPersistCreateEditSelectDelete()
    {
        var data = new LocalData(root);
        var old = new AppSettings(Enabled:true, Offset:.5, Host:"192.168.1.5", Port:9003, Preset:"Custom", CustomTemplate:"{message}\n{lyrics}",
            Message:"日本語 🎵", Compact:true, CustomAlignment:"Center", Appearance:new("Blue"));
        var library = data.ReadProfiles(old);
        Assert.Equal(5,library.Items.Count(p=>p.BuiltIn)); Assert.Equal(old,library.Selected.Apply(old));
        Assert.Equal("Current only",library.Selected.ContextMode);
        library = library.Create("My copy",library.Selected)!;
        var id = library.SelectedId;
        library = library.Save(library.Selected with {Name="Evening", ContextMode="Adaptive", Compact=false})!;
        Assert.True(data.SaveProfiles(library));
        library = data.ReadProfiles(new());
        Assert.Equal("Evening",library.Selected.Name); Assert.Equal("Adaptive",library.Selected.ContextMode); Assert.False(library.Selected.Compact);
        var applied = library.Selected.Apply(old);
        Assert.Equal(old.Appearance,applied.Appearance); Assert.Equal(old.Host,applied.Host); Assert.Equal(old.Offset,applied.Offset); Assert.True(applied.Enabled);
        Assert.NotNull(library.Select("lyrics")); Assert.Null(library.Delete("lyrics"));
        library = library.Delete(id)!; Assert.DoesNotContain(library.Items,p=>p.Id==id); Assert.True(data.SaveProfiles(library));
        Assert.DoesNotContain(data.ReadProfiles(old).Items,p=>p.Id==id);
        File.WriteAllText(Path.Combine(root,"profiles.json"),"{bad"); Assert.Equal(old,data.ReadProfiles(old).Selected.Apply(old));
    }
    [Theory]
    [InlineData("Current only","current")]
    [InlineData("Current + next","current\nnext")]
    [InlineData("Previous + current + next","previous\n› current\nnext")]
    [InlineData("Adaptive","previous\n› current\nnext")]
    public void ContextModesUseTheAuthoritativeLineAndClearAcrossGaps(string mode,string expected)
    {
        var timeline = LrcParser.Parse("[00:01]previous\n[00:03]current\n[00:06]next\n[00:08]\n[00:30]later");
        Assert.Equal(expected,LyricContextComposer.ComposeProfile(timeline.Context(3),CoreTests.Track,"Lyrics Only","{lyrics}","",mode,false));
        Assert.Equal("previous\n› current\nnext",LyricContextComposer.ComposeProfile(timeline.Context(4,1),CoreTests.Track,"Lyrics Only","{lyrics}","","Adaptive",false));
        Assert.Equal(LyricContext.Empty,timeline.Context(0)); Assert.Equal(LyricContext.Empty,timeline.Context(9));
        Assert.Equal(LyricContext.Empty,timeline.Context(25)); Assert.Equal(LyricContext.Empty,timeline.Context(40));
        Assert.Equal("",timeline.Context(30).Previous);
    }
    [Fact]
    public void SemanticBudgetKeepsProfileMetadataBeforeDroppingContext()
    {
        var track = CoreTests.Track with {Title=new('t',80),Artist=new('a',80)};
        foreach (var context in new[]
        {
            new LyricContext(new('p',25),new('c',70),new('n',25)),
            new LyricContext(new('p',60),new('c',70),new('n',25)),
            new LyricContext(new('p',25),new('c',130),new('n',25)),
        })
        {
            var result = LyricContextComposer.ComposeProfile(context,track,"Song + Lyrics","{lyrics}","",
                "Adaptive",false,DateTimeOffset.MinValue,null);
            Assert.StartsWith("♫",result);
            Assert.InRange(ChatboxFormatter.Format(result,false).Length,0,144);
        }
        var fullCurrent = new string('c',142);
        var compact = LyricContextComposer.ComposeProfile(new("previous",fullCurrent,"next"),track,"Song + Lyrics","{lyrics}","",
            "Adaptive",true,DateTimeOffset.MinValue,null);
        Assert.StartsWith("♫",compact);
        Assert.InRange(ChatboxFormatter.Format(compact,true).Length,0,144);
    }
    [Fact]
    public void AdaptiveSharesTheExplicitFullLadderIncludingPreviousWithoutNext()
    {
        var at = new DateTimeOffset(2026, 9, 7, 19, 31, 0, TimeSpan.Zero);
        foreach (var context in new[]
        {
            new LyricContext("previous", "current", "next"),
            new LyricContext("previous", "current", ""),
            new LyricContext("", "current", "next"),
            new LyricContext(new('p', 140), "current", "next"),
        })
        {
            Assert.Equal(
                LyricContextComposer.ComposeProfile(context, CoreTests.Track, "Song + Lyrics", "{lyrics}", "", "Previous + current + next", false, at, null),
                LyricContextComposer.ComposeProfile(context, CoreTests.Track, "Song + Lyrics", "{lyrics}", "", "Adaptive", false, at, null));
        }
        var adaptive = LyricContextComposer.ComposeProfile(new("previous", "current", ""), CoreTests.Track, "Song + Lyrics", "{lyrics}", "", "Adaptive", false, at, null);
        Assert.Equal("♫ Song — Artist\nprevious\n› current", adaptive);

        Assert.Equal("current\nnext", LyricContextComposer.ComposeProfile(new(new string('p', 140), "current", "next"), CoreTests.Track, "Lyrics Only", "{lyrics}", "", "Adaptive", false, at, null));
        Assert.Equal(new string('c', 140), LyricContextComposer.ComposeProfile(new("previous", new string('c', 140), "next"), CoreTests.Track, "Lyrics Only", "{lyrics}", "", "Adaptive", false, at, null));
    }
    [Theory]
    [InlineData("日本語")]
    [InlineData("👨‍👩‍👧‍👦")]
    [InlineData("é")]
    [InlineData("🇯🇵")]
    public void CurrentTruncationPreservesGraphemesAndCompactAndLineLimits(string element)
    {
        var current = string.Concat(Enumerable.Repeat(element,160));
        var expected = ChatboxFormatter.Format("♫ Song — Artist\n" + current,true);
        var result = LyricContextComposer.ComposeProfile(new("old",current,"next"),CoreTests.Track,"Song + Lyrics","{lyrics}","",
            "Adaptive",true,new DateTimeOffset(2026,9,7,19,31,0,TimeSpan.Zero),null);
        Assert.Equal(expected,ChatboxFormatter.Format(result,true));
        Assert.InRange(ChatboxFormatter.Format(result,true).Length,0,144);
        Assert.Equal(result,new UTF8Encoding(false,true).GetString(new UTF8Encoding(false,true).GetBytes(result)));
        var nine = string.Join('\n',Enumerable.Repeat("current",9));
        var at = new DateTimeOffset(2026,9,7,19,31,0,TimeSpan.Zero);
        var overBudget = LyricContextComposer.ComposeProfile(new("old",nine,"next"),CoreTests.Track,"Song + Lyrics","{lyrics}","",
            "Adaptive",false,at,null);
        Assert.StartsWith("♫",overBudget);
        Assert.NotEqual(nine,overBudget);
        var safe = ChatboxFormatter.Format(overBudget,false);
        Assert.InRange(safe.Count(c=>c=='\n'),0,8);
        Assert.InRange(safe.Length,0,144);
        var giant = "e" + new string('\u0301',300);
        var giantResult = LyricContextComposer.ComposeProfile(new("old",giant,"next"),CoreTests.Track,"Lyrics Only","{lyrics}","",
            "Adaptive",true,at,null);
        Assert.Equal(giant,giantResult);
        Assert.Equal("",ChatboxFormatter.Format(giantResult,true));
    }
    private static string ProfileCompose(LyricContext context, string preset, string custom, string mode, bool compact = false, string message = "")
    {
        return LyricContextComposer.ComposeProfile(context, CoreTests.Track, preset, custom, message, mode, compact,
            new DateTimeOffset(2026, 9, 7, 19, 31, 0, TimeSpan.Zero), 111);
    }
    [Fact]
    public void ProfileAwareLyricsOnlyModes()
    {
        var context = new LyricContext("previous", "current", "next");
        Assert.Equal("current", ProfileCompose(context, "Lyrics Only", "{lyrics}", "Current only"));
        Assert.Equal("current\nnext", ProfileCompose(context, "Lyrics Only", "{lyrics}", "Current + next"));
        Assert.Equal("current", ProfileCompose(new("p", "current", new string('n', 140)), "Lyrics Only", "{lyrics}", "Current + next"));
    }
    [Fact]
    public void ProfileAwareAdaptiveTriesAllCandidatesInOrder()
    {
        Assert.Equal("previous\n› current\nnext",
            ProfileCompose(new("previous", "current", "next"), "Lyrics Only", "{lyrics}", "Adaptive"));
        Assert.Equal("current\nnext",
            ProfileCompose(new(new string('p', 140), "current", "next"), "Lyrics Only", "{lyrics}", "Adaptive"));
        Assert.Equal("previous\n› current",
            ProfileCompose(new("previous", "current", new string('n', 140)), "Lyrics Only", "{lyrics}", "Adaptive"));
        Assert.Equal("current",
            ProfileCompose(new(new string('p', 140), "current", new string('n', 140)), "Lyrics Only", "{lyrics}", "Adaptive"));
    }
    [Fact]
    public void ProfileAwareMusicInfoKeepsMetadataBeforeDroppingContext()
    {
        var context = new LyricContext("previous", "current", "next");
        var full = ProfileCompose(context, "Song + Lyrics", "{lyrics}", "Current + next");
        Assert.Equal("♫ Song — Artist\ncurrent\nnext", full);
        var track = CoreTests.Track with { Title = new('t', 80), Artist = new('a', 80) };
        var tight = new LyricContext(new('p', 25), new('c', 70), new('n', 25));
        var result = LyricContextComposer.ComposeProfile(tight, track, "Song + Lyrics", "{lyrics}", "", "Adaptive", false,
            new DateTimeOffset(2026, 9, 7, 19, 31, 0, TimeSpan.Zero), 111);
        Assert.StartsWith("♫", result);
        Assert.InRange(ChatboxFormatter.Format(result, false).Length, 0, 144);
        var adaptive = LyricContextComposer.ComposeProfile(context, CoreTests.Track, "Song + Lyrics", "{lyrics}", "", "Adaptive", false,
            new DateTimeOffset(2026, 9, 7, 19, 31, 0, TimeSpan.Zero), 111);
        Assert.Contains("Song", adaptive); Assert.Contains("Artist", adaptive); Assert.Contains("next", adaptive);
    }
    [Fact]
    public void ProfileAwareCustomTemplateSupportsContextThroughLyricsToken()
    {
        const string template = "{time}\n{title} - {artist}\n{elapsed} - {duration}\n{lyrics}";
        var context = new LyricContext("previous", "current", "next");
        Assert.Equal("19:31\nSong - Artist\n1:51 - 2:00\ncurrent",
            ProfileCompose(context, "Custom", template, "Current only"));
        Assert.Equal("19:31\nSong - Artist\n1:51 - 2:00\ncurrent\nnext",
            ProfileCompose(context, "Custom", template, "Current + next"));
        Assert.Equal("19:31\nSong - Artist\n1:51 - 2:00\nprevious\n› current\nnext",
            ProfileCompose(context, "Custom", template, "Adaptive"));
        var heavy = ProfileCompose(new("previous", "current", "next"), "Custom",
            new string('m', 130) + "\n{lyrics}", "Adaptive");
        Assert.Contains(new string('m', 130), heavy);
        Assert.InRange(ChatboxFormatter.Format(heavy, false).Length, 0, 144);
    }
    [Fact]
    public void ProfileAwareContextNeedsLyricsToken()
    {
        Assert.False(LyricContextComposer.SupportsContext("Status / Time", "{message}\n{time}"));
        Assert.False(LyricContextComposer.SupportsContext("Custom", "{message}"));
        Assert.True(LyricContextComposer.SupportsContext("Lyrics Only", "{lyrics}"));
        Assert.True(LyricContextComposer.SupportsContext("Song + Lyrics", "{lyrics}"));
        Assert.True(LyricContextComposer.SupportsContext("Custom", "{time}\n{lyrics}"));
        var context = new LyricContext("previous", "current", "next");
        Assert.Equal("msg", ProfileCompose(context, "Custom", "{message}", "Adaptive", message: "msg"));
        Assert.DoesNotContain("next", ProfileCompose(context, "Custom", "{message}", "Adaptive", message: "msg"));
        var summary = new DisplayProfile("x", "Custom", "Custom", "Adaptive", CustomTemplate: "{message}");
        Assert.DoesNotContain("Adaptive", summary.Summary);
        var supported = new DisplayProfile("y", "Custom", "Custom", "Adaptive", CustomTemplate: "{lyrics}");
        Assert.Contains("Adaptive", supported.Summary);
    }
    [Fact]
    public void ProfileAwareGapBudgetAndPreviewRules()
    {
        var gapped = new LyricContext("previous", "", "next");
        Assert.DoesNotContain("next", ProfileCompose(gapped, "Lyrics Only", "{lyrics}", "Adaptive"));
        Assert.DoesNotContain("next", ProfileCompose(gapped, "Custom", "{time}\n{lyrics}", "Previous + current + next"));
        var sized = ProfileCompose(new("p", new string('c', 70), new string('n', 72)), "Lyrics Only", "{lyrics}", "Adaptive");
        var sizedCompact = ProfileCompose(new("p", new string('c', 70), new string('n', 72)), "Lyrics Only", "{lyrics}", "Adaptive", compact: true);
        Assert.DoesNotContain(new string('n', 72), sizedCompact);
        Assert.Contains(new string('c', 70), sizedCompact);
        Assert.Contains(new string('n', 72), sized);
        var exact = ProfileCompose(new("p", new string('c', 70), new string('n', 73)), "Lyrics Only", "{lyrics}", "Adaptive");
        Assert.Contains(new string('n', 73), exact);
        var over = ProfileCompose(new("p", new string('c', 70), new string('n', 74)), "Lyrics Only", "{lyrics}", "Adaptive");
        Assert.DoesNotContain(new string('n', 74), over);
        var grapheme = string.Concat(Enumerable.Repeat("👨‍👩‍👧‍👦", 40));
        var gapped2 = LyricContextComposer.ComposeProfile(new("p", grapheme, "n"), CoreTests.Track, "Lyrics Only", "{lyrics}", "", "Adaptive", true,
            new DateTimeOffset(2026, 9, 7, 19, 31, 0, TimeSpan.Zero), 111);
        Assert.Equal(gapped2, new UTF8Encoding(false, true).GetString(new UTF8Encoding(false, true).GetBytes(gapped2)));
        Assert.InRange(ChatboxFormatter.Format(gapped2, true).Length, 0, 144);
        var nine = string.Join('\n', Enumerable.Repeat("current", 9));
        Assert.Equal(nine, ProfileCompose(new("old", nine, "next"), "Lyrics Only", "{lyrics}", "Adaptive"));
        var roundtrip = ProfileCompose(new("previous", "current", "next"), "Song + Lyrics", "{lyrics}", "Previous + current + next");
        Assert.Equal(roundtrip, ChatboxFormatter.Format(roundtrip, false));
    }
    [Fact]
    public async Task IgnoreSkipsAllSourcesRetainsImportsAndInvalidatesLateResults()
    {
        var data = new LocalData(root); var track = CoreTests.Track;
        Assert.True(data.SaveLocal(track,"[00:01]local")); Assert.True(data.SetIgnored(track,true));
        Assert.True(new LocalData(root).IsIgnored(track)); Assert.False(data.IsIgnored(track with {Title="Song (Live)"}));
        using var http = new HttpClient(new NoNetwork()); using var resolver = new LyricsResolver(http,data);
        Assert.Equal(LyricsOutcome.Ignored,(await resolver.ResolveAsync(track,default)).Outcome);
        Assert.Equal("[00:01]local",data.ReadLocal(track));
        Assert.True(data.SetIgnored(track,false)); Assert.False(File.Exists(Path.Combine(root,"ignored",track.Key+".json")));
        Assert.Equal(LyricsOutcome.Found,(await resolver.ResolveAsync(track,default)).Outcome);
        File.WriteAllText(Path.Combine(root,"ignored",track.Key+".json"),"{bad"); Assert.False(data.IsIgnored(track));
        var engine = new SynchronizationEngine {Enabled=true}; engine.Observe(CoreTests.Snapshot(1)); var oldEpoch=engine.Epoch;
        engine.InvalidateLyrics("Lyrics ignored for this recording");
        Assert.False(engine.Complete(oldEpoch,new(LrcParser.Parse("[00:01]stale"),"Synced"))); Assert.Equal("",engine.Output(0));
        Assert.Equal(1,engine.Position(0));
    }
    [Fact]
    public void ResumeFailsClosedWhenTheIgnoreDecisionCannotBeDeleted()
    {
        var data = new LocalData(root); var track = CoreTests.Track;
        Assert.True(data.SetIgnored(track, true));
        var path = Path.Combine(root, "ignored", track.Key + ".json");
        using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.False(data.SetIgnored(track, false));
        Assert.True(data.IsIgnored(track));
    }
    [Fact]
    public void OnlyAUsableLocalLrcBlocksRemoteManualMatching()
    {
        var data = new LocalData(root); var track = CoreTests.Track;
        Directory.CreateDirectory(Path.GetDirectoryName(data.LocalLrcPath(track))!);
        File.WriteAllText(data.LocalLrcPath(track), "plain untimed text");
        Assert.False(data.HasUsableLocalLyrics(track));
        Assert.True(data.SaveLocal(track, "[00:01]local priority"));
        Assert.True(data.HasUsableLocalLyrics(track));
        Assert.False(data.HasUsableLocalLyrics(track with { Duration = track.Duration + .25 }));
    }
    [Fact]
    public void QuickMessagesPersistButSelectingOneIsAnUnsentDraftEvenAfterLiveEditing()
    {
        var data = new LocalData(root); var library = data.ReadQuickMessages();
        var message = new QuickMessage("custom","Greeting","こんにちは 🎵");
        library = library.Save(message)!; Assert.True(data.SaveQuickMessages(library));
        Assert.Contains(message,data.ReadQuickMessages().Items);
        library = library.Save(message with {Name="Edited",Text="AFK 日本語"})!; Assert.True(data.SaveQuickMessages(library));
        Assert.Contains(data.ReadQuickMessages().Items,m=>m.Name=="Edited");
        var manual = new ManualChat(); manual.Edit("old live",true,0); manual.Send();
        manual.PrepareDraft(message.Text);
        Assert.Equal(message.Text,manual.Draft); Assert.True(manual.IsManual); Assert.Null(manual.Desired("automatic",1));
        Assert.False(manual.PendingSend); Assert.False(manual.Typing(1));
        manual.Send(); Assert.Equal(message.Text,manual.Desired("automatic",2));
        var diagnostics = new Diagnostics().Export(null,new(),"none","ready",0);
        Assert.DoesNotContain(message.Text,diagnostics);
        Assert.True(data.SaveQuickMessages(library.Delete(message.Id))); Assert.DoesNotContain(data.ReadQuickMessages().Items,m=>m.Id==message.Id);
        Assert.True(data.SaveQuickMessages(new(1,[]))); Assert.Empty(data.ReadQuickMessages().Items);
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"quick-messages.json")));
        Assert.Equal(0,json.RootElement.GetProperty("Items").GetArrayLength());
    }
    private sealed class NoNetwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken) => throw new InvalidOperationException("No network expected");
    }
    [Fact]
    public void FullUnicodeLibrariesCanBeReadAfterSuccessfulSave()
    {
        var data = new LocalData(root);
        var profiles = Enumerable.Range(0, ProfileLibrary.Maximum).Select(i => new DisplayProfile(
            new string('界', 62) + i.ToString("D2"), new string('名', 40),
            CustomTemplate: new string('語', 512), Message: new string('文', 512))).ToArray();
        var library = new ProfileLibrary(1, profiles[^1].Id, profiles);
        Assert.True(data.SaveProfiles(library));
        var restored = new LocalData(root).ReadProfiles(new());
        Assert.Equal(profiles.Length, restored.Items.Count);
        Assert.Equal(library.SelectedId, restored.SelectedId);
        Assert.Equal(profiles.Select(profile => JsonSerializer.Serialize(profile.PrepareForSave())),
            restored.Items.Select(profile => JsonSerializer.Serialize(profile)));

        var messages = Enumerable.Range(0, QuickMessageLibrary.Maximum).Select(i => new QuickMessage(
            new string('界', 62) + i.ToString("D2"), new string('名', 32), new string('文', 512))).ToArray();
        Assert.True(data.SaveQuickMessages(new(1, messages)));
        var restoredMessages = new LocalData(root).ReadQuickMessages();
        Assert.Equal(messages.Length, restoredMessages.Items.Count);
        Assert.Equal(messages, restoredMessages.Items.ToArray());
    }
    private static PlaybackSnapshot PlayingSnapshot(PlaybackSourceKind kind, PlaybackState state = PlaybackState.Playing) =>
        CoreTests.Snapshot(state: state) with { Source = kind };

    [Theory]
    [InlineData("No Spotify session", "Spotify · not detected", false)]
    [InlineData("Updating Spotify track", "Spotify · reconnecting", false)]
    [InlineData("Checking Spotify session", "Spotify · reconnecting", false)]
    [InlineData("Reading Spotify", "Spotify · reconnecting", false)]
    [InlineData("Refreshing Spotify playback", "Spotify · reconnecting", false)]
    [InlineData("Spotify unavailable · reconnecting", "Spotify · reconnecting", false)]
    public void ExplicitSpotifyNullSnapshotNeverClaimsPlayback(string status, string expected, bool choose)
    {
        var view = PresentationText.SourceView(PlaybackSourceMode.Spotify, null, null, status, false);
        Assert.Equal((expected, choose), view);
    }

    [Fact]
    public void SuspendPresentsReconnectingInsteadOfNotDetected()
    {
        Assert.Equal(("Spotify · reconnecting", false), PresentationText.SourceView(
            PlaybackSourceMode.Spotify, null, null, "Playback suspended", false));
        Assert.Equal(("Apple Music · reconnecting", false), PresentationText.SourceView(
            PlaybackSourceMode.AppleMusic, null, null, "Playback suspended", false));
    }

    [Theory]
    [InlineData(PlaybackState.Playing, "Spotify · playing")]
    [InlineData(PlaybackState.Paused, "Spotify · paused")]
    public void ExplicitSpotifySnapshotMatchesAuthoritativeState(PlaybackState state, string expected)
    {
        var view = PresentationText.SourceView(PlaybackSourceMode.Spotify, PlaybackSourceKind.Spotify,
            PlayingSnapshot(PlaybackSourceKind.Spotify, state), "status", false);
        Assert.Equal((expected, false), view);
    }

    [Theory]
    [InlineData("No Apple Music session", "Apple Music · not detected")]
    [InlineData("Checking Apple Music session", "Apple Music · reconnecting")]
    [InlineData("Updating track", "Apple Music · reconnecting")]
    [InlineData("Reading Apple Music", "Apple Music · reconnecting")]
    [InlineData("Refreshing playback after resume", "Apple Music · reconnecting")]
    [InlineData("Apple Music unavailable · reconnecting", "Apple Music · reconnecting")]
    public void ExplicitAppleNullSnapshotNeverClaimsPlayback(string status, string expected)
    {
        var view = PresentationText.SourceView(PlaybackSourceMode.AppleMusic, null, null, status, false);
        Assert.Equal((expected, false), view);
    }

    [Theory]
    [InlineData(PlaybackState.Playing, "Apple Music · playing")]
    [InlineData(PlaybackState.Paused, "Apple Music · paused")]
    public void ExplicitAppleSnapshotMatchesAuthoritativeState(PlaybackState state, string expected)
    {
        var view = PresentationText.SourceView(PlaybackSourceMode.AppleMusic, PlaybackSourceKind.AppleMusic,
            PlayingSnapshot(PlaybackSourceKind.AppleMusic, state), "status", false);
        Assert.Equal((expected, false), view);
    }

    [Theory]
    [InlineData(PlaybackSourceKind.Spotify, PlaybackState.Playing, "Automatic · Spotify playing")]
    [InlineData(PlaybackSourceKind.Spotify, PlaybackState.Paused, "Automatic · Spotify paused")]
    [InlineData(PlaybackSourceKind.AppleMusic, PlaybackState.Playing, "Automatic · Apple Music playing")]
    [InlineData(PlaybackSourceKind.AppleMusic, PlaybackState.Paused, "Automatic · Apple Music paused")]
    public void AutomaticSelectedSourceStatesItself(PlaybackSourceKind kind, PlaybackState state, string expected)
    {
        var view = PresentationText.SourceView(PlaybackSourceMode.Automatic, kind,
            PlayingSnapshot(kind, state), "status", false);
        Assert.Equal((expected, false), view);
    }

    [Fact]
    public void AutomaticAmbiguityExposesGuidanceAndAction()
    {
        const string status = "Apple Music and Spotify are both playing. Choose a playback source.";
        var view = PresentationText.SourceView(PlaybackSourceMode.Automatic, null, null, status, true);
        Assert.Equal((status, true), view);
        var waiting = PresentationText.SourceView(PlaybackSourceMode.Automatic, null, null, "Waiting for a music player", false);
        Assert.Equal(("Waiting for a music player", false), waiting);
        var checking = PresentationText.SourceView(PlaybackSourceMode.Automatic, null, null, "Checking music players", false);
        Assert.Equal(("Checking music players", false), checking);
    }

    [Fact]
    public void SourceViewChangesWhenSnapshotArrivesUnderIdenticalStatus()
    {
        var before = PresentationText.SourceView(PlaybackSourceMode.Spotify, null, null, "status", false);
        var after = PresentationText.SourceView(PlaybackSourceMode.Spotify, PlaybackSourceKind.Spotify,
            PlayingSnapshot(PlaybackSourceKind.Spotify), "status", false);
        Assert.NotEqual(before, after);
        Assert.Equal("Spotify · not detected", before.Text);
        var moved = PresentationText.SourceView(PlaybackSourceMode.Spotify, PlaybackSourceKind.AppleMusic,
            PlayingSnapshot(PlaybackSourceKind.AppleMusic), "status", false);
        Assert.NotEqual(after, moved);
    }

    [Fact]
    public void ProfileAwareCenterAndRightEvaluateAlignedCost()
    {
        var at = new DateTimeOffset(2026, 9, 7, 19, 31, 0, TimeSpan.Zero);
        var template = new string('m', 60) + "\n{lyrics}";
        var context = new LyricContext("p", new string('c', 20), new string('n', 20));
        var left = LyricContextComposer.ComposeProfile(context, CoreTests.Track, "Custom", template, "", "Adaptive", false, at, null, "Left");
        Assert.Contains(new string('n', 20), left);
        var right = LyricContextComposer.ComposeProfile(context, CoreTests.Track, "Custom", template, "", "Adaptive", false, at, null, "Right");
        Assert.DoesNotContain(new string('n', 20), right);
        Assert.Contains(new string('c', 20), right);
        Assert.Contains(new string('m', 60), right);
        var aligned = MessageLayout.Align(right, "Right");
        Assert.InRange(aligned.Length, 0, 144);
        Assert.Equal(aligned, ChatboxFormatter.Format(aligned, false));
        var fuller = MessageLayout.Align(
            LyricContextComposer.ComposeProfile(context, CoreTests.Track, "Custom", template, "", "Current + next", false, at, null, "Left"), "Right");
        Assert.True(fuller.Length > 144);
        var compact = LyricContextComposer.ComposeProfile(context, CoreTests.Track, "Custom", template, "", "Adaptive", true, at, null, "Right");
        Assert.Equal(right, compact);
    }

    [Fact]
    public void ProfileAwareAlignmentPreservesUnicodeLineLimitsAndMusicInfo()
    {
        var at = new DateTimeOffset(2026, 9, 7, 19, 31, 0, TimeSpan.Zero);
        Assert.Equal("p\n› 日本語\nn", LyricContextComposer.ComposeProfile(
            new("p", "日本語", "n"), CoreTests.Track, "Custom", "{lyrics}", "", "Adaptive", false, at, null, "Center"));
        var nine = string.Join('\n', Enumerable.Repeat("current", 9));
        var aligned = MessageLayout.Align(LyricContextComposer.ComposeProfile(
            new("old", nine, "next"), CoreTests.Track, "Custom", "{lyrics}", "", "Adaptive", false, at, null, "Center"), "Center");
        Assert.Equal(8, aligned.Count(c => c == '\n'));
        Assert.Equal(
            LyricContextComposer.ComposeProfile(new("p", "c", "n"), CoreTests.Track, "Song + Lyrics", "{lyrics}", "", "Adaptive", false, at, null, "Left"),
            LyricContextComposer.ComposeProfile(new("p", "c", "n"), CoreTests.Track, "Song + Lyrics", "{lyrics}", "", "Adaptive", false, at, null, "Center"));
    }

    public void Dispose() { if(Directory.Exists(root)) Directory.Delete(root,true); }
}
