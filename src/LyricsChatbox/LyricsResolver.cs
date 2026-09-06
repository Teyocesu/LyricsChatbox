using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace LyricsChatbox;

public record LyricsResolution(LyricTimeline? Timeline, string Status, DateTimeOffset? RetryAt = null);

public sealed class LyricsResolver(HttpClient http, LocalData data) : IDisposable
{
    private readonly SemaphoreSlim network = new(1, 1);
    private DateTimeOffset nextRequest;
    private DateTimeOffset cooldown;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public async Task<LyricsResolution> ResolveAsync(TrackIdentity track, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var local = data.ReadLocal(track);
        if (local is not null)
        {
            var parsed = LrcParser.Parse(local);
            if (parsed.Lines.Count > 0) return new(parsed, "Synced lyrics loaded · local LRC");
        }
        var cached = data.ReadCache(track);
        if (cached is not null)
        {
            var result = Convert(cached, "cache");
            if (result.Timeline is not null || cached.Instrumental) return result;
        }
        if (string.IsNullOrWhiteSpace(track.Title) || string.IsNullOrWhiteSpace(track.Artist) || !double.IsFinite(track.Duration) || track.Duration is < 1 or > 3600)
            return new(null, "Insufficient track metadata for safe matching");
        try
        {
            var query = "track_name=" + Uri.EscapeDataString(track.Title) + "&artist_name=" + Uri.EscapeDataString(track.Artist);
            var direct = await GetAsync<LyricsRecord>("get?" + query + "&album_name=" + Uri.EscapeDataString(track.Album) +
                "&duration=" + track.Duration.ToString(System.Globalization.CultureInfo.InvariantCulture), token);
            LyricsRecord? record = direct is not null && LyricsMatching.Score(track, direct).HasValue ? direct : null;
            if (record is null || (!record.Instrumental && string.IsNullOrWhiteSpace(record.SyncedLyrics)))
            {
                var candidates = await GetAsync<LyricsRecord[]>("search?" + query, token) ?? [];
                var match = LyricsMatching.Choose(track, candidates);
                if (match.Ambiguous) return new(null, "Ambiguous lyrics match");
                record = match.Record;
            }
            token.ThrowIfCancellationRequested();
            if (record is null) return new(null, "No synchronized lyrics");
            var resolved = Convert(record, "LRCLIB");
            if (resolved.Timeline is not null || record.Instrumental) data.SaveCache(track, record);
            return resolved;
        }
        catch (ProviderCooldown ex) { return new(null, "Lyrics provider rate limited", ex.Until); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or OperationCanceledException or ArgumentException)
        { return new(null, "Lyrics provider unavailable", DateTimeOffset.UtcNow.AddSeconds(60)); }
    }

    private static LyricsResolution Convert(LyricsRecord record, string source)
    {
        if (record.Instrumental) return new(null, "Instrumental");
        var timeline = LrcParser.Parse(record.SyncedLyrics);
        return timeline.Lines.Count == 0 ? new(null, "No synchronized lyrics") : new(timeline, "Synced lyrics loaded · " + source);
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken token)
    {
        await network.WaitAsync(token);
        try
        {
            if (DateTimeOffset.UtcNow < cooldown) throw new ProviderCooldown(cooldown);
            var delay = nextRequest - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero) await Task.Delay(delay, token);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(12));
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://lrclib.net/api/" + path);
            request.Headers.UserAgent.ParseAdd("LyricsChatbox/0.1.0 (+https://github.com/Teyocesu/LyricsChatbox)");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode == HttpStatusCode.TooManyRequests || response.StatusCode == HttpStatusCode.ServiceUnavailable && response.Headers.RetryAfter is not null)
            {
                cooldown = response.Headers.RetryAfter?.Date ?? DateTimeOffset.UtcNow.Add(response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(60));
                if (cooldown < DateTimeOffset.UtcNow.AddMilliseconds(350)) cooldown = DateTimeOffset.UtcNow.AddMilliseconds(350);
                throw new ProviderCooldown(cooldown);
            }
            if (response.StatusCode == HttpStatusCode.NotFound) return default;
            response.EnsureSuccessStatusCode();
            const int maximum = 4_000_000;
            if (response.Content.Headers.ContentLength > maximum) throw new IOException("Provider response too large");
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int count;
            while ((count = await stream.ReadAsync(chunk, timeout.Token)) != 0)
            {
                if (buffer.Length + count > maximum) throw new IOException("Provider response too large");
                buffer.Write(chunk, 0, count);
            }
            return JsonSerializer.Deserialize<T>(buffer.GetBuffer().AsSpan(0, (int)buffer.Length), Json);
        }
        finally { nextRequest = DateTimeOffset.UtcNow.AddMilliseconds(350); network.Release(); }
    }
    public void Dispose() => network.Dispose();
    private sealed class ProviderCooldown(DateTimeOffset until) : Exception { public DateTimeOffset Until { get; } = until; }
}
