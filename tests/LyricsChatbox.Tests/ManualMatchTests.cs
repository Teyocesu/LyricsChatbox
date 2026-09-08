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
    public async Task ExplicitSelectionIsRequiredForRecordingMismatchAndReplayUsesOneCachedBody()
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
        Assert.DoesNotContain("chosen timed line", mapping);
        Assert.True(data.ForgetManualAssociation(CoreTests.Track));
        Assert.Null(data.ReadCachedLyrics(CoreTests.Track)); Assert.Null(data.ReadManualAssociation(CoreTests.Track));
    }
    [Fact]
    public async Task MissingSavedCandidateFallsBackAndLocalImportWins()
    {
        var data = new LocalData(root); var choice = new ManualCandidate("LRCLIB", Alternative with { SyncedLyrics = null });
        data.SaveManualAssociation(CoreTests.Track, choice, Alternative);
        File.Delete(Path.Combine(root, "cache", CoreTests.Track.Key + ".json"));
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
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(send(request)); }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
