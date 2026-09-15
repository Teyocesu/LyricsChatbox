using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LyricsChatbox;

// Explicit product scope; no arbitrary player discovery.
public enum PlaybackSourceKind { AppleMusic, Spotify }

public record TrackIdentity(string Title, string Artist, string Album, double Duration)
{
    // Length-safe serialization avoids separator collisions; raw metadata remains in the snapshot.
    public string Key => Hash("2", Title, Artist, Album,
        Duration.ToString("R", System.Globalization.CultureInfo.InvariantCulture));

    // v0.5.0 rounded duration to a whole second. LocalData uses this only to claim and migrate
    // an existing recording file when no current-key file exists.
    public string LegacyKey => Hash(Title, Artist, Album,
        Math.Round(Duration).ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static string Hash(params string[] parts) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(parts))));

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
    DateTimeOffset LastUpdated, PlaybackState State, double Rate, DateTimeOffset ObservedUtc, double ObservedMono,
    PlaybackSourceKind Source = PlaybackSourceKind.AppleMusic);

public sealed record PlaybackTimingPolicy(double FreshnessSeconds, double DiscontinuitySeconds,
    bool IntegerPositionCeiling, bool RequireFreshAnchorAfterResume)
{
    public static PlaybackTimingPolicy AppleMusic { get; } = new(2, 1.25, true, false);
    // 4.617s measured p95 anchor cadence, 4.512s maximum healthy timestamp age,
    // 225ms polling: 5.25s retains bounded margin without changing Apple's policy.
    public static PlaybackTimingPolicy Spotify { get; } = new(5.25, 1.25, false, true);
    public static PlaybackTimingPolicy For(PlaybackSourceKind source) => source switch
    {
        PlaybackSourceKind.AppleMusic => AppleMusic,
        PlaybackSourceKind.Spotify => Spotify,
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };
}

public static class MonotonicClock
{
    public static double Now => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
}

public sealed class PlaybackClock
{
    // Public constants retain the v0.5.4 Apple contract.
    public const double FreshnessSeconds = 2;
    public const double DiscontinuitySeconds = 1.25;
    private readonly PlaybackTimingPolicy policy;
    private PlaybackSnapshot? snapshot;
    private double anchor;
    private double anchorMono;
    private double ceiling;
    private bool valid;
    private DateTimeOffset? pausedAnchorForResume;
    public bool Discontinuity { get; private set; }
    public PlaybackClock(PlaybackTimingPolicy? policy = null)
    {
        this.policy = policy ?? PlaybackTimingPolicy.AppleMusic;
        if (!double.IsFinite(this.policy.FreshnessSeconds) || this.policy.FreshnessSeconds <= 0 ||
            !double.IsFinite(this.policy.DiscontinuitySeconds) || this.policy.DiscontinuitySeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(policy));
    }

    public void Reset() { snapshot = null; valid = false; pausedAnchorForResume = null; }

    public void Observe(PlaybackSnapshot next)
    {
        var age = (next.ObservedUtc - next.LastUpdated).TotalSeconds;
        var rateValid = double.IsFinite(next.Rate) && next.Rate > 0 && next.Rate <= 4;
        var candidate = next.Position - next.Start;
        var nextValid = double.IsFinite(candidate) && double.IsFinite(next.Start) && double.IsFinite(next.End) &&
            double.IsFinite(next.ObservedMono) && next.End > next.Start && next.Position >= next.Start &&
            next.Position <= next.End && next.State != PlaybackState.Stopped;
        if (policy.RequireFreshAnchorAfterResume && next.State == PlaybackState.Paused)
            pausedAnchorForResume = next.LastUpdated;
        if (next.State == PlaybackState.Playing)
        {
            nextValid &= rateValid && age >= -0.5 && age <= policy.FreshnessSeconds;
            if (policy.RequireFreshAnchorAfterResume && pausedAnchorForResume is { } pausedAnchor)
            {
                nextValid &= next.LastUpdated > pausedAnchor;
                if (nextValid) pausedAnchorForResume = null;
            }
            candidate += Math.Clamp(age, 0, policy.FreshnessSeconds) * (rateValid ? next.Rate : 1);
        }
        var previous = Position(next.ObservedMono);
        var same = snapshot?.SessionId == next.SessionId && snapshot.Track == next.Track;
        Discontinuity = same && previous.HasValue &&
            (next.Position < snapshot!.Position || Math.Abs(candidate - previous.Value) > policy.DiscontinuitySeconds);
        var samePlayingPosition = same && valid && nextValid && snapshot!.State == PlaybackState.Playing &&
            next.State == PlaybackState.Playing && snapshot.Position == next.Position && snapshot.Rate == next.Rate;
        var integerPosition = Math.Abs(next.Position - Math.Round(next.Position)) < 0.000001;
        // Apple republishes integer Position about every 280ms. Those heartbeat timestamps do not create
        // a new fractional position. Keep interpolation within this confirmed second, never across its end.
        // Reanchor from scratch when Position advances, so no extrapolated lead carries into the next second.
        if (!(samePlayingPosition && integerPosition && policy.IntegerPositionCeiling))
        {
            anchor = nextValid ? Math.Clamp(candidate, 0, next.End - next.Start) : 0;
            anchorMono = next.ObservedMono;
            ceiling = !nextValid ? 0 : next.State == PlaybackState.Playing && integerPosition && policy.IntegerPositionCeiling
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
        if (elapsed < 0 || elapsed > policy.FreshnessSeconds) return null;
        if (snapshot.State == PlaybackState.Paused) return anchor;
        var age = Math.Max(0, (snapshot.ObservedUtc - snapshot.LastUpdated).TotalSeconds);
        if (age + elapsed > policy.FreshnessSeconds) return null;
        return Math.Clamp(anchor + (now - anchorMono) * snapshot.Rate, 0, ceiling);
    }
}
