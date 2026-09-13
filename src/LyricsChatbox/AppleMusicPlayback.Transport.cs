using Windows.Media;
using Windows.Media.Control;

namespace LyricsChatbox;

public enum MediaCommand { PlayPause, Previous, Next, Seek, Shuffle, Repeat }
public sealed record MediaControls(bool Playing = false, bool PlayPause = false, bool Previous = false,
    bool Next = false, bool Seek = false, bool Shuffle = false, bool Repeat = false,
    bool? Shuffling = null, MediaPlaybackAutoRepeatMode? RepeatMode = null);

public sealed partial class AppleMusicPlayback
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
                c.IsPreviousEnabled, c.IsNextEnabled, c.IsPlaybackPositionEnabled, c.IsShuffleEnabled,
                c.IsRepeatEnabled, info.IsShuffleActive, info.AutoRepeatMode);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return new(); }
    }

    public async Task<bool> ControlAsync(MediaCommand command, long expectedRevision, double fraction = 0)
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
                    var repeat = info.AutoRepeatMode.Value switch { MediaPlaybackAutoRepeatMode.None => MediaPlaybackAutoRepeatMode.List, MediaPlaybackAutoRepeatMode.List => MediaPlaybackAutoRepeatMode.Track, _ => MediaPlaybackAutoRepeatMode.None };
                    operation = active.TryChangeAutoRepeatModeAsync(repeat); break;
                case MediaCommand.Seek when c.IsPlaybackPositionEnabled && double.IsFinite(fraction) && fraction >= 0 && fraction <= 1:
                    var timeline = active.GetTimelineProperties();
                    var ticks = timeline.StartTime.Ticks + (long)((timeline.EndTime.Ticks - timeline.StartTime.Ticks) * fraction);
                    if (timeline.EndTime > timeline.StartTime) operation = active.TryChangePlaybackPositionAsync(ticks);
                    break;
            }
            if (operation is null) return false;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            var accepted = await operation.AsTask(timeout.Token);
            Wake();
            return accepted && ReferenceEquals(active, session);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return false; }
        finally { Volatile.Write(ref transportBusy, 0); }
    }
}
