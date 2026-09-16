using Windows.Media.Control;

namespace LyricsChatbox;

public sealed partial class SpotifyPlayback : IPlaybackSource
{
    public const string VerifiedSource = "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify";
    private static readonly HashSet<string> VerifiedSources = [VerifiedSource];
    public static bool IsVerifiedSource(string? id) => id is not null && VerifiedSources.Contains(id);
    public static T? SelectUniqueVerifiedSession<T>(IEnumerable<(string Source, T Session)> sessions) where T : class
    {
        var matches = sessions.Where(item => IsVerifiedSource(item.Source)).Select(item => item.Session).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }
    public PlaybackSourceKind Kind => PlaybackSourceKind.Spotify;
    public string DisplayName => "Spotify";
    public IPlaybackVolume? Volume { get; } = new SpotifyPlaybackVolume();
    private readonly CancellationTokenSource stop = new();
    private readonly SemaphoreSlim wake = new(0, 1);
    private readonly SpotifyTransitionGate transitions = new();
    private readonly object transitionLock = new();
    private readonly NullPlaybackObservationDeduper nullObservations = new();
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private GlobalSystemMediaTransportControlsSession? session;
    private Task? loop;
    private long revision;
    private long sessionNumber;
    private long artworkRevision = -1;
    private string? artworkKey;
    public long Revision => Interlocked.Read(ref revision);
    public event Action<PlaybackSnapshot?, string, long>? Observed;
    public event Action<TrackIdentity, long, Windows.Storage.Streams.IRandomAccessStreamReference?>? ArtworkAvailable;

    public void Start() => loop ??= Task.Run(RunAsync);
    public void Suspend() => Invalidate("Playback suspended", false);
    public void ReanchorAfterResume() => Invalidate("Refreshing Spotify playback");
    private void Wake() { try { wake.Release(); } catch (SemaphoreFullException) { } catch (ObjectDisposedException) { } }
    private void Invalidate(string status, bool wakeLoop = true)
    {
        var rev = Interlocked.Increment(ref revision);
        Emit(null, status, rev);
        if (wakeLoop) Wake();
    }
    private void Emit(PlaybackSnapshot? snapshot, string status, long rev)
    {
        if (nullObservations.ShouldEmit(snapshot, status, rev)) Observed?.Invoke(snapshot, status, rev);
    }
    private void SessionsChanged(GlobalSystemMediaTransportControlsSessionManager _, SessionsChangedEventArgs __) =>
        Invalidate("Checking Spotify session");
    private void MediaChanged(GlobalSystemMediaTransportControlsSession _, MediaPropertiesChangedEventArgs __)
    {
        lock (transitionLock) transitions.InvalidateForMediaChange(MonotonicClock.Now);
        Invalidate("Updating Spotify track");
    }
    private void PlaybackChanged(GlobalSystemMediaTransportControlsSession _, PlaybackInfoChangedEventArgs __) => Wake();
    private void TimelineChanged(GlobalSystemMediaTransportControlsSession _, TimelinePropertiesChangedEventArgs __) => Wake();

    private void Select(GlobalSystemMediaTransportControlsSession? next, string? emptyStatus = null)
    {
        if (Equals(session, next)) return;
        if (session is not null)
        {
            session.MediaPropertiesChanged -= MediaChanged;
            session.PlaybackInfoChanged -= PlaybackChanged;
            session.TimelinePropertiesChanged -= TimelineChanged;
        }
        session = next;
        sessionNumber++;
        lock (transitionLock) transitions.Reset();
        artworkKey = null; artworkRevision = -1;
        if (session is not null)
        {
            session.MediaPropertiesChanged += MediaChanged;
            session.PlaybackInfoChanged += PlaybackChanged;
            session.TimelinePropertiesChanged += TimelineChanged;
        }
        Invalidate(session is null ? emptyStatus ?? "No Spotify session" : "Reading Spotify");
    }

    private async Task RunAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                if (manager is null)
                {
                    manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(stop.Token)
                        .WaitAsync(TimeSpan.FromSeconds(3), stop.Token);
                    manager.SessionsChanged += SessionsChanged;
                }
                var matches = manager.GetSessions().Where(s => IsVerifiedSource(s.SourceAppUserModelId)).ToArray();
                Select(matches.Length == 1 ? matches[0] : null,
                    matches.Length > 1 ? "Multiple Spotify sessions · waiting" : "No Spotify session");
                var active = session;
                if (active is null)
                    Emit(null, matches.Length > 1 ? "Multiple Spotify sessions · waiting" : "No Spotify session", Revision);
                else
                {
                    var rev = Revision;
                    var media = await active.TryGetMediaPropertiesAsync().AsTask(stop.Token)
                        .WaitAsync(TimeSpan.FromSeconds(2), stop.Token);
                    var timeline = active.GetTimelineProperties();
                    var info = active.GetPlaybackInfo();
                    var utc = DateTimeOffset.UtcNow;
                    var mono = MonotonicClock.Now;
                    var state = info.PlaybackStatus switch
                    {
                        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => PlaybackState.Playing,
                        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => PlaybackState.Paused,
                        _ => PlaybackState.Stopped
                    };
                    var track = new TrackIdentity(media.Title, media.Artist, media.AlbumTitle,
                        (timeline.EndTime - timeline.StartTime).TotalSeconds);
                    var snapshot = new PlaybackSnapshot(VerifiedSource + ":" + sessionNumber, track,
                        media.Title, media.Artist, media.AlbumTitle, media.TrackNumber,
                        timeline.StartTime.TotalSeconds, timeline.EndTime.TotalSeconds,
                        timeline.Position.TotalSeconds, timeline.LastUpdatedTime, state, info.PlaybackRate ?? 1,
                        utc, mono, PlaybackSourceKind.Spotify);
                    if (rev == Revision && ReferenceEquals(active, session))
                    {
                        SpotifyGateResult result;
                        lock (transitionLock) result = transitions.Observe(snapshot);
                        if (result == SpotifyGateResult.Invalidate) Invalidate("Updating Spotify playback");
                        else if (result == SpotifyGateResult.Settling) Emit(null, "Settling Spotify track", Revision);
                        else
                        {
                            Emit(snapshot, "Spotify · " + state.ToString().ToLowerInvariant(), Revision);
                            if (artworkRevision != Revision || artworkKey != track.Key)
                            {
                                artworkRevision = Revision; artworkKey = track.Key;
                                ArtworkAvailable?.Invoke(track, Revision, media.Thumbnail);
                            }
                        }
                    }
                }
                await wake.WaitAsync(TimeSpan.FromMilliseconds(225), stop.Token);
                await Task.Delay(30, stop.Token);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                if (session is null) Invalidate("Spotify unavailable · reconnecting");
                else Select(null, "Spotify unavailable · reconnecting");
                if (manager is not null) manager.SessionsChanged -= SessionsChanged;
                manager = null;
                try { await Task.Delay(1000, stop.Token); } catch (OperationCanceledException) { break; }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync();
        if (loop is not null) await loop;
        Select(null);
        if (manager is not null) manager.SessionsChanged -= SessionsChanged;
        wake.Dispose(); stop.Dispose();
    }
}
