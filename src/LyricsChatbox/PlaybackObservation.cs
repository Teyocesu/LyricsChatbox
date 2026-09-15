namespace LyricsChatbox;

public sealed class NullPlaybackObservationDeduper
{
    private readonly object gate = new();
    private bool hasNull;
    private string? status;
    private long revision;

    public bool ShouldEmit(PlaybackSnapshot? snapshot, string nextStatus, long nextRevision)
    {
        lock (gate)
        {
            if (snapshot is not null)
            {
                hasNull = false;
                status = null;
                revision = 0;
                return true;
            }
            if (hasNull && status == nextStatus && revision == nextRevision) return false;
            hasNull = true;
            status = nextStatus;
            revision = nextRevision;
            return true;
        }
    }
}
