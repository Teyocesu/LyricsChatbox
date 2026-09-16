using Windows.Storage.Streams;

namespace LyricsChatbox;

public interface IPlaybackVolume
{
    MusicVolume? Read();
    bool Set(MusicVolume expected, float level);
}

public interface IPlaybackSource : IAsyncDisposable
{
    PlaybackSourceKind Kind { get; }
    string DisplayName { get; }
    long Revision { get; }
    IPlaybackVolume? Volume { get; }
    event Action<PlaybackSnapshot?, string, long>? Observed;
    event Action<TrackIdentity, long, IRandomAccessStreamReference?>? ArtworkAvailable;
    void Start();
    void Suspend();
    void ReanchorAfterResume();
    MediaControls GetControls();
    Task<bool> ControlAsync(MediaCommand command, long expectedRevision);
}

// One selected source at a time. A switch publishes null first and rejects even already-queued
// callbacks from the previous source by generation/revision; the consumer owns epoch invalidation.
public sealed class PlaybackSourceHost : IAsyncDisposable
{
    private IPlaybackSource active;
    private long offset;
    private long generation;
    private bool started;
    private Action<PlaybackSnapshot?, string, long>? observedHandler;
    private Action<TrackIdentity, long, IRandomAccessStreamReference?>? artworkHandler;
    public PlaybackSourceHost(IPlaybackSource initial)
    {
        active = initial;
        Attach();
    }
    public PlaybackSourceKind Kind => active.Kind;
    public string DisplayName => active.DisplayName;
    public long Revision => Interlocked.Read(ref offset) + active.Revision;
    public IPlaybackVolume? Volume => active.Volume;
    public event Action<PlaybackSnapshot?, string, long>? Observed;
    public event Action<TrackIdentity, long, IRandomAccessStreamReference?>? ArtworkAvailable;
    public void Start() { started = true; active.Start(); }
    public void Suspend() => active.Suspend();
    public void ReanchorAfterResume() => active.ReanchorAfterResume();
    public MediaControls GetControls() => active.GetControls();
    public Task<bool> ControlAsync(MediaCommand command, long expectedRevision)
    {
        var source = active;
        var local = source.Revision;
        return expectedRevision == Revision && ReferenceEquals(source, active)
            ? source.ControlAsync(command, local) : Task.FromResult(false);
    }
    public async Task SwitchAsync(IPlaybackSource next, bool disposePrevious = true)
    {
        if (ReferenceEquals(next, active)) return;
        var old = active;
        var previousRevision = Revision;
        Detach();
        active = next;
        Interlocked.Increment(ref generation);
        Interlocked.Exchange(ref offset, previousRevision + 1);
        Attach();
        Observed?.Invoke(null, "Refreshing playback source", Revision);
        if (started) next.Start();
        if (disposePrevious) await old.DisposeAsync();
    }
    private void Attach()
    {
        var source = active;
        var selectedGeneration = Interlocked.Read(ref generation);
        observedHandler = (snapshot, status, localRevision) =>
        {
            if (selectedGeneration != Interlocked.Read(ref generation) || !ReferenceEquals(source, active)) return;
            if (snapshot is not null && snapshot.Source != source.Kind) snapshot = null;
            Observed?.Invoke(snapshot, status, Interlocked.Read(ref offset) + localRevision);
        };
        artworkHandler = (track, localRevision, reference) =>
        {
            if (selectedGeneration != Interlocked.Read(ref generation) || !ReferenceEquals(source, active)) return;
            ArtworkAvailable?.Invoke(track, Interlocked.Read(ref offset) + localRevision, reference);
        };
        source.Observed += observedHandler;
        source.ArtworkAvailable += artworkHandler;
    }
    private void Detach()
    {
        if (observedHandler is not null) active.Observed -= observedHandler;
        if (artworkHandler is not null) active.ArtworkAvailable -= artworkHandler;
        observedHandler = null; artworkHandler = null;
    }
    public async ValueTask DisposeAsync() { Detach(); await active.DisposeAsync(); }
}
