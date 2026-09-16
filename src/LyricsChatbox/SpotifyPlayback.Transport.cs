using Windows.Media;
using Windows.Media.Control;

namespace LyricsChatbox;

public sealed partial class SpotifyPlayback
{
    private int transportBusy;
    public MediaControls GetControls()
    {
        try
        {
            var current = session;
            if (current is null || stop.IsCancellationRequested) return new();
            var info = current.GetPlaybackInfo(); var c = info.Controls;
            var playing = info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            return new(playing, c.IsPlayPauseToggleEnabled || (playing ? c.IsPauseEnabled : c.IsPlayEnabled),
                c.IsPreviousEnabled, c.IsNextEnabled, c.IsShuffleEnabled, c.IsRepeatEnabled,
                info.IsShuffleActive, info.AutoRepeatMode);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return new(); }
    }

    public async Task<bool> ControlAsync(MediaCommand command, long expectedRevision)
    {
        if (Interlocked.CompareExchange(ref transportBusy, 1, 0) != 0) return false;
        try
        {
            var active = session;
            if (active is null || expectedRevision != Revision || stop.IsCancellationRequested) return false;
            var info = active.GetPlaybackInfo(); var c = info.Controls;
            var playing = info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            Windows.Foundation.IAsyncOperation<bool>? operation = null;
            switch (command)
            {
                case MediaCommand.PlayPause:
                    if (playing && c.IsPauseEnabled) operation = active.TryPauseAsync();
                    else if (!playing && c.IsPlayEnabled) operation = active.TryPlayAsync();
                    else if (c.IsPlayPauseToggleEnabled) operation = active.TryTogglePlayPauseAsync();
                    break;
                case MediaCommand.Previous when c.IsPreviousEnabled: operation = active.TrySkipPreviousAsync(); break;
                case MediaCommand.Next when c.IsNextEnabled: operation = active.TrySkipNextAsync(); break;
                case MediaCommand.Shuffle when c.IsShuffleEnabled && info.IsShuffleActive.HasValue:
                    operation = active.TryChangeShuffleActiveAsync(!info.IsShuffleActive.Value); break;
                case MediaCommand.Repeat when c.IsRepeatEnabled && info.AutoRepeatMode.HasValue:
                    var repeat = info.AutoRepeatMode.Value switch
                    {
                        MediaPlaybackAutoRepeatMode.None => MediaPlaybackAutoRepeatMode.List,
                        MediaPlaybackAutoRepeatMode.List => MediaPlaybackAutoRepeatMode.Track,
                        _ => MediaPlaybackAutoRepeatMode.None
                    };
                    operation = active.TryChangeAutoRepeatModeAsync(repeat); break;
            }
            if (operation is null) return false;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            var accepted = await operation.AsTask(timeout.Token);
            Wake();
            // A successful Next/Previous may advance the media revision before GSMTC completes
            // this operation. The caller's source revision guard rejects that stale completion.
            return accepted && ReferenceEquals(active, session);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return false; }
        finally { Volatile.Write(ref transportBusy, 0); }
    }
}
