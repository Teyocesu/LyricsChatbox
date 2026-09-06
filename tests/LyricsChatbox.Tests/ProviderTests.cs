using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LyricsChatbox.Tests;

public sealed class ProviderTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "LyricsChatbox.Tests", Guid.NewGuid().ToString("N"));
    private static LyricsRecord Record => new(1, "Song", "Artist", "Album", 120, false, "[00:01]first\n[00:03]日本語");
    private LocalData Data => new(root);
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
        { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };

    [Fact]
    public async Task DirectLookupUsesIdentifyingHeadersAndValidatedCacheThenLocalPriority()
    {
        using var handler = new Handler((request, _) =>
        {
            Assert.Equal("https", request.RequestUri!.Scheme);
            Assert.Contains("LyricsChatbox/", request.Headers.UserAgent.ToString());
            Assert.Contains("duration=120", request.RequestUri.Query);
            return Task.FromResult(Json(Record));
        });
        using var http = new HttpClient(handler);
        using var resolver = new LyricsResolver(http, Data);
        var first = await resolver.ResolveAsync(CoreTests.Track, default);
        Assert.Equal("first", first.Timeline!.Current(1));
        var cached = await resolver.ResolveAsync(CoreTests.Track, default);
        Assert.Contains("cache", cached.Status);
        Assert.Equal(1, handler.Calls);
        Assert.True(Data.SaveLocal(CoreTests.Track, "[00:01]local"));
        var local = await resolver.ResolveAsync(CoreTests.Track, default);
        Assert.Equal("local", local.Timeline!.Current(1));
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnsynchronizedIsNotTimedAndInstrumentalIsAValidCachedState(bool instrumental)
    {
        using var handler = new Handler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("search")
            ? Json(new[] { Record with { SyncedLyrics = null } }) : Json(Record with { Instrumental = instrumental, SyncedLyrics = null })));
        using var http = new HttpClient(handler);
        using var resolver = new LyricsResolver(http, Data);
        var result = await resolver.ResolveAsync(CoreTests.Track, default);
        Assert.Null(result.Timeline);
        Assert.Equal(instrumental ? "Instrumental" : "No synchronized lyrics", result.Status);
        Assert.Equal(instrumental ? 1 : 2, handler.Calls);
        if (instrumental)
        {
            Assert.Equal("Instrumental", (await resolver.ResolveAsync(CoreTests.Track, default)).Status);
            Assert.Equal(1, handler.Calls);
        }
    }

    [Fact]
    public async Task WrongDirectResultFallsBackAndConflictingSearchFailsClosed()
    {
        using var handler = new Handler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("get")
            ? Json(Record with { ArtistName = "Wrong artist" })
            : Json(new object?[] { null, Record, Record with { Id = 2, SyncedLyrics = "[00:02]different" } })));
        using var http = new HttpClient(handler);
        using var resolver = new LyricsResolver(http, Data);
        var result = await resolver.ResolveAsync(CoreTests.Track, default);
        Assert.Null(result.Timeline);
        Assert.Equal("Ambiguous lyrics match", result.Status);
        Assert.Null(Data.ReadCache(CoreTests.Track));
    }

    [Theory]
    [InlineData(500)]
    [InlineData(200)]
    public async Task ProviderErrorsAndMalformedJsonDoNotPoisonALaterTrack(int status)
    {
        var calls = 0;
        using var handler = new Handler((_, _) => Task.FromResult(++calls == 1
            ? new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("not json") }
            : Json(Record with { TrackName = "B" })));
        using var http = new HttpClient(handler);
        using var resolver = new LyricsResolver(http, Data);
        var failed = await resolver.ResolveAsync(CoreTests.Track, default);
        Assert.Equal("Lyrics provider unavailable", failed.Status);
        Assert.NotNull(failed.RetryAt);
        var later = await resolver.ResolveAsync(CoreTests.Track with { Title = "B" }, default);
        Assert.NotNull(later.Timeline);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetryAfterDateOrDeltaAppliesAcrossTracksWithoutAnotherRequest(bool date)
    {
        var until = DateTimeOffset.UtcNow.AddMinutes(2);
        using var handler = new Handler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = date ? new RetryConditionHeaderValue(until) : new RetryConditionHeaderValue(TimeSpan.FromMinutes(2));
            return Task.FromResult(response);
        });
        using var http = new HttpClient(handler);
        using var resolver = new LyricsResolver(http, Data);
        var first = await resolver.ResolveAsync(CoreTests.Track, default);
        var next = await resolver.ResolveAsync(CoreTests.Track with { Title = "B" }, default);
        Assert.Equal("Lyrics provider rate limited", next.Status);
        Assert.True(first.RetryAt >= until.AddSeconds(-1));
        Assert.Equal(first.RetryAt, next.RetryAt);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task CancellationOfInFlightLookupReleasesNetworkAndCannotSaveStaleSuccess()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var handler = new Handler(async (_, _) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                entered.SetResult();
                await release.Task; // Simulates transport finishing after cancellation.
                return Json(Record);
            }
            return Json(Record with { TrackName = "B" });
        });
        using var http = new HttpClient(handler);
        using var resolver = new LyricsResolver(http, Data);
        using var cancel = new CancellationTokenSource();
        var stale = resolver.ResolveAsync(CoreTests.Track, cancel.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancel.Cancel(); release.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stale);
        Assert.Null(Data.ReadCache(CoreTests.Track));
        var later = await resolver.ResolveAsync(CoreTests.Track with { Title = "B" }, default);
        Assert.NotNull(later.Timeline);
    }

    [Fact]
    public async Task ExcessiveResponseIsRejectedAndMissingMetadataMakesNoRequest()
    {
        using var handler = new Handler((_, _) =>
        {
            var response = Json(Record);
            response.Content.Headers.ContentLength = 4_000_001;
            return Task.FromResult(response);
        });
        using var http = new HttpClient(handler);
        using var resolver = new LyricsResolver(http, Data);
        Assert.Null((await resolver.ResolveAsync(CoreTests.Track with { Duration = double.NaN }, default)).Timeline);
        Assert.Equal(0, handler.Calls);
        Assert.Equal("Lyrics provider unavailable", (await resolver.ResolveAsync(CoreTests.Track, default)).Status);
    }

    [Fact]
    public void SettingsAndCacheRecoverFromCorruptionAndPersistUnicode()
    {
        Directory.CreateDirectory(root);
        var settings = new AppSettings(true, 1.2, "::1", 9010);
        Assert.True(Data.SaveSettings(settings));
        Assert.Equal(settings, Data.ReadSettings());
        foreach (var invalid in new[] { "{", "null", "{\"Host\":null}", "{\"Port\":0}", "{\"Offset\":100}" })
        {
            File.WriteAllText(Path.Combine(root, "settings.json"), invalid);
            Assert.Equal(new AppSettings(), Data.ReadSettings());
        }
        Data.SaveCache(CoreTests.Track, Record);
        Assert.Equal(Record, Data.ReadCache(CoreTests.Track));
        var path = Path.Combine(root, "cache", CoreTests.Track.Key + ".json");
        foreach (var invalid in new[] { "{", "null", JsonSerializer.Serialize(new { Version = 1, TrackKey = CoreTests.Track.Key, StoredUtc = DateTimeOffset.UtcNow, Record = (object?)null }) })
        {
            File.WriteAllText(path, invalid);
            Assert.Null(Data.ReadCache(CoreTests.Track));
        }
        Assert.True(Data.SaveLocal(CoreTests.Track, "[00:01]日本語 🎵"));
        Assert.Contains("日本語 🎵", Data.ReadLocal(CoreTests.Track));
        Assert.False(Data.SaveLocal(CoreTests.Track, "plain lyrics"));
        var other = CoreTests.Track with { Album = "Different release" };
        Assert.Null(Data.ReadLocal(other));
    }

    public void Dispose()
    {
        // Unique test-owned directory, never a user data path.
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; return send(request, cancellationToken); }
    }
}
