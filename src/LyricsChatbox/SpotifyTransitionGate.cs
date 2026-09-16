namespace LyricsChatbox;

public enum SpotifyGateResult { Accept, Invalidate, Settling }

// GSMTC may publish a new Position/End before the new title. Nothing from a mixed
// observation is accepted; recovery needs subsequent coherent authoritative samples.
public sealed class SpotifyTransitionGate
{
    private PlaybackSnapshot? accepted;
    private Pending? pending;
    private sealed record Pending(PlaybackSnapshot Before, double StartedMono, bool RequireNewMetadata,
        bool RestartLike, PlaybackSnapshot? Candidate = null, int StableSamples = 0);

    public void Reset() { accepted = null; pending = null; }

    public void InvalidateForMediaChange(double mono)
    {
        if (pending is null && accepted is { } before)
        {
            pending = new(before, mono, false, false);
            accepted = null;
        }
    }

    public SpotifyGateResult Observe(PlaybackSnapshot next)
    {
        if (!Coherent(next))
        {
            if (pending is null && accepted is { } before)
            {
                pending = new(before, next.ObservedMono, false, false);
                accepted = null;
                return SpotifyGateResult.Invalidate;
            }
            return SpotifyGateResult.Settling;
        }
        if (pending is { } settle) return Settle(next, settle);
        if (accepted is { } previous)
        {
            if (previous.SessionId != next.SessionId)
            {
                pending = new(previous, next.ObservedMono, false, false);
                accepted = null;
                return SpotifyGateResult.Invalidate;
            }
            var metadataChanged = next.RawTitle != previous.RawTitle || next.RawArtist != previous.RawArtist ||
                next.RawAlbum != previous.RawAlbum || next.TrackNumber != previous.TrackNumber;
            var durationChanged = Math.Abs(next.End - previous.End) > 0.75 || Math.Abs(next.Start - previous.Start) > 0.75;
            var reset = previous.Position - next.Position > 0.75;
            var jump = false;
            if (previous.State == PlaybackState.Playing && next.State == PlaybackState.Playing)
            {
                var elapsed = (next.LastUpdated - previous.LastUpdated).TotalSeconds;
                if (elapsed >= 0 && elapsed < 30 &&
                    Math.Abs(next.Position - (previous.Position + elapsed * previous.Rate)) > 1.5)
                    jump = true;
            }
            if (metadataChanged || durationChanged || reset || jump)
            {
                // A changed duration with old metadata is the observed transition race.
                pending = new(previous, next.ObservedMono, durationChanged && !metadataChanged,
                    reset && !durationChanged && !metadataChanged);
                accepted = null;
                return SpotifyGateResult.Invalidate;
            }
        }
        accepted = next;
        return SpotifyGateResult.Accept;
    }

    private SpotifyGateResult Settle(PlaybackSnapshot next, Pending settle)
    {
        if (next.SessionId != settle.Before.SessionId || next.ObservedMono <= settle.StartedMono ||
            next.LastUpdated <= settle.Before.LastUpdated) return SpotifyGateResult.Settling;
        var newMetadata = next.RawTitle != settle.Before.RawTitle || next.RawArtist != settle.Before.RawArtist ||
            next.RawAlbum != settle.Before.RawAlbum || next.TrackNumber != settle.Before.TrackNumber;
        if (settle.RequireNewMetadata && !newMetadata) return SpotifyGateResult.Settling;
        var sameCandidate = settle.Candidate is { } candidate &&
            candidate.RawTitle == next.RawTitle && candidate.RawArtist == next.RawArtist &&
            candidate.RawAlbum == next.RawAlbum && candidate.TrackNumber == next.TrackNumber &&
            Math.Abs(candidate.End - next.End) <= 0.75 && Math.Abs(candidate.Start - next.Start) <= 0.75;
        settle = settle with { Candidate = next, StableSamples = sameCandidate ? settle.StableSamples + 1 : 1 };
        pending = settle;
        // A restart-like reset gets a bounded observation window in which delayed metadata
        // can arrive. A true title change still needs a second consistent sample.
        var minimum = settle.RestartLike ? 0.5 : 0.25;
        if (settle.StableSamples < 2 || next.ObservedMono - settle.StartedMono < minimum)
            return SpotifyGateResult.Settling;
        accepted = next;
        pending = null;
        return SpotifyGateResult.Accept;
    }

    private static bool Coherent(PlaybackSnapshot next)
    {
        if (string.IsNullOrWhiteSpace(next.RawTitle) || string.IsNullOrWhiteSpace(next.RawArtist) ||
            !double.IsFinite(next.Start) || !double.IsFinite(next.End) || !double.IsFinite(next.Position) ||
            !double.IsFinite(next.ObservedMono) || next.End <= next.Start || next.Position < next.Start ||
            next.Position > next.End || next.State == PlaybackState.Stopped) return false;
        var age = (next.ObservedUtc - next.LastUpdated).TotalSeconds;
        return (next.State == PlaybackState.Paused || age is >= -0.5 and <= 5.25) &&
            double.IsFinite(next.Rate) && next.Rate is > 0 and <= 4;
    }
}
