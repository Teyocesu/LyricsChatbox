using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace LyricsChatbox;

public enum LyricsOutcome { Found, Instrumental, NotFound, Rejected, Ambiguous, RateLimited, Timeout, Unavailable }
public record ProviderResult(LyricsOutcome Outcome, LyricsRecord? Record = null, DateTimeOffset? RetryAt = null);
public interface ISyncedLyricsProvider
{
    string Name { get; }
    Task<ProviderResult> FindAsync(TrackIdentity track, CancellationToken token);
    Task<IReadOnlyList<LyricsRecord>> SearchManualAsync(string query, CancellationToken token) => Task.FromResult<IReadOnlyList<LyricsRecord>>([]);
    Task<ProviderResult> FetchManualAsync(LyricsRecord expected, CancellationToken token) => Task.FromResult(new ProviderResult(LyricsOutcome.Unavailable));
}

// Community REST endpoint, best effort: no cookies, login, HTML parsing or encrypted API emulation.
public sealed partial class NetEaseLyricsProvider(HttpClient http) : ISyncedLyricsProvider, IDisposable
{
    public string Name => "NetEase";
    public static readonly TimeSpan Deadline = TimeSpan.FromSeconds(6);
    private readonly SemaphoreSlim network = new(1, 1);
    private DateTimeOffset nextRequest, cooldown;

    public async Task<ProviderResult> FindAsync(TrackIdentity track, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(Deadline);
        var ct = deadline.Token;
        try
        {
            if (string.IsNullOrWhiteSpace(track.Title) || string.IsNullOrWhiteSpace(track.Artist) ||
                !double.IsFinite(track.Duration) || track.Duration is < 1 or > 3600)
                return new(LyricsOutcome.Rejected);
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://music.163.com/api/cloudsearch/pc")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                { ["s"] = track.Title + " " + track.Artist, ["type"] = "1", ["limit"] = "20", ["offset"] = "0" })
            };
            using var search = await RequestAsync(request, ct);
            var root = search.RootElement;
            if (!Succeeded(root)) return new(LyricsOutcome.Unavailable, RetryAt: Retry());
            if (!root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object)
                return new(LyricsOutcome.Unavailable, RetryAt: Retry());
            if (!result.TryGetProperty("songs", out var songs)) return new(LyricsOutcome.NotFound);
            if (songs.ValueKind != JsonValueKind.Array || songs.GetArrayLength() > 20) return new(LyricsOutcome.Rejected);
            var candidates = new List<LyricsRecord>();
            foreach (var song in songs.EnumerateArray())
            {
                var candidate = ReadSong(song);
                if (candidate is not null && LyricsMatching.Score(track, candidate).HasValue) candidates.Add(candidate);
            }
            if (candidates.Count == 0) return new(songs.GetArrayLength() == 0 ? LyricsOutcome.NotFound : LyricsOutcome.Rejected);
            candidates = candidates.DistinctBy(c => c.Id).OrderByDescending(c => LyricsMatching.Score(track, c)).ToList();
            // Bound fan-out, without hiding an unexamined conflicting recording in the winning score band.
            var bestScore = LyricsMatching.Score(track, candidates[0])!.Value;
            var contenders = candidates.Where(c => bestScore - LyricsMatching.Score(track, c) < 3).ToArray();
            if (contenders.Length > 3) return new(LyricsOutcome.Ambiguous);
            var timed = new List<LyricsRecord>();
            foreach (var candidate in contenders)
            {
                using var lyricRequest = new HttpRequestMessage(HttpMethod.Get,
                    $"https://music.163.com/api/song/lyric?id={candidate.Id}&lv=-1&kv=-1&tv=-1");
                using var lyrics = await RequestAsync(lyricRequest, ct);
                if (!Succeeded(lyrics.RootElement)) return new(LyricsOutcome.Unavailable, RetryAt: Retry());
                var text = lyrics.RootElement.TryGetProperty("lrc", out var lrc) && lrc.ValueKind == JsonValueKind.Object &&
                    lrc.TryGetProperty("lyric", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                if (LrcParser.Parse(text).Lines.Count > 0) timed.Add(candidate with { SyncedLyrics = text });
            }
            ct.ThrowIfCancellationRequested();
            var match = LyricsMatching.Choose(track, timed);
            return match.Ambiguous ? new(LyricsOutcome.Ambiguous) : match.Record is null
                ? new(LyricsOutcome.NotFound) : new(LyricsOutcome.Found, match.Record);
        }
        catch (CooldownException ex) { return new(LyricsOutcome.RateLimited, RetryAt: ex.Until); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return new(LyricsOutcome.Timeout, RetryAt: Retry()); }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or InvalidOperationException or ArgumentException)
        { return new(LyricsOutcome.Unavailable, RetryAt: Retry()); }
    }

    private static DateTimeOffset Retry() => DateTimeOffset.UtcNow.AddSeconds(60);
    private static bool Succeeded(JsonElement root) => root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty("code", out var code) && code.TryGetInt32(out var value) && value == 200;

    private static LyricsRecord? ReadSong(JsonElement song)
    {
        if (song.ValueKind != JsonValueKind.Object || !song.TryGetProperty("id", out var id) || !id.TryGetInt64(out var number) || number <= 0 ||
            !song.TryGetProperty("name", out var title) || title.ValueKind != JsonValueKind.String ||
            !song.TryGetProperty("dt", out var duration) || !duration.TryGetDouble(out var milliseconds) ||
            !song.TryGetProperty("ar", out var artists) || artists.ValueKind != JsonValueKind.Array || artists.GetArrayLength() is 0 or > 30 ||
            !song.TryGetProperty("al", out var album) || album.ValueKind != JsonValueKind.Object ||
            !album.TryGetProperty("name", out var albumName) || albumName.ValueKind != JsonValueKind.String) return null;
        var names = new List<string>();
        foreach (var artist in artists.EnumerateArray())
        {
            if (artist.ValueKind != JsonValueKind.Object || !artist.TryGetProperty("name", out var name) ||
                name.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(name.GetString())) return null;
            names.Add(name.GetString()!);
        }
        return new(number, title.GetString()!, string.Join(" & ", names), albumName.GetString()!, milliseconds / 1000, false, null);
    }

    private async Task<JsonDocument> RequestAsync(HttpRequestMessage request, CancellationToken token)
    {
        await network.WaitAsync(token);
        try
        {
            if (DateTimeOffset.UtcNow < cooldown) throw new CooldownException(cooldown);
            var delay = nextRequest - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero) await Task.Delay(delay, token);
            request.Headers.Referrer = new Uri("https://music.163.com/");
            request.Headers.UserAgent.ParseAdd("LyricsChatbox/0.3 (+https://github.com/Teyocesu/LyricsChatbox)");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                response.StatusCode == HttpStatusCode.ServiceUnavailable && response.Headers.RetryAfter is not null)
            {
                cooldown = response.Headers.RetryAfter?.Date ?? DateTimeOffset.UtcNow.Add(response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(60));
                if (cooldown < DateTimeOffset.UtcNow.AddMilliseconds(350)) cooldown = DateTimeOffset.UtcNow.AddMilliseconds(350);
                throw new CooldownException(cooldown);
            }
            response.EnsureSuccessStatusCode();
            const int maximum = 2_000_000;
            if (response.Content.Headers.ContentLength > maximum) throw new IOException("Response too large");
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int count;
            while ((count = await stream.ReadAsync(chunk, token)) != 0)
            {
                if (buffer.Length + count > maximum) throw new IOException("Response too large");
                buffer.Write(chunk, 0, count);
            }
            token.ThrowIfCancellationRequested();
            return JsonDocument.Parse(buffer.ToArray());
        }
        finally { nextRequest = DateTimeOffset.UtcNow.AddMilliseconds(350); network.Release(); }
    }

    public void Dispose() => network.Dispose();
    private sealed class CooldownException(DateTimeOffset until) : Exception { public DateTimeOffset Until { get; } = until; }
}
