using Windows.Storage.Streams;

namespace LyricsChatbox;

public sealed record PlaybackCandidate(bool Present = false, PlaybackState? State = null);
public sealed record PlaybackChoice(PlaybackSourceKind? Source, string Status, bool Ambiguous = false);

public static class AutomaticPlaybackSelector
{
    public static PlaybackChoice Choose(PlaybackCandidate apple, PlaybackCandidate spotify, PlaybackSourceKind? bound)
    {
        var aPlaying = apple.Present && apple.State == PlaybackState.Playing;
        var sPlaying = spotify.Present && spotify.State == PlaybackState.Playing;
        if (aPlaying && sPlaying)
            return new(null, "Apple Music and Spotify are both playing. Choose a playback source.", true);
        if (aPlaying) return new(PlaybackSourceKind.AppleMusic, "Apple Music");
        if (sPlaying) return new(PlaybackSourceKind.Spotify, "Spotify");
        if (bound == PlaybackSourceKind.AppleMusic && apple.Present && apple.State == PlaybackState.Paused)
            return new(bound, "Apple Music");
        if (bound == PlaybackSourceKind.Spotify && spotify.Present && spotify.State == PlaybackState.Paused)
            return new(bound, "Spotify");
        if (apple.Present && !spotify.Present && apple.State == PlaybackState.Paused)
            return new(PlaybackSourceKind.AppleMusic, "Apple Music");
        if (spotify.Present && !apple.Present && spotify.State == PlaybackState.Paused)
            return new(PlaybackSourceKind.Spotify, "Spotify");
        if (apple.Present && spotify.Present)
            return new(null, "Choose a playback source in Settings.", true);
        return new(null, "Waiting for a music player");
    }
}

// Two candidate observers, one selected host. Candidate state never reaches lyrics or artwork.
public sealed class PlaybackSourceCoordinator : IAsyncDisposable
{
    private readonly IPlaybackSource apple;
    private readonly IPlaybackSource spotify;
    private readonly PlaybackSourceHost host;
    private readonly object gate = new();
    private readonly SemaphoreSlim transition = new(1, 1);
    private PlaybackSourceMode mode;
    private PlaybackCandidate appleCandidate = new(), spotifyCandidate = new();
    private bool appleKnown, spotifyKnown;
    private PlaybackSourceKind? desired;
    private PlaybackSourceKind? bound;
    private bool blocked;
    private string status;
    private long generation;
    private bool disposed;
    public PlaybackSourceCoordinator(PlaybackSourceMode mode, IPlaybackSource? apple = null, IPlaybackSource? spotify = null)
    {
        this.apple = apple ?? new AppleMusicPlayback();
        this.spotify = spotify ?? new SpotifyPlayback();
        this.mode = mode;
        status = mode switch
        {
            PlaybackSourceMode.AppleMusic => "Apple Music",
            PlaybackSourceMode.Spotify => "Spotify",
            _ => "Waiting for a music player"
        };
        host = new PlaybackSourceHost(mode == PlaybackSourceMode.Spotify ? this.spotify : this.apple);
        bound = mode == PlaybackSourceMode.Automatic ? null : host.Kind;
        desired = bound;
        blocked = mode == PlaybackSourceMode.Automatic;
        this.apple.Observed += AppleObserved;
        this.spotify.Observed += SpotifyObserved;
        host.Observed += HostObserved;
        host.ArtworkAvailable += HostArtwork;
    }
    public PlaybackSourceMode Mode { get { lock (gate) return mode; } }
    public PlaybackSourceKind? ActiveKind { get { lock (gate) return blocked ? null : bound; } }
    public string Status { get { lock (gate) return status; } }
    public bool Ambiguous { get { lock (gate) return blocked && status.Contains("Choose a playback source", StringComparison.Ordinal); } }
    public long Revision => host.Revision + Interlocked.Read(ref generation);
    public IPlaybackVolume? Volume => ActiveKind is null ? null : host.Volume;
    public event Action<PlaybackSnapshot?, string, long>? Observed;
    public event Action<TrackIdentity, long, IRandomAccessStreamReference?>? ArtworkAvailable;
    public void Start()
    {
        apple.Start(); spotify.Start(); host.Start();
        if (Mode == PlaybackSourceMode.Automatic) Observed?.Invoke(null, Status, Revision);
    }
    public void Suspend() { apple.Suspend(); spotify.Suspend(); }
    public void ReanchorAfterResume() { apple.ReanchorAfterResume(); spotify.ReanchorAfterResume(); }
    public MediaControls GetControls() => ActiveKind is null ? new() : host.GetControls();
    public Task<bool> ControlAsync(MediaCommand command, long expectedRevision) =>
        ActiveKind is not null && expectedRevision == Revision
            ? host.ControlAsync(command, host.Revision) : Task.FromResult(false);

    public async Task SetModeAsync(PlaybackSourceMode next)
    {
        lock (gate)
        {
            if (mode == next) return;
            mode = next;
            RechooseLocked();
        }
        await ApplyDesiredAsync();
    }

    private void AppleObserved(PlaybackSnapshot? snapshot, string message, long _) =>
        CandidateObserved(PlaybackSourceKind.AppleMusic, snapshot, message);
    private void SpotifyObserved(PlaybackSnapshot? snapshot, string message, long _) =>
        CandidateObserved(PlaybackSourceKind.Spotify, snapshot, message);
    private void CandidateObserved(PlaybackSourceKind kind, PlaybackSnapshot? snapshot, string message)
    {
        bool changed;
        lock (gate)
        {
            var current = kind == PlaybackSourceKind.AppleMusic ? appleCandidate : spotifyCandidate;
            var absent = message.StartsWith("No ", StringComparison.Ordinal) || message.StartsWith("Multiple ", StringComparison.Ordinal) ||
                message.Contains("unavailable", StringComparison.OrdinalIgnoreCase);
            var next = snapshot is not null ? new PlaybackCandidate(true, snapshot.State) :
                absent ? new PlaybackCandidate() : current;
            if (kind == PlaybackSourceKind.AppleMusic)
            {
                appleCandidate = next;
                if (snapshot is not null || absent) appleKnown = true;
            }
            else
            {
                spotifyCandidate = next;
                if (snapshot is not null || absent) spotifyKnown = true;
            }
            changed = mode == PlaybackSourceMode.Automatic && RechooseLocked();
        }
        if (changed) _ = ApplyDesiredAsync();
    }
    private bool RechooseLocked()
    {
        var choice = mode switch
        {
            PlaybackSourceMode.AppleMusic => new PlaybackChoice(PlaybackSourceKind.AppleMusic, "Apple Music"),
            PlaybackSourceMode.Spotify => new PlaybackChoice(PlaybackSourceKind.Spotify, "Spotify"),
            _ when !appleKnown || !spotifyKnown => new PlaybackChoice(null, "Checking music players"),
            _ => AutomaticPlaybackSelector.Choose(appleCandidate, spotifyCandidate, bound)
        };
        var changed = desired != choice.Source || (choice.Source is null && status != choice.Status) ||
            (mode != PlaybackSourceMode.Automatic && bound != choice.Source);
        desired = choice.Source; status = choice.Status;
        if (!changed) return false;
        blocked = true;
        Interlocked.Increment(ref generation);
        Observed?.Invoke(null, choice.Source is null ? choice.Status : "Refreshing playback source", Revision);
        return true;
    }
    private async Task ApplyDesiredAsync()
    {
        await transition.WaitAsync();
        try
        {
            while (!disposed)
            {
                PlaybackSourceKind? target;
                lock (gate) target = desired;
                if (target is null) return;
                var source = target == PlaybackSourceKind.AppleMusic ? apple : spotify;
                if (host.Kind != target) await host.SwitchAsync(source, false);
                bool settled;
                lock (gate)
                {
                    settled = desired == target;
                    if (settled)
                    {
                        bound = target;
                        blocked = false;
                        Interlocked.Increment(ref generation);
                    }
                }
                if (settled)
                {
                    // Even an already-running candidate must re-publish a fresh selected observation.
                    source.ReanchorAfterResume();
                    return;
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            lock (gate) { blocked = true; status = "Music player unavailable · retrying"; Interlocked.Increment(ref generation); }
            Observed?.Invoke(null, Status, Revision);
        }
        finally { transition.Release(); }
    }
    private void HostObserved(PlaybackSnapshot? snapshot, string message, long hostRevision)
    {
        bool allow;
        lock (gate) allow = !blocked && bound == host.Kind && desired == bound;
        if (!allow || hostRevision != host.Revision) return;
        Observed?.Invoke(snapshot, message, hostRevision + Interlocked.Read(ref generation));
    }
    private void HostArtwork(TrackIdentity track, long hostRevision, IRandomAccessStreamReference? reference)
    {
        bool allow;
        lock (gate) allow = !blocked && bound == host.Kind && desired == bound;
        if (allow && hostRevision == host.Revision)
            ArtworkAvailable?.Invoke(track, hostRevision + Interlocked.Read(ref generation), reference);
    }
    public async ValueTask DisposeAsync()
    {
        disposed = true;
        apple.Observed -= AppleObserved; spotify.Observed -= SpotifyObserved;
        host.Observed -= HostObserved; host.ArtworkAvailable -= HostArtwork;
        await host.DisposeAsync();
        await (host.Kind == PlaybackSourceKind.AppleMusic ? spotify : apple).DisposeAsync();
        transition.Dispose();
    }
}
