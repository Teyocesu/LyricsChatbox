using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LyricsChatbox;

public record TrackIdentity(string Title, string Artist, string Album, double Duration)
{
    // Length-safe serialization avoids separator collisions; raw metadata remains in the snapshot.
    public string Key => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new[] { Title, Artist, Album, Math.Round(Duration).ToString(System.Globalization.CultureInfo.InvariantCulture) }))));

    public static TrackIdentity FromApple(string title, string artist, string album, double duration)
    {
        // Observed Apple Music 1.1540 GSMTC format, only when AlbumTitle is absent and split is unambiguous.
        var parts = artist.Split(" — ", StringSplitOptions.None);
        if (string.IsNullOrWhiteSpace(album) && parts.Length == 2 && parts.All(p => !string.IsNullOrWhiteSpace(p)))
            return new(title, parts[0], parts[1], duration);
        return new(title, artist, album, duration);
    }
}

public enum PlaybackState { Stopped, Paused, Playing }
public record PlaybackSnapshot(string SessionId, TrackIdentity Track, string RawTitle, string RawArtist,
    string RawAlbum, int TrackNumber, double Start, double End, double Position,
    DateTimeOffset LastUpdated, PlaybackState State, double Rate, DateTimeOffset ObservedUtc, double ObservedMono);

public static class MonotonicClock
{
    public static double Now => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
}

public sealed class PlaybackClock
{
    // Spike: ~280ms events, integer Position; 250ms polling, 2s freshness budget, 1.25s quantization tolerance.
    public const double FreshnessSeconds = 2;
    public const double DiscontinuitySeconds = 1.25;
    private PlaybackSnapshot? snapshot;
    private double anchor;
    private double anchorMono;
    private double ceiling;
    private bool valid;
    public bool Discontinuity { get; private set; }

    public void Reset() { snapshot = null; valid = false; }

    public void Observe(PlaybackSnapshot next)
    {
        var age = (next.ObservedUtc - next.LastUpdated).TotalSeconds;
        var rateValid = double.IsFinite(next.Rate) && next.Rate > 0 && next.Rate <= 4;
        var candidate = next.Position - next.Start;
        var nextValid = double.IsFinite(candidate) && double.IsFinite(next.Start) && double.IsFinite(next.End) &&
            double.IsFinite(next.ObservedMono) && next.End > next.Start && next.Position >= next.Start &&
            next.Position <= next.End && next.State != PlaybackState.Stopped;
        if (next.State == PlaybackState.Playing)
        {
            nextValid &= rateValid && age >= -0.5 && age <= FreshnessSeconds;
            candidate += Math.Clamp(age, 0, FreshnessSeconds) * (rateValid ? next.Rate : 1);
        }
        var previous = Position(next.ObservedMono);
        var same = snapshot?.SessionId == next.SessionId && snapshot.Track == next.Track;
        Discontinuity = same && previous.HasValue &&
            (next.Position < snapshot!.Position || Math.Abs(candidate - previous.Value) > DiscontinuitySeconds);
        var samePlayingPosition = same && valid && nextValid && snapshot!.State == PlaybackState.Playing &&
            next.State == PlaybackState.Playing && snapshot.Position == next.Position && snapshot.Rate == next.Rate;
        var integerPosition = Math.Abs(next.Position - Math.Round(next.Position)) < 0.000001;
        // Apple republishes integer Position about every 280ms. Those heartbeat timestamps do not create
        // a new fractional position. Keep interpolation within this confirmed second, never across its end.
        // Reanchor from scratch when Position advances, so no extrapolated lead carries into the next second.
        if (!(samePlayingPosition && integerPosition))
        {
            anchor = nextValid ? Math.Clamp(candidate, 0, next.End - next.Start) : 0;
            anchorMono = next.ObservedMono;
            ceiling = !nextValid ? 0 : next.State == PlaybackState.Playing && integerPosition
                ? Math.Min(next.End - next.Start, next.Position - next.Start + 0.999999)
                : next.End - next.Start;
            anchor = Math.Min(anchor, ceiling);
        }
        snapshot = next;
        valid = nextValid;
    }

    public double? Position(double now)
    {
        if (!valid || snapshot is null || !double.IsFinite(now)) return null;
        var elapsed = now - snapshot.ObservedMono;
        if (elapsed < 0 || elapsed > FreshnessSeconds) return null;
        if (snapshot.State == PlaybackState.Paused) return anchor;
        var age = Math.Max(0, (snapshot.ObservedUtc - snapshot.LastUpdated).TotalSeconds);
        if (age + elapsed > FreshnessSeconds) return null;
        return Math.Clamp(anchor + (now - anchorMono) * snapshot.Rate, 0, ceiling);
    }
}
