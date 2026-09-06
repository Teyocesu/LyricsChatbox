namespace LyricsChatbox;

// Owned by one dispatcher. External/async results enter only through epoch-checked Complete.
public sealed class SynchronizationEngine
{
    private readonly PlaybackClock clock = new();
    private string? sessionId;
    public long Epoch { get; private set; }
    public TrackIdentity? Track { get; private set; }
    public PlaybackSnapshot? Snapshot { get; private set; }
    public LyricTimeline? Timeline { get; private set; }
    public string LyricsStatus { get; private set; } = "Waiting for a track";
    public DateTimeOffset? RetryAt { get; private set; }
    public bool Enabled { get; set; }
    public double Offset { get; set; }
    public bool Observe(PlaybackSnapshot? next)
    {
        var changed = next?.SessionId != sessionId || next?.Track != Track;
        if (changed)
        {
            Epoch++; Timeline = null; RetryAt = null;
            Track = next?.Track; sessionId = next?.SessionId;
            LyricsStatus = Track is null ? "Waiting for a track" : "Looking up synced lyrics";
            clock.Reset();
        }
        Snapshot = next;
        if (next is not null) clock.Observe(next); else clock.Reset();
        return changed;
    }
    public bool Complete(long epoch, LyricsResolution resolution)
    {
        if (epoch != Epoch || Track is null) return false;
        Timeline = resolution.Timeline; LyricsStatus = resolution.Status; RetryAt = resolution.RetryAt;
        return true;
    }
    public void BeginRetry() { RetryAt = null; LyricsStatus = "Looking up synced lyrics"; }
    public double? Position(double now) => clock.Position(now);
    public string Current(double now) => Position(now) is double position ? Timeline?.Current(position, Offset) ?? "" : "";
    public string Output(double now) => Enabled ? Current(now) : "";
}
