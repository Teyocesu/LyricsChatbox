using System.Diagnostics;
using System.Text.Json;
using Windows.Media.Control;
using LyricsChatbox;

Console.OutputEncoding = System.Text.Encoding.UTF8;
var duration = args.Length > 0 && int.TryParse(args[0], out var seconds) ? seconds : 25;
var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
var clock = Stopwatch.StartNew();
var tracked = new HashSet<GlobalSystemMediaTransportControlsSession>();
var playbackClocks = new Dictionary<string, PlaybackClock>();
var exercise = args.Contains("--exercise");
var actions = new Queue<(double At, string Name)>(exercise ? new[] { (1d,"play"), (22d,"pause"), (28d,"play"), (34d,"forward"), (40d,"backward"), (46d,"restart"), (52d,"next"), (58d,"previous"), (64d,"pause") } : []);
void Log(object data) => Console.WriteLine(JsonSerializer.Serialize(data));
void Event(string name, string? source = null) => Log(new { kind = "event", name, source, mono = clock.Elapsed.TotalSeconds });
manager.SessionsChanged += (_, _) => Event("SessionsChanged");
manager.CurrentSessionChanged += (_, _) => Event("CurrentSessionChanged");
while (clock.Elapsed.TotalSeconds < duration)
{
    var sessions = manager.GetSessions();
    if (actions.TryPeek(out var action) && clock.Elapsed.TotalSeconds >= action.At)
    {
        actions.Dequeue();
        var apple = sessions.FirstOrDefault(s => s.SourceAppUserModelId == "AppleInc.AppleMusicWin_nzyj5cx40ttqa!App");
        if (apple is not null)
        {
            var ok = action.Name switch {
                "play" => await apple.TryPlayAsync(), "pause" => await apple.TryPauseAsync(),
                "next" => await apple.TrySkipNextAsync(), "previous" => await apple.TrySkipPreviousAsync(),
                "forward" => await apple.TryChangePlaybackPositionAsync(TimeSpan.FromSeconds(100).Ticks),
                "backward" => await apple.TryChangePlaybackPositionAsync(TimeSpan.FromSeconds(20).Ticks),
                "restart" => await apple.TryChangePlaybackPositionAsync(0), _ => false };
            Log(new { kind = "action", action.Name, ok, mono = clock.Elapsed.TotalSeconds });
        }
    }
    if (sessions.Count == 0) Log(new { kind = "no-session", mono = clock.Elapsed.TotalSeconds });
    foreach (var session in sessions)
    {
        var source = session.SourceAppUserModelId;
        if (tracked.Add(session))
        {
            session.MediaPropertiesChanged += (_, _) => Event("MediaPropertiesChanged", source);
            session.PlaybackInfoChanged += (_, _) => Event("PlaybackInfoChanged", source);
            session.TimelinePropertiesChanged += (_, _) => Event("TimelinePropertiesChanged", source);
        }
        try
        {
            var media = await session.TryGetMediaPropertiesAsync();
            var playback = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();
            var observed = DateTimeOffset.UtcNow;
            var mono = MonotonicClock.Now;
            if (!playbackClocks.TryGetValue(source, out var engineClock)) playbackClocks[source] = engineClock = new();
            var identity = TrackIdentity.FromApple(media.Title, media.Artist, media.AlbumTitle, (timeline.EndTime - timeline.StartTime).TotalSeconds);
            var state = playback.PlaybackStatus switch {
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => PlaybackState.Playing,
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => PlaybackState.Paused, _ => PlaybackState.Stopped };
            engineClock.Observe(new(source, identity, media.Title, media.Artist, media.AlbumTitle, media.TrackNumber,
                timeline.StartTime.TotalSeconds, timeline.EndTime.TotalSeconds, timeline.Position.TotalSeconds,
                timeline.LastUpdatedTime, state, playback.PlaybackRate ?? 1, observed, mono));
            Log(new { kind = "sample", mono = clock.Elapsed.TotalSeconds, observed, source,
                title = media.Title, artist = media.Artist, album = media.AlbumTitle, track = media.TrackNumber,
                status = playback.PlaybackStatus.ToString(), rate = playback.PlaybackRate,
                start = timeline.StartTime.TotalSeconds, end = timeline.EndTime.TotalSeconds,
                position = timeline.Position.TotalSeconds, updated = timeline.LastUpdatedTime,
                age = (observed - timeline.LastUpdatedTime).TotalSeconds,
                enginePosition = engineClock.Position(mono), discontinuity = engineClock.Discontinuity });
        }
        catch (Exception ex) { Log(new { kind = "error", source, type = ex.GetType().Name, ex.Message }); }
    }
    await Task.Delay(250);
}
