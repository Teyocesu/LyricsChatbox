using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace LyricsChatbox.Tests;

public sealed class SecondaryProviderTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "LyricsChatbox.Tests", Guid.NewGuid().ToString("N"));
    private LocalData Data => new(root);
    private static LyricsRecord Record => new(9, "Song", "Artist", "Album", 120, false, "[00:01]日本語 🎵\n[00:03]next");
    private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body)) };
    private static object Song(int id = 9, string title = "Song", string artist = "Artist", int duration = 120000, string album = "Album") =>
        new { id, name = title, dt = duration, ar = new[] { new { name = artist } }, al = new { name = album } };

    [Fact]
    public async Task SecondaryUsesSearchMetadataAndCachesWithProvenanceOnlyAfterPrimaryMiss()
    {
        using var handler = new Handler((request, _) =>
        {
            if (request.RequestUri!.Host == "lrclib.net") return Task.FromResult(request.RequestUri.AbsolutePath.EndsWith("get")
                ? new HttpResponseMessage(HttpStatusCode.NotFound) : Json(Array.Empty<object>()));
            Assert.Equal("https", request.RequestUri.Scheme);
            Assert.False(request.Headers.Contains("Cookie"));
            Assert.False(request.Headers.Contains("Authorization"));
            return Task.FromResult(request.Method == HttpMethod.Post ? Json(new { code = 200, result = new { songs = new[] { Song() } } })
                : Json(new { code = 200, lrc = new { lyric = Record.SyncedLyrics } }));
        });
        using var http = new HttpClient(handler);
        using var secondary = new NetEaseLyricsProvider(http);
        using var resolver = new LyricsResolver(http, Data, secondary);
        var progress = new List<string>();
        var result = await resolver.ResolveAsync(CoreTests.Track, default, progress.Add);
        Assert.Equal(LyricsOutcome.Found, result.Outcome);
        Assert.Equal("NetEase", result.Provider);
        Assert.Equal("日本語 🎵", result.Timeline!.Current(1));
        Assert.Single(progress);
        Assert.Equal(4, handler.Calls);
        Assert.Equal("NetEase", Data.ReadCachedLyrics(CoreTests.Track)!.Provider);
        var cached = await resolver.ResolveAsync(CoreTests.Track, default);
        Assert.Equal("NetEase", cached.Provider);
        Assert.Contains("cache", cached.Status);
        Assert.Equal(4, handler.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrimarySuccessOrInstrumentalNeverCallsSecondary(bool instrumental)
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Json(Record with { Instrumental = instrumental }))));
        var secondary = new FakeProvider((_, _) => throw new InvalidOperationException("Must not call fallback"));
        using var resolver = new LyricsResolver(http, Data, secondary);
        var result = await resolver.ResolveAsync(CoreTests.Track, default);
        Assert.Equal(instrumental ? LyricsOutcome.Instrumental : LyricsOutcome.Found, result.Outcome);
        Assert.Equal(0, secondary.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnusablePrimaryOrPrimaryRateLimitUsesFallback(bool rateLimited)
    {
        using var http = new HttpClient(new Handler((request, _) => Task.FromResult(rateLimited
            ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            : request.RequestUri!.AbsolutePath.EndsWith("get") ? Json(Record with { SyncedLyrics = "untimed" })
            : Json(new[] { Record with { ArtistName = "Wrong artist" } }))));
        var secondary = new FakeProvider((_, _) => Task.FromResult(new ProviderResult(LyricsOutcome.Found, Record)));
        using var resolver = new LyricsResolver(http, Data, secondary);
        Assert.Equal("NetEase", (await resolver.ResolveAsync(CoreTests.Track, default)).Provider);
        Assert.Equal(1, secondary.Calls);
    }

    [Fact]
    public async Task CoordinatorRejectsAnInvalidSuccessFromAnAdapter()
    {
        using var http = new HttpClient(new Handler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("get")
            ? new HttpResponseMessage(HttpStatusCode.NotFound) : Json(Array.Empty<object>()))));
        var secondary = new FakeProvider((_, _) => Task.FromResult(new ProviderResult(LyricsOutcome.Found, Record with { ArtistName = "Wrong artist" })));
        using var resolver = new LyricsResolver(http, Data, secondary);
        var result = await resolver.ResolveAsync(CoreTests.Track, default);
        Assert.Equal(LyricsOutcome.Rejected, result.Outcome);
        Assert.Null(result.Timeline);
        Assert.Null(Data.ReadCache(CoreTests.Track));
    }

    [Fact]
    public async Task MalformedFallbackDoesNotPersistANegativeResultAndRecovers()
    {
        var malformed = true;
        using var http = new HttpClient(new Handler((request, _) =>
        {
            if (request.RequestUri!.Host == "lrclib.net") return Task.FromResult(request.RequestUri.AbsolutePath.EndsWith("get")
                ? new HttpResponseMessage(HttpStatusCode.NotFound) : Json(Array.Empty<object>()));
            if (malformed) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{malformed") });
            return Task.FromResult(request.Method == HttpMethod.Post ? Json(new { code = 200, result = new { songs = new[] { Song() } } })
                : Json(new { code = 200, lrc = new { lyric = Record.SyncedLyrics } }));
        }));
        using var provider = new NetEaseLyricsProvider(http);
        using var resolver = new LyricsResolver(http, Data, provider);
        var failure = await resolver.ResolveAsync(CoreTests.Track, default);
        Assert.Equal(LyricsOutcome.Unavailable, failure.Outcome);
        Assert.NotNull(failure.RetryAt);
        Assert.Null(Data.ReadCache(CoreTests.Track));
        malformed = false;
        Assert.NotNull((await resolver.ResolveAsync(CoreTests.Track, default)).Timeline);
    }

    [Fact]
    public async Task CanceledSecondaryCompletionCannotSaveOrAffectANewerEpoch()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new Handler((r, _) => Task.FromResult(r.RequestUri!.AbsolutePath.EndsWith("get")
            ? new HttpResponseMessage(HttpStatusCode.NotFound) : Json(Array.Empty<object>()))));
        var secondary = new FakeProvider(async (_, _) => { entered.SetResult(); await release.Task; return new(LyricsOutcome.Found, Record); });
        using var resolver = new LyricsResolver(http, Data, secondary);
        using var cancellation = new CancellationTokenSource();
        var task = resolver.ResolveAsync(CoreTests.Track, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cancellation.Cancel(); release.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Null(Data.ReadCache(CoreTests.Track));
        var engine = new SynchronizationEngine();
        engine.Observe(CoreTests.Snapshot());
        var old = engine.Epoch;
        engine.Observe(CoreTests.Snapshot(track: CoreTests.Track with { Title = "B" }));
        Assert.False(engine.ReportProgress(old, "stale progress"));
        Assert.False(engine.Complete(old, new(LrcParser.Parse(Record.SyncedLyrics), "stale result")));
    }

    [Theory]
    [InlineData("Wrong", "Artist", 120000, "Album")]
    [InlineData("Song", "Wrong", 120000, "Album")]
    [InlineData("Song", "Artist", 123000, "Album")]
    [InlineData("Song", "Artist", 120000, "Album Live")]
    public async Task WrongMetadataIsRejectedBeforeAnyLyricDownload(string title, string artist, int duration, string album)
    {
        using var handler = new Handler((_, _) => Task.FromResult(Json(new { code = 200, result = new { songs = new[] { Song(title: title, artist: artist, duration: duration, album: album) } } })));
        using var http = new HttpClient(handler);
        using var provider = new NetEaseLyricsProvider(http);
        Assert.Equal(LyricsOutcome.Rejected, (await provider.FindAsync(CoreTests.Track, default)).Outcome);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConflictingAndExcessiveContendersFailClosed(bool excessive)
    {
        using var handler = new Handler((request, _) => Task.FromResult(request.Method == HttpMethod.Post
            ? Json(new { code = 200, result = new { songs = Enumerable.Range(1, excessive ? 4 : 2).Select(id => Song(id)).ToArray() } })
            : Json(new { code = 200, lrc = new { lyric = "[00:01]" + request.RequestUri!.Query } })));
        using var http = new HttpClient(handler);
        using var provider = new NetEaseLyricsProvider(http);
        Assert.Equal(LyricsOutcome.Ambiguous, (await provider.FindAsync(CoreTests.Track, default)).Outcome);
        Assert.Equal(excessive ? 1 : 3, handler.Calls);
    }

    [Fact]
    public async Task UnsyncedMissingAndEncryptedResponsesAreNotAccepted()
    {
        foreach (var body in new object[] { new { code = 200, result = "encrypted" }, new { code = 200, result = new { songs = new[] { Song() } } } })
        {
            using var http = new HttpClient(new Handler((r, _) => Task.FromResult(Json(r.Method == HttpMethod.Post ? body : new { code = 200, lrc = new { lyric = "untimed text" } }))));
            using var provider = new NetEaseLyricsProvider(http);
            var result = await provider.FindAsync(CoreTests.Track, default);
            Assert.NotEqual(LyricsOutcome.Found, result.Outcome);
            Assert.Null(result.Record);
        }
    }

    [Fact]
    public async Task SecondaryRateLimitIsSharedAcrossTracksAndDoesNotPoisonPrimary()
    {
        using var handler = new Handler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(1));
            return Task.FromResult(response);
        });
        using var http = new HttpClient(handler);
        using var provider = new NetEaseLyricsProvider(http);
        var first = await provider.FindAsync(CoreTests.Track, default);
        var second = await provider.FindAsync(CoreTests.Track with { Title = "B" }, default);
        Assert.Equal(LyricsOutcome.RateLimited, second.Outcome);
        Assert.Equal(first.RetryAt, second.RetryAt);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(500)]
    public async Task InvalidAndOversizedResponsesRemainTransient(int status)
    {
        using var http = new HttpClient(new Handler((_, _) =>
        {
            var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("malformed") };
            response.Content.Headers.ContentLength = 2_000_001;
            return Task.FromResult(response);
        }));
        using var provider = new NetEaseLyricsProvider(http);
        var result = await provider.FindAsync(CoreTests.Track, default);
        Assert.Equal(LyricsOutcome.Unavailable, result.Outcome);
        Assert.NotNull(result.RetryAt);
    }

    [Fact]
    public async Task PrimaryDeadlineEntersFallbackWithoutAChainOfTwelveSecondWaits()
    {
        using var http = new HttpClient(new Handler(async (_, token) => { await Task.Delay(Timeout.Infinite, token); return Json(Record); }));
        var secondary = new FakeProvider((_, _) => Task.FromResult(new ProviderResult(LyricsOutcome.Found, Record)));
        using var resolver = new LyricsResolver(http, Data, secondary);
        var task = resolver.ResolveAsync(CoreTests.Track, default);
        var result = await task.WaitAsync(TimeSpan.FromSeconds(8));
        Assert.NotNull(result.Timeline);
        Assert.Equal(1, secondary.Calls);
    }

    [Fact]
    public async Task SecondaryDeadlineCancelsTransportAndReturnsATransientResult()
    {
        using var http = new HttpClient(new Handler(async (_, token) =>
        { await Task.Delay(Timeout.Infinite, token); return Json(new { code = 200 }); }));
        using var provider = new NetEaseLyricsProvider(http);
        var result = await provider.FindAsync(CoreTests.Track, default).WaitAsync(TimeSpan.FromSeconds(9));
        Assert.Equal(LyricsOutcome.Timeout, result.Outcome);
        Assert.NotNull(result.RetryAt);
        Assert.Null(result.Record);
    }

    [Fact]
    public void OldCacheDefaultsToLrclibAndUnrecognizedProvenanceIsRejected()
    {
        var path = Path.Combine(root, "cache", CoreTests.Track.Key + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new { Version = 1, TrackKey = CoreTests.Track.Key, StoredUtc = DateTimeOffset.UtcNow, Record }));
        Assert.Equal("LRCLIB", Data.ReadCachedLyrics(CoreTests.Track)!.Provider);
        Data.SaveCache(CoreTests.Track, Record, "Unknown");
        Assert.Null(Data.ReadCachedLyrics(CoreTests.Track));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Calls++; return send(request, token); }
    }
    private sealed class FakeProvider(Func<TrackIdentity, CancellationToken, Task<ProviderResult>> find) : ISyncedLyricsProvider
    {
        public string Name => "NetEase";
        public int Calls { get; private set; }
        public Task<ProviderResult> FindAsync(TrackIdentity track, CancellationToken token) { Calls++; return find(track, token); }
    }
}
