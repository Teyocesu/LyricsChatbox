// Isolated GSMTC observation. No application LocalData, lyrics, network, OSC, or production playback references.
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Windows.Media;
using Windows.Media.Control;

const int MaxSamples = 6000;
const int MaxSessions = 32;
const int MaxEvents = 2000;
const int MaxChanges = 300;
const int MaxArtworkBytes = 1_048_576;
if (args.SequenceEqual(["--self-test"]))
{
    SpikeChecks.Run();
    Console.WriteLine("Spike checks passed.");
    return;
}
var options = Options.Parse(args);
if (options is null) return;
Console.OutputEncoding = System.Text.Encoding.UTF8;
var started = DateTimeOffset.UtcNow;
var stopwatch = Stopwatch.StartNew();
var root = Path.GetFullPath(Path.Combine("artifacts", "spikes", "spotify", started.ToString("yyyyMMddTHHmmssfffZ")));
Directory.CreateDirectory(root);
var events = new ConcurrentQueue<EventRow>();
var marks = new ConcurrentQueue<MarkRow>();
var eventCount = 0;
var droppedEventCount = 0;
var report = new Report(Environment.OSVersion.VersionString, Commit(), started, options.Duration, options.Source, options.Exercise);
using var cancel = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };
if (options.Interactive)
{
    Console.WriteLine("Markers: type pause, resume, long-pause, next, previous, natural, scrub-forward, scrub-backward, close, reopen, or a multiplayer state; Enter. Ctrl+C finishes and writes the report.");
    _ = Task.Run(() =>
    {
        while (!cancel.IsCancellationRequested)
        {
            var label = Console.ReadLine();
            if (label is null) break;
            if (label.Length is > 0 and <= 80) marks.Enqueue(new(label.Trim(), DateTimeOffset.UtcNow, stopwatch.Elapsed.TotalSeconds));
        }
    });
}
GlobalSystemMediaTransportControlsSessionManager? manager = null;
var tracked = new ConcurrentDictionary<GlobalSystemMediaTransportControlsSession, TrackedSession>(ReferenceEqualityComparer.Instance);
try
{
    manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), cancel.Token);
    manager.SessionsChanged += ManagerChanged;
    var exercised = false;
    while (stopwatch.Elapsed.TotalSeconds < options.Duration && !cancel.IsCancellationRequested)
    {
        var tick = stopwatch.Elapsed.TotalSeconds;
        try
        {
            var sessions = manager.GetSessions();
            var present = new HashSet<GlobalSystemMediaTransportControlsSession>(sessions, ReferenceEqualityComparer.Instance);
            foreach (var pair in tracked.Where(p => !present.Contains(p.Key)).ToArray())
            {
                pair.Value.EndedMono = tick;
                Detach(pair.Key);
                tracked.TryRemove(pair.Key, out _);
                AddEvent("SessionDisappeared", pair.Value.Id, pair.Value.Source);
            }
            foreach (var session in sessions)
            {
                if (!tracked.TryGetValue(session, out var item))
                {
                    if (report.Sessions.Count >= MaxSessions)
                    {
                        if (!report.Errors.Contains("More than 32 GSMTC sessions; additional sessions omitted"))
                            report.Errors.Add("More than 32 GSMTC sessions; additional sessions omitted");
                        continue;
                    }
                    item = new TrackedSession("session-" + (report.Sessions.Count + 1), session.SourceAppUserModelId, tick);
                    tracked.TryAdd(session, item);
                    report.Sessions.Add(item);
                    Attach(session);
                    AddEvent("SessionAppeared", item.Id, item.Source);
                }
                try
                {
                    // A timed async media read cannot stall the entire diagnostic indefinitely.
                    var media = await session.TryGetMediaPropertiesAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2), cancel.Token);
                    var timeline = session.GetTimelineProperties();
                    var playback = session.GetPlaybackInfo();
                    var now = DateTimeOffset.UtcNow;
                    var mono = stopwatch.Elapsed.TotalSeconds;
                    var status = playback.PlaybackStatus.ToString();
                    var key = JsonSerializer.Serialize(new { media.Title, media.Artist, media.AlbumTitle, media.TrackNumber, status,
                        timeline.StartTime, timeline.EndTime, media.PlaybackType });
                    if (key != item.LastMetadataKey)
                    {
                        if (item.Changes.Count < MaxChanges)
                        {
                            item.Changes.Add(new MetadataRow(now, mono, media.Title, media.Artist, media.AlbumTitle,
                                media.TrackNumber, media.PlaybackType?.ToString() ?? "unavailable", status,
                                timeline.StartTime.TotalSeconds, timeline.EndTime.TotalSeconds,
                                media.Thumbnail is not null));
                        }
                        else item.DroppedChanges++;
                        if (item.LastMetadataKey is not null) AddEvent("MetadataOrStateChangedByPoll", item.Id, item.Source);
                        item.LastMetadataKey = key;
                        if (media.Thumbnail is not null && item.ArtworkChecks < 12)
                        {
                            item.ArtworkChecks++;
                            item.Artwork.Add(await CheckArtwork(media.Thumbnail));
                        }
                    }
                    if (item.Controls is null || item.LastControlsStatus != status)
                    {
                        var c = playback.Controls;
                        item.Controls = new(c.IsPlayEnabled, c.IsPauseEnabled, c.IsPlayPauseToggleEnabled,
                            c.IsPreviousEnabled, c.IsNextEnabled, c.IsShuffleEnabled, c.IsRepeatEnabled,
                            c.IsPlaybackPositionEnabled, playback.IsShuffleActive, playback.AutoRepeatMode?.ToString());
                        item.LastControlsStatus = status;
                    }
                    var sample = new SampleRow(now, mono, status, playback.PlaybackRate,
                        timeline.StartTime.TotalSeconds, timeline.EndTime.TotalSeconds, timeline.Position.TotalSeconds,
                        timeline.LastUpdatedTime, (now - timeline.LastUpdatedTime).TotalSeconds,
                        media.Title, media.Artist);
                    if (item.Samples.Count < MaxSamples) item.Samples.Add(sample);
                    else item.DroppedSamples++;
                    if (!exercised && options.Exercise is not null && tick >= 3)
                    {
                        exercised = true;
                        var matches = sessions.Where(s => s.SourceAppUserModelId == options.Source).ToArray();
                        if (matches.Length != 1) report.Exercises.Add(new(options.Exercise, options.Source!, false, null, "Source absent or ambiguous", now, mono));
                        else
                        {
                            var target = matches[0];
                            var info = target.GetPlaybackInfo();
                            var c = info.Controls;
                            bool enabled = options.Exercise switch
                            {
                                "play" => c.IsPlayEnabled, "pause" => c.IsPauseEnabled,
                                "next" => c.IsNextEnabled, "previous" => c.IsPreviousEnabled,
                                "shuffle" => c.IsShuffleEnabled && info.IsShuffleActive.HasValue,
                                "repeat" => c.IsRepeatEnabled && info.AutoRepeatMode.HasValue, _ => false
                            };
                            bool? result = null;
                            string? error = null;
                            if (enabled)
                            {
                                try { result = await Exercise(target, options.Exercise, info).WaitAsync(TimeSpan.FromSeconds(3), cancel.Token); }
                                catch (Exception ex) when (ex is not OutOfMemoryException) { error = ex.GetType().Name + ": " + ex.Message; }
                            }
                            report.Exercises.Add(new(options.Exercise, options.Source!, enabled, result, error, DateTimeOffset.UtcNow, stopwatch.Elapsed.TotalSeconds));
                            marks.Enqueue(new("exercise-" + options.Exercise, DateTimeOffset.UtcNow, stopwatch.Elapsed.TotalSeconds));
                        }
                    }
                }
                catch (Exception ex) when (ex is not OutOfMemoryException && !(ex is OperationCanceledException && cancel.IsCancellationRequested))
                {
                    if (item.Errors.Count < 20) item.Errors.Add(ex.GetType().Name + ": " + ex.Message);
                    else item.DroppedErrors++;
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException && !(ex is OperationCanceledException && cancel.IsCancellationRequested))
        {
            if (report.Errors.Count < 20) report.Errors.Add(ex.GetType().Name + ": " + ex.Message);
        }
        try { await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(0, 225 - (stopwatch.Elapsed.TotalSeconds - tick) * 1000)), cancel.Token); }
        catch (OperationCanceledException) { break; }
    }
}
catch (Exception ex) when (ex is not OutOfMemoryException && !(ex is OperationCanceledException && cancel.IsCancellationRequested))
{
    report.Errors.Add("Manager: " + ex.GetType().Name + ": " + ex.Message);
}
finally
{
    if (manager is not null) manager.SessionsChanged -= ManagerChanged;
    foreach (var session in tracked.Keys) Detach(session);
    while (events.TryDequeue(out var row))
    {
        if (report.Events.Count < MaxEvents) report.Events.Add(row);
    }
    report.DroppedEvents = droppedEventCount;
    while (marks.TryDequeue(out var mark))
        if (report.Marks.Count < 300) report.Marks.Add(mark);
    report.EndedUtc = DateTimeOffset.UtcNow;
    foreach (var item in report.Sessions) item.Statistics = Statistics.Calculate(item.Samples);
    var json = Path.Combine(root, "spotify-gsmtc-report.json");
    await File.WriteAllTextAsync(json, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    var summary = Path.Combine(root, "spotify-gsmtc-summary.txt");
    await File.WriteAllLinesAsync(summary, [
        "GSMTC observation only; no Spotify support or viability conclusion.",
        $"Start: {started:O}; duration: {stopwatch.Elapsed.TotalSeconds:F1}s",
        $"Sessions: {report.Sessions.Count}; events: {report.Events.Count}; markers: {report.Marks.Count}",
        ..report.Sessions.Select(s => $"{s.Id}: {s.Source}; samples {s.Samples.Count}; changes {s.Changes.Count}; playing positions {s.Statistics?.UniquePlayingPositions}; median update {s.Statistics?.MedianPositionUpdateIntervalSeconds:F3}s; p95 {s.Statistics?.P95PositionUpdateIntervalSeconds:F3}s"),
        $"Report: {json}"
    ]);
    Console.WriteLine($"Report: {json}");
}

void AddEvent(string name, string? id, string? source) =>
    EnqueueEvent(new(name, id, source, DateTimeOffset.UtcNow, stopwatch.Elapsed.TotalSeconds));
void EnqueueEvent(EventRow row)
{
    if (Interlocked.Increment(ref eventCount) <= MaxEvents) events.Enqueue(row);
    else Interlocked.Increment(ref droppedEventCount);
}
void ManagerChanged(GlobalSystemMediaTransportControlsSessionManager _, SessionsChangedEventArgs __) =>
    AddEvent("SessionsChanged", null, null);
void MediaChanged(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs _) =>
    AddEvent("MediaPropertiesChanged", tracked.TryGetValue(s, out var item) ? item.Id : null, s.SourceAppUserModelId);
void PlaybackChanged(GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs _) =>
    AddEvent("PlaybackInfoChanged", tracked.TryGetValue(s, out var item) ? item.Id : null, s.SourceAppUserModelId);
void TimelineChanged(GlobalSystemMediaTransportControlsSession s, TimelinePropertiesChangedEventArgs _) =>
    AddEvent("TimelinePropertiesChanged", tracked.TryGetValue(s, out var item) ? item.Id : null, s.SourceAppUserModelId);
void Attach(GlobalSystemMediaTransportControlsSession s)
{
    s.MediaPropertiesChanged += MediaChanged;
    s.PlaybackInfoChanged += PlaybackChanged;
    s.TimelinePropertiesChanged += TimelineChanged;
}
void Detach(GlobalSystemMediaTransportControlsSession s)
{
    s.MediaPropertiesChanged -= MediaChanged;
    s.PlaybackInfoChanged -= PlaybackChanged;
    s.TimelinePropertiesChanged -= TimelineChanged;
}

static async Task<bool> Exercise(GlobalSystemMediaTransportControlsSession s, string command,
    GlobalSystemMediaTransportControlsSessionPlaybackInfo info) => command switch
{
    "play" => await s.TryPlayAsync(), "pause" => await s.TryPauseAsync(),
    "next" => await s.TrySkipNextAsync(), "previous" => await s.TrySkipPreviousAsync(),
    "shuffle" => await s.TryChangeShuffleActiveAsync(!info.IsShuffleActive!.Value),
    "repeat" => await s.TryChangeAutoRepeatModeAsync(info.AutoRepeatMode!.Value switch
    { MediaPlaybackAutoRepeatMode.None => MediaPlaybackAutoRepeatMode.List,
      MediaPlaybackAutoRepeatMode.List => MediaPlaybackAutoRepeatMode.Track,
      _ => MediaPlaybackAutoRepeatMode.None }), _ => false
};

static async Task<ArtworkRow> CheckArtwork(Windows.Storage.Streams.IRandomAccessStreamReference thumbnail)
{
    try
    {
        using var stream = await thumbnail.OpenReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        var size = stream.Size;
        if (size > MaxArtworkBytes) return new(true, false, stream.ContentType, null, "Exceeds 1 MiB bound");
        // No image or bytes are retained. Confirm that the bounded stream can actually be read.
        using var input = stream.AsStreamForRead();
        var buffer = new byte[8192];
        long read = 0;
        int count;
        while ((count = await input.ReadAsync(buffer)) > 0)
        {
            read += count;
            if (read > MaxArtworkBytes) return new(true, false, stream.ContentType, null, "Exceeds 1 MiB bound");
        }
        stream.Seek(0);
        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        return new(true, read > 0 && decoder.PixelWidth is > 0 and <= 4096 &&
            decoder.PixelHeight is > 0 and <= 4096, stream.ContentType, read, null);
    }
    catch (Exception ex) when (ex is not OutOfMemoryException) { return new(true, false, null, null, ex.GetType().Name); }
}
static string Commit()
{
    try
    {
        using var p = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
        if (p is null || !p.WaitForExit(1500) || p.ExitCode != 0) return "unavailable";
        return p.StandardOutput.ReadToEnd().Trim();
    }
    catch { return "unavailable"; }
}
