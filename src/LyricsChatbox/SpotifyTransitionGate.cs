namespace LyricsChatbox;

public enum SpotifyGateResult { Accept, Invalidate, Settling }

// Spotify can publish metadata and timeline changes in either order. The gate keeps
// automatic playback invalid until one exact recording candidate is repeated by
// distinct, fresh authoritative anchors.
public sealed class SpotifyTransitionGate
{
    private PlaybackSnapshot? accepted;
    private PlaybackSnapshot? lastAccepted;
    private Pending? pending;
    private string? expectedSessionId;

    private sealed record CandidateProof(PlaybackSnapshot Snapshot, DateTimeOffset LastAnchor, int AnchorSamples,
        double LastObservedMono, int ObservationSamples);
    private sealed record Pending(PlaybackSnapshot? Before, double StartedMono, string? ExpectedSessionId,
        bool RequireMetadataChange, CandidateProof? Candidate = null);

    public SpotifyTransitionGate() => BeginSession(null);

    // Preserve the last accepted recording across loss/reconnect so a transient
    // rounded duration cannot briefly become a different authoritative identity.
    public void BeginSession(string? sessionId)
    {
        if (accepted is { } current) lastAccepted = current;
        accepted = null;
        expectedSessionId = sessionId;
        pending = new(lastAccepted, double.NegativeInfinity, sessionId, false);
    }

    public void Reset() => BeginSession(null);

    public void InvalidateForMediaChange(double mono)
    {
        var acceptedInCurrentSession = accepted is not null;
        if (accepted is { } current) lastAccepted = current;
        var before = accepted ?? pending?.Before ?? lastAccepted;
        accepted = null;
        pending = new(before, mono, expectedSessionId,
            pending?.RequireMetadataChange == true || acceptedInCurrentSession);
    }

    public SpotifyGateResult Observe(PlaybackSnapshot next)
    {
        if (expectedSessionId is not null && next.SessionId != expectedSessionId)
            return SpotifyGateResult.Settling;
        if (expectedSessionId is null)
        {
            expectedSessionId = next.SessionId;
            pending = (pending ?? new(null, double.NegativeInfinity, next.SessionId, false)) with
            {
                ExpectedSessionId = next.SessionId
            };
        }

        if (!Coherent(next))
        {
            if (accepted is { } current)
            {
                lastAccepted = current;
                accepted = null;
                pending = new(current, next.ObservedMono, expectedSessionId, false);
                return SpotifyGateResult.Invalidate;
            }
            if (pending is { } settling) pending = settling with { Candidate = null };
            return SpotifyGateResult.Settling;
        }

        if (pending is { } settle) return Settle(next, settle);
        if (accepted is not { } previous)
        {
            pending = new(lastAccepted, next.ObservedMono, expectedSessionId, false);
            return SpotifyGateResult.Settling;
        }

        if (previous.SessionId != next.SessionId || RecordingMetadataChanged(previous, next) ||
            previous.Track.Key != next.Track.Key || TimelineChanged(previous, next))
        {
            lastAccepted = previous;
            accepted = null;
            pending = new(previous, next.ObservedMono, expectedSessionId, false);
            return SpotifyGateResult.Invalidate;
        }

        accepted = next;
        lastAccepted = next;
        return SpotifyGateResult.Accept;
    }

    private SpotifyGateResult Settle(PlaybackSnapshot next, Pending settle)
    {
        if (settle.ExpectedSessionId is not null && next.SessionId != settle.ExpectedSessionId)
            return SpotifyGateResult.Settling;
        if (next.ObservedMono <= settle.StartedMono)
            return SpotifyGateResult.Settling;

        if (settle.Before is { } before)
        {
            if (next.LastUpdated <= before.LastUpdated)
                return SpotifyGateResult.Settling;

            var metadataChanged = RecordingMetadataChanged(before, next);
            var identityChanged = before.Track.Key != next.Track.Key;
            var timelineChanged = TimelineChanged(before, next);
            var identityMetadataChanged = before.Track.Title != next.Track.Title ||
                before.Track.Artist != next.Track.Artist || before.Track.Album != next.Track.Album;
            var identityDurationChanged = before.Track.Duration != next.Track.Duration;

            if (settle.RequireMetadataChange && !metadataChanged)
                return SpotifyGateResult.Settling;
            // Symmetric fail-closed ordering: new identity metadata needs evidence of
            // its timeline, while a duration-only identity needs new metadata.
            if (identityChanged && identityMetadataChanged && !timelineChanged)
                return SpotifyGateResult.Settling;
            if (identityChanged && identityDurationChanged && !identityMetadataChanged)
                return SpotifyGateResult.Settling;
        }

        var proof = settle.Candidate;
        if (proof is null || !SameCandidate(proof.Snapshot, next))
            proof = new(next, next.LastUpdated, 1, next.ObservedMono, 1);
        else if (next.LastUpdated > proof.LastAnchor)
            proof = new(next, next.LastUpdated, proof.AnchorSamples + 1, next.ObservedMono,
                next.ObservedMono > proof.LastObservedMono ? proof.ObservationSamples + 1 : proof.ObservationSamples);
        else if (next.LastUpdated < proof.LastAnchor)
            proof = new(next, next.LastUpdated, 1, next.ObservedMono, 1);
        else
            proof = proof with
            {
                Snapshot = next,
                LastObservedMono = Math.Max(proof.LastObservedMono, next.ObservedMono),
                ObservationSamples = next.ObservedMono > proof.LastObservedMono
                    ? proof.ObservationSamples + 1 : proof.ObservationSamples
            };

        pending = settle with { Candidate = proof };
        var exactPausedReconnect = next.State == PlaybackState.Paused && settle.Before is { } prior &&
            next.Track.Key == prior.Track.Key;
        var proven = next.State == PlaybackState.Playing
            ? proof.AnchorSamples >= 2
            : exactPausedReconnect && proof.ObservationSamples >= 2;
        if (!proven)
            return SpotifyGateResult.Settling;

        accepted = next;
        lastAccepted = next;
        pending = null;
        return SpotifyGateResult.Accept;
    }

    private static bool SameCandidate(PlaybackSnapshot left, PlaybackSnapshot right) =>
        left.SessionId == right.SessionId && left.Track.Key == right.Track.Key &&
        left.RawTitle == right.RawTitle && left.RawArtist == right.RawArtist &&
        left.RawAlbum == right.RawAlbum && left.TrackNumber == right.TrackNumber &&
        left.Start == right.Start && left.End == right.End && left.State == right.State;

    private static bool RecordingMetadataChanged(PlaybackSnapshot left, PlaybackSnapshot right) =>
        left.RawTitle != right.RawTitle || left.RawArtist != right.RawArtist ||
        left.RawAlbum != right.RawAlbum || left.TrackNumber != right.TrackNumber;

    private static bool TimelineChanged(PlaybackSnapshot previous, PlaybackSnapshot next)
    {
        if (previous.Start != next.Start || previous.End != next.End || previous.Position - next.Position > 0.75)
            return true;
        if (previous.State != PlaybackState.Playing || next.State != PlaybackState.Playing)
            return false;
        var elapsed = (next.LastUpdated - previous.LastUpdated).TotalSeconds;
        return elapsed >= 0 && elapsed < 30 &&
            Math.Abs(next.Position - (previous.Position + elapsed * previous.Rate)) > 1.5;
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
