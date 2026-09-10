using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace LyricsChatbox;

public record LyricsResolution(LyricTimeline? Timeline, string Status, DateTimeOffset? RetryAt = null,
    string? Provider = null, LyricsOutcome Outcome = LyricsOutcome.Unavailable, double? CandidateDuration = null,
    bool FromCache = false, bool ManualMatch = false);

public sealed partial class LyricsResolver(HttpClient http, LocalData data, ISyncedLyricsProvider? secondary = null) : IDisposable
{
    private readonly SemaphoreSlim network = new(1, 1);
    private DateTimeOffset nextRequest;
    private DateTimeOffset cooldown;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static readonly TimeSpan PrimaryDeadline = TimeSpan.FromSeconds(5);

    public async Task<LyricsResolution> ResolveAsync(TrackIdentity track, CancellationToken token,
        Action<string>? progress = null)
    {
        token.ThrowIfCancellationRequested();
        if (data.IsIgnored(track)) return new(null, "Lyrics ignored for this recording", Outcome: LyricsOutcome.Ignored);
        if (data.ReadManualAssociation(track) is { } association && LrcParser.Parse(data.ReadLocal(track)).Lines.Count==0 && data.ReadCachedLyrics(track) is null)
        {
            var choice = new ManualCandidate(association.Provider,association.Metadata);
            var selected = await FetchManualAsync(choice,token);
            token.ThrowIfCancellationRequested();
            if(selected.Outcome==LyricsOutcome.Found && selected.Record is { } manualRecord)
            {
                data.SaveManualAssociation(track,choice,manualRecord);
                return Convert(manualRecord,choice.Provider) with {Status="Synced lyrics loaded · manual match · "+choice.Provider, ManualMatch=true};
            }
        }
        LyricsResolution primary;
        using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            deadline.CancelAfter(PrimaryDeadline);
            try { primary = await ResolvePrimaryAsync(track, deadline.Token); }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            { primary = new(null, "Lyrics provider timed out", DateTimeOffset.UtcNow.AddSeconds(60), "LRCLIB", LyricsOutcome.Timeout); }
        }
        token.ThrowIfCancellationRequested();
        if (secondary is null || primary.Timeline is not null || primary.Outcome is LyricsOutcome.Instrumental or LyricsOutcome.Rejected)
            return primary;
        progress?.Invoke("Searching another source…");
        var fallback = await secondary.FindAsync(track, token);
        token.ThrowIfCancellationRequested();
        // Revalidate the boundary before parsing or caching, even if a future adapter is defective.
        if (fallback.Outcome == LyricsOutcome.Found && fallback.Record is { } record && LyricsMatching.Score(track, record).HasValue)
        {
            var resolved = Convert(record, secondary.Name);
            if (resolved.Timeline is not null)
            {
                token.ThrowIfCancellationRequested();
                data.SaveCache(track, record, secondary.Name);
                return resolved;
            }
        }
        if (fallback.Outcome == LyricsOutcome.Found) fallback = new(LyricsOutcome.Rejected);
        var retry = new[] { primary.RetryAt, fallback.RetryAt }.Where(t => t.HasValue).Min();
        var status = fallback.Outcome switch
        {
            LyricsOutcome.Ambiguous => "No confident lyrics match",
            LyricsOutcome.RateLimited => "Another source is busy · retrying later",
            LyricsOutcome.Timeout => "Another source timed out · retrying later",
            LyricsOutcome.Unavailable => "Another source is unavailable · retrying later",
            _ => "No synchronized lyrics · import a local LRC"
        };
        return new(null, status, retry, secondary.Name, fallback.Outcome);
    }

    private async Task<LyricsResolution> ResolvePrimaryAsync(TrackIdentity track, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var local = data.ReadLocal(track);
        if (local is not null)
        {
            var parsed = LrcParser.Parse(local);
            if (parsed.Lines.Count > 0) return new(parsed, "Synced lyrics loaded · local LRC", Provider: "Local LRC", Outcome: LyricsOutcome.Found);
        }
        var cachedEntry = data.ReadCachedLyrics(track);
        var cached = cachedEntry?.Record;
        if (cached is not null)
        {
            var result = Convert(cached, cachedEntry!.Provider) with { Status = "Synced lyrics loaded · cache · " + cachedEntry.Provider, FromCache=true, ManualMatch=cachedEntry.Manual };
            if (result.Timeline is not null || cached.Instrumental) return cached.Instrumental ? result with { Status = "Instrumental" } : result;
        }
        if (string.IsNullOrWhiteSpace(track.Title) || string.IsNullOrWhiteSpace(track.Artist) || !double.IsFinite(track.Duration) || track.Duration is < 1 or > 3600)
            return new(null, "Insufficient track metadata for safe matching", Outcome: LyricsOutcome.Rejected);
        try
        {
            var query = "track_name=" + Uri.EscapeDataString(track.Title) + "&artist_name=" + Uri.EscapeDataString(track.Artist);
            var direct = await GetAsync<LyricsRecord>("get?" + query + "&album_name=" + Uri.EscapeDataString(track.Album) +
                "&duration=" + track.Duration.ToString(System.Globalization.CultureInfo.InvariantCulture), token);
            LyricsRecord? record = direct is not null && LyricsMatching.Score(track, direct).HasValue ? direct : null;
            if (!Usable(record))
            {
                var candidates = await GetAsync<LyricsRecord[]>("search?" + query, token) ?? [];
                var match = LyricsMatching.Choose(track, candidates.Where(Usable));
                // LRCLIB caps search at 20. An exact album filter can expose a recording hidden by that cap.
                // Preserve broad candidates when checking conflicts; narrowing must not erase ambiguity.
                if ((match.Record is null || match.Ambiguous) && candidates.Length >= 20 && !string.IsNullOrWhiteSpace(track.Album))
                {
                    var narrowed = await GetAsync<LyricsRecord[]>("search?" + query + "&album_name=" + Uri.EscapeDataString(track.Album), token) ?? [];
                    match = LyricsMatching.Choose(track, candidates.Concat(narrowed).Where(Usable));
                }
                if (match.Ambiguous) return new(null, "Ambiguous lyrics match", Provider: "LRCLIB", Outcome: LyricsOutcome.Ambiguous);
                record = match.Record;
            }
            token.ThrowIfCancellationRequested();
            if (record is null) return new(null, "No synchronized lyrics", Provider: "LRCLIB", Outcome: LyricsOutcome.NotFound);
            var resolved = Convert(record, "LRCLIB");
            if (resolved.Timeline is not null || record.Instrumental) data.SaveCache(track, record);
            return resolved;
        }
        catch (ProviderCooldown ex) { return new(null, "Lyrics provider rate limited", ex.Until, "LRCLIB", LyricsOutcome.RateLimited); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or OperationCanceledException or ArgumentException)
        { return new(null, "Lyrics provider unavailable", DateTimeOffset.UtcNow.AddSeconds(60), "LRCLIB", LyricsOutcome.Unavailable); }
    }

    private static bool Usable(LyricsRecord? record) => record is not null &&
        (record.Instrumental || LrcParser.Parse(record.SyncedLyrics).Lines.Count > 0);

    private static LyricsResolution Convert(LyricsRecord record, string source)
    {
        if (record.Instrumental) return new(null, "Instrumental", Provider: source, Outcome: LyricsOutcome.Instrumental, CandidateDuration:record.Duration);
        var timeline = LrcParser.Parse(record.SyncedLyrics);
        return timeline.Lines.Count == 0 ? new(null, "No synchronized lyrics", Provider: source, Outcome: LyricsOutcome.NotFound) : new(timeline, "Synced lyrics loaded · " + source, Provider: source, Outcome: LyricsOutcome.Found, CandidateDuration:record.Duration);
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
            timeout.CancelAfter(PrimaryDeadline);
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://lrclib.net/api/" + path);
            request.Headers.UserAgent.ParseAdd($"LyricsChatbox/{typeof(LyricsResolver).Assembly.GetName().Version?.ToString(3)} (+https://github.com/Teyocesu/LyricsChatbox)");
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
