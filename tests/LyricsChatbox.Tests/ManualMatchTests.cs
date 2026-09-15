using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace LyricsChatbox.Tests;

public sealed class ManualMatchTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "LyricsChatbox.Tests", Guid.NewGuid().ToString("N"));
    private static readonly LyricsRecord Alternative = new(123, "Song (Live)", "Artist", "Live Album", 120, false, "[00:01]chosen timed line");
    private static HttpResponseMessage Json(object item) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(item)) };
    [Fact]
    public async Task ExplicitSelectionIsRequiredAndReplayUsesTheAtomicBundle()
    {
        var data = new LocalData(root); var calls = 0;
        using var http = new HttpClient(new Handler(request =>
        {
            calls++; return Json(request.RequestUri!.AbsolutePath.EndsWith("/search") ? new[] { Alternative, Alternative with { Id = 124, AlbumName = "Other Live Album" } } : Alternative);
        }));
        using var resolver = new LyricsResolver(http, data);
        Assert.Null(LyricsMatching.Score(CoreTests.Track, Alternative));
        var candidates = await resolver.SearchManualAsync(CoreTests.Track, "Song Artist", default);
        Assert.Equal(2, candidates.Count); Assert.All(candidates, c => Assert.Null(c.Metadata.SyncedLyrics));
        Assert.Null(data.ReadManualAssociation(CoreTests.Track));
        var choice = candidates[0]; var fetched = await resolver.FetchManualAsync(choice, default);
        Assert.Equal(LyricsOutcome.Found, fetched.Outcome);
        Assert.True(data.SaveManualAssociation(CoreTests.Track, choice, fetched.Record!));
        var before = calls; var replay = await resolver.ResolveAsync(CoreTests.Track, default);
        Assert.Equal("chosen timed line", replay.Timeline!.Current(1)); Assert.Equal(before, calls);
        Assert.Null(data.ReadCachedLyrics(CoreTests.Track with { Artist = "Another artist" }));
        var mapping = File.ReadAllText(Path.Combine(root, "matches", CoreTests.Track.Key + ".json"));
        Assert.Contains("\"Version\": 2", mapping);
        Assert.Contains("chosen timed line", mapping);
        Assert.False(File.Exists(Path.Combine(root, "cache", CoreTests.Track.Key + ".json")));
        Assert.True(data.ForgetManualAssociation(CoreTests.Track));
        Assert.Null(data.ReadCachedLyrics(CoreTests.Track)); Assert.Null(data.ReadManualAssociation(CoreTests.Track));
    }
    [Fact]
    public async Task MissingSavedCandidateFallsBackAndLocalImportWins()
    {
        var data = new LocalData(root); var choice = new ManualCandidate("LRCLIB", Alternative with { SyncedLyrics = null });
        data.SaveManualAssociation(CoreTests.Track, choice, Alternative);
        File.WriteAllText(Path.Combine(root, "matches", CoreTests.Track.Key + ".json"),
            JsonSerializer.Serialize(new ManualAssociation(1, CoreTests.Track.Key, "LRCLIB", choice.Metadata)));
        var normal = Alternative with { Id = 3, TrackName = CoreTests.Track.Title, AlbumName = CoreTests.Track.Album, ArtistName = CoreTests.Track.Artist };
        using var http = new HttpClient(new Handler(request => request.RequestUri!.AbsolutePath.EndsWith("/get/123") ? new(HttpStatusCode.NotFound) : Json(normal)));
        using var resolver = new LyricsResolver(http, data);
        Assert.NotNull((await resolver.ResolveAsync(CoreTests.Track, default)).Timeline);
        data.SaveLocal(CoreTests.Track, "[00:01]local priority");
        Assert.Equal("local priority", (await resolver.ResolveAsync(CoreTests.Track, default)).Timeline!.Current(1));
    }
    [Fact]
    public async Task ChangedProviderIdentityAndUntimedCandidatesCannotBeSelected()
    {
        using var http = new HttpClient(new Handler(_ => Json(Alternative with { ArtistName = "Replaced artist" })));
        using var resolver = new LyricsResolver(http, new(root));
        var choice = new ManualCandidate("LRCLIB", Alternative with { SyncedLyrics = null });
        Assert.Equal(LyricsOutcome.NotFound, (await resolver.FetchManualAsync(choice, default)).Outcome);
        Assert.False(new LocalData(root).SaveManualAssociation(CoreTests.Track, choice, Alternative with { SyncedLyrics = "plain" }));
    }
    [Fact]
    public async Task CancelledLateManualResponseCannotInstallAnAssociation()
    {
        using var cancel = new CancellationTokenSource();
        using var http = new HttpClient(new Handler(_ => { cancel.Cancel(); return Json(Alternative); }));
        var data = new LocalData(root); using var resolver = new LyricsResolver(http, data);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resolver.FetchManualAsync(new("LRCLIB", Alternative), cancel.Token));
        Assert.Null(data.ReadManualAssociation(CoreTests.Track));
    }
    [Fact]
    public void FailedAtomicReplacementPreservesThePreviousManualMatch()
    {
        var data = new LocalData(root);
        var choice = new ManualCandidate("LRCLIB", Alternative with { SyncedLyrics = null });
        Assert.True(data.SaveManualAssociation(CoreTests.Track, choice, Alternative));
        var path = Path.Combine(root, "matches", CoreTests.Track.Key + ".json");
        using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.False(data.SaveManualAssociation(CoreTests.Track, choice, Alternative with { SyncedLyrics = "[00:02]replacement" }));

        Assert.Equal("chosen timed line", data.ReadCachedLyrics(CoreTests.Track)!.Record.SyncedLyrics![7..]);
        Assert.Empty(Directory.GetFiles(Path.Combine(root, "matches"), "*.tmp"));
    }
    [Fact]
    public void AssociationFailureLeavesTheExistingAutomaticCacheIntact()
    {
        var data = new LocalData(root);
        var automatic = new LyricsRecord(5, CoreTests.Track.Title, CoreTests.Track.Artist, CoreTests.Track.Album,
            CoreTests.Track.Duration, false, "[00:01]automatic");
        data.SaveCache(CoreTests.Track, automatic);
        Directory.CreateDirectory(Path.Combine(root, "matches", CoreTests.Track.Key + ".json"));

        Assert.False(data.SaveManualAssociation(CoreTests.Track,
            new("LRCLIB", Alternative with { SyncedLyrics = null }), Alternative));
        var cached = data.ReadCachedLyrics(CoreTests.Track);
        Assert.False(cached!.Manual);
        Assert.Equal("[00:01]automatic", cached.Record.SyncedLyrics);
    }
    [Fact]
    public void SavedManualChoiceKeepsPriorityWhenItsBundledLyricsExpire()
    {
        var data = new LocalData(root);
        var automatic = new LyricsRecord(5, CoreTests.Track.Title, CoreTests.Track.Artist, CoreTests.Track.Album,
            CoreTests.Track.Duration, false, "[00:01]automatic");
        data.SaveCache(CoreTests.Track, automatic);
        var choice = new ManualCandidate("LRCLIB", Alternative with { SyncedLyrics = null });
        Assert.True(data.SaveManualAssociation(CoreTests.Track, choice, Alternative));
        var path = Path.Combine(root, "matches", CoreTests.Track.Key + ".json");
        var bundle = JsonSerializer.Deserialize<ManualAssociation>(File.ReadAllText(path))!;
        File.WriteAllText(path, JsonSerializer.Serialize(bundle with { StoredUtc = DateTimeOffset.UtcNow.AddDays(-31) }));

        Assert.NotNull(data.ReadManualAssociation(CoreTests.Track));
        Assert.Null(data.ReadCachedLyrics(CoreTests.Track));
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(send(request)); }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
