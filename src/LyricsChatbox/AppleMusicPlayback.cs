using Windows.Media.Control;

namespace LyricsChatbox;

public sealed class AppleMusicPlayback : IAsyncDisposable
{
    public const string AppleSource = "AppleInc.AppleMusicWin_nzyj5cx40ttqa!App";
    private readonly CancellationTokenSource stop = new();
    private readonly SemaphoreSlim wake = new(0, 1);
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private GlobalSystemMediaTransportControlsSession? session;
    private Task? loop;
    private long revision;
    private long sessionNumber;
    private double settleUntil;
    private long artworkRevision = -1;
    private string? artworkKey;
    public long Revision => Interlocked.Read(ref revision);
    public event Action<PlaybackSnapshot?, string, long>? Observed;
    public event Action<TrackIdentity, long, Windows.Storage.Streams.IRandomAccessStreamReference?>? ArtworkAvailable;

    public void Start() => loop ??= Task.Run(RunAsync);
    public void ReanchorAfterResume() => Invalidate("Refreshing playback after resume");
    private void Wake() { try { wake.Release(); } catch (SemaphoreFullException) { } catch (ObjectDisposedException) { } }
    private void Invalidate(string status)
    {
        var rev = Interlocked.Increment(ref revision);
        Volatile.Write(ref settleUntil, MonotonicClock.Now + 0.3);
        Observed?.Invoke(null, status, rev);
        Wake();
    }
    private void SessionsChanged(GlobalSystemMediaTransportControlsSessionManager _, SessionsChangedEventArgs __) => Invalidate("Checking Apple Music session");
    private void MediaChanged(GlobalSystemMediaTransportControlsSession _, MediaPropertiesChangedEventArgs __) => Invalidate("Updating track");
    private void PlaybackChanged(GlobalSystemMediaTransportControlsSession _, PlaybackInfoChangedEventArgs __) => Wake();
    private void TimelineChanged(GlobalSystemMediaTransportControlsSession _, TimelinePropertiesChangedEventArgs __) => Wake();

    private void Select(GlobalSystemMediaTransportControlsSession? next)
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
        if (session is not null)
        {
            session.MediaPropertiesChanged += MediaChanged;
            session.PlaybackInfoChanged += PlaybackChanged;
            session.TimelinePropertiesChanged += TimelineChanged;
        }
        Invalidate(session is null ? "No Apple Music session" : "Reading Apple Music");
    }

    private async Task RunAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                if (manager is null)
                {
                    manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(stop.Token).WaitAsync(TimeSpan.FromSeconds(3), stop.Token);
                    manager.SessionsChanged += SessionsChanged;
                }
                var matches = manager.GetSessions().Where(s => s.SourceAppUserModelId == AppleSource).ToArray();
                // If more than one native session exists, do not guess which recording the user hears.
                Select(matches.Length == 1 ? matches[0] : null);
                var active = session;
                if (active is null)
                    Observed?.Invoke(null, matches.Length > 1 ? "Multiple Apple Music sessions · waiting" : "No Apple Music session", Revision);
                else if (MonotonicClock.Now >= Volatile.Read(ref settleUntil))
                {
                    var rev = Revision;
                    var media = await active.TryGetMediaPropertiesAsync().AsTask(stop.Token).WaitAsync(TimeSpan.FromSeconds(2), stop.Token);
                    var timeline = active.GetTimelineProperties();
                    var playback = active.GetPlaybackInfo();
                    var now = DateTimeOffset.UtcNow;
                    var mono = MonotonicClock.Now;
                    var state = playback.PlaybackStatus switch
                    {
                        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => PlaybackState.Playing,
                        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => PlaybackState.Paused,
                        _ => PlaybackState.Stopped
                    };
                    var track = TrackIdentity.FromApple(media.Title, media.Artist, media.AlbumTitle,
                        (timeline.EndTime - timeline.StartTime).TotalSeconds);
                    if (rev == Revision && ReferenceEquals(active, session))
                    {
                        var snapshot = new PlaybackSnapshot(AppleSource + ":" + sessionNumber, track, media.Title, media.Artist,
                            media.AlbumTitle, media.TrackNumber, timeline.StartTime.TotalSeconds, timeline.EndTime.TotalSeconds,
                            timeline.Position.TotalSeconds, timeline.LastUpdatedTime, state, playback.PlaybackRate ?? 1, now, mono);
                        Observed?.Invoke(snapshot, "Apple Music · " + state.ToString().ToLowerInvariant(), rev);
                        if (artworkRevision != rev || artworkKey != track.Key)
                        {
                            artworkRevision = rev; artworkKey = track.Key;
                            ArtworkAvailable?.Invoke(track, rev, media.Thumbnail);
                        }
                    }
                }
                await wake.WaitAsync(TimeSpan.FromMilliseconds(250), stop.Token);
                // Bound burst event work without losing the latest authoritative state.
                await Task.Delay(30, stop.Token);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Invalidate("Apple Music unavailable · reconnecting");
                Select(null);
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
