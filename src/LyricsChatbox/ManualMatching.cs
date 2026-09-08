using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace LyricsChatbox;

public record ManualCandidate(string Provider, LyricsRecord Metadata)
{
    public string Display => $"{Metadata.TrackName} — {Metadata.ArtistName}\n{Metadata.AlbumName} · {TimeSpan.FromSeconds(Metadata.Duration):m\\:ss} · {Provider}";
}
public record ManualAssociation(int Version, string TrackKey, string Provider, LyricsRecord Metadata);
public static class ManualMatching
{
    public static bool Valid(LyricsRecord? r) => r is
    {
        Id: > 0, TrackName.Length: > 0 and <= 512, ArtistName.Length: > 0 and <= 512,
        AlbumName.Length: <= 512
    } && double.IsFinite(r.Duration) && r.Duration is >= 1 and <= 3600;
    public static bool Same(LyricsRecord a, LyricsRecord b) => Valid(a) && Valid(b) && a.Id == b.Id &&
        a.TrackName == b.TrackName && a.ArtistName == b.ArtistName && a.AlbumName == b.AlbumName && Math.Abs(a.Duration - b.Duration) < 0.5;
    // A suggestion is never an automatic match. Users can compare recording/version metadata explicitly.
    public static IEnumerable<LyricsRecord> Suggestions(TrackIdentity track, IEnumerable<LyricsRecord> records) => records
        .Where(Valid).Where(r => Math.Abs(track.Duration - r.Duration) <= Math.Max(30, track.Duration * 0.15))
        .OrderByDescending(r => LyricsMatching.Score(track, r) ?? 0).ThenBy(r => Math.Abs(r.Duration - track.Duration));
}

public sealed partial class NetEaseLyricsProvider
{
    public async Task<IReadOnlyList<LyricsRecord>> SearchManualAsync(string query, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 512) return [];
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(Deadline);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://music.163.com/api/cloudsearch/pc")
            { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["s"] = query, ["type"] = "1", ["limit"] = "20", ["offset"] = "0" }) };
            using var json = await RequestAsync(request, deadline.Token);
            if (!Succeeded(json.RootElement)) return [];
            if (!json.RootElement.TryGetProperty("result", out var result) || !result.TryGetProperty("songs", out var songs) ||
                songs.ValueKind != JsonValueKind.Array || songs.GetArrayLength() > 20) return [];
            return songs.EnumerateArray().Select(ReadSong).Where(ManualMatching.Valid).Cast<LyricsRecord>().ToArray();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is IOException or HttpRequestException or JsonException or InvalidOperationException or ArgumentException or OperationCanceledException or CooldownException) { return []; }
    }
    public async Task<ProviderResult> FetchManualAsync(LyricsRecord expected, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(Deadline);
        try
        {
            if (!ManualMatching.Valid(expected)) return new(LyricsOutcome.Rejected);
            var refreshed = await SearchManualAsync(expected.TrackName + " " + expected.ArtistName, deadline.Token);
            var current = refreshed.FirstOrDefault(r => ManualMatching.Same(expected, r));
            if (current is null) return new(LyricsOutcome.NotFound);
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://music.163.com/api/song/lyric?id={current.Id}&lv=-1&kv=-1&tv=-1");
            using var json = await RequestAsync(request, deadline.Token);
            if (!Succeeded(json.RootElement)) return new(LyricsOutcome.Unavailable);
            var text = json.RootElement.GetProperty("lrc").GetProperty("lyric").GetString();
            return LrcParser.Parse(text).Lines.Count > 0 ? new(LyricsOutcome.Found, current with { SyncedLyrics = text }) : new(LyricsOutcome.NotFound);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is IOException or HttpRequestException or JsonException or InvalidOperationException or ArgumentException or KeyNotFoundException or OperationCanceledException or CooldownException)
        { return new(LyricsOutcome.Unavailable); }
    }
}
