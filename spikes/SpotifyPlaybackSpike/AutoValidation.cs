// Explicit test-only orchestration. Production has no reference to this project or seek path.
using System.Diagnostics;
using System.Text.Json;
using Windows.Media.Control;

internal sealed class AutoValidation
{
    private const int MaxEvents = 2000;
    private const int MaxSamplesPerSession = 6000;
    private const string PublicBootstrapUri = "spotify:track:6rqhFgbbKwnb9MLmUQDhG6";
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Report report;
    private readonly string root;
    private readonly Dictionary<GlobalSystemMediaTransportControlsSession, Subscription> tracked =
        new(ReferenceEqualityComparer.Instance);
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private readonly DateTimeOffset started = DateTimeOffset.UtcNow;
    private AutoValidation()
    {
        root = Path.GetFullPath(Path.Combine("artifacts", "spikes", "spotify", started.ToString("yyyyMMddTHHmmssfffZ")));
        Directory.CreateDirectory(root);
        report = new(Environment.OSVersion.VersionString, Commit(), started, 0, null, "auto-validate");
    }
    public static async Task RunAsync() => await new AutoValidation().Run();
    private async Task Run()
    {
        try
        {
            Mark("auto-start");
            var running = Process.GetProcessesByName("Spotify").Length;
            Note("spotify-launch", null, null, null, "existing Spotify processes: " + running);
            if (running == 0) Launch("spotify:");
            manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            manager.SessionsChanged += SessionsChanged;
            await PumpFor(TimeSpan.FromSeconds(15));
            if (Spotify() is null)
            {
                // Official desktop navigation URI; no Web API, OAuth, browser or private Spotify command.
                Mark("bootstrap-track-uri");
                Launch(PublicBootstrapUri);
                await PumpFor(TimeSpan.FromSeconds(20));
            }
            var first = Spotify();
            if (first is null)
            {
                report.VolumeSessionObservation = SpotifyAudioSessions.ReadOnlySummary();
                Note("volume-read-only", null, null, null, report.VolumeSessionObservation);
                report.AutoOutcome = "No Spotify GSMTC session appeared after app launch and documented URI navigation";
                Note("bootstrap-result", null, null, null, report.AutoOutcome);
                return;
            }
            report.SpotifyIdentifier = first.Value.Session.SourceAppUserModelId;
            Note("spotify-identified", null, null, null, report.SpotifyIdentifier);
            if (first.Value.Info.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            {
                await Command("auto-initial-play", first.Value.Session, first.Value.Info.Controls.IsPlayEnabled,
                    s => s.TryPlayAsync().AsTask(), "Playing", TimeSpan.FromSeconds(10));
                if (Spotify()?.Info.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                {
                    report.AutoOutcome = "GSMTC session exists but playing state could not be established";
                    return;
                }
            }
            var ready = Spotify();
            if (ready is not null && ready.Value.Last.EndSeconds - ready.Value.Last.PositionSeconds < 75 &&
                ready.Value.Info.Controls.IsNextEnabled)
                await Transition("auto-prebaseline-next", ready.Value.Session, true, s => s.TrySkipNextAsync().AsTask());
            await Baseline("baseline");
            if (report.BaselineHealthy != true && Spotify() is { } retry &&
                retry.Last.PlaybackStatus == "Playing" && retry.Last.EndSeconds - retry.Last.PositionSeconds > 70)
                await Baseline("baseline-retry");

            var current = Spotify();
            if (current is not null)
            {
                await Command("auto-pause", current.Value.Session, current.Value.Info.Controls.IsPauseEnabled,
                    s => s.TryPauseAsync().AsTask(), "Paused", TimeSpan.FromSeconds(8));
                await PumpFor(TimeSpan.FromSeconds(5));
                current = Spotify();
                if (current is not null) await Command("auto-resume", current.Value.Session, current.Value.Info.Controls.IsPlayEnabled,
                    s => s.TryPlayAsync().AsTask(), "Playing", TimeSpan.FromSeconds(8));
                current = Spotify();
                if (current is not null) await Command("auto-long-pause", current.Value.Session, current.Value.Info.Controls.IsPauseEnabled,
                    s => s.TryPauseAsync().AsTask(), "Paused", TimeSpan.FromSeconds(8));
                await PumpFor(TimeSpan.FromSeconds(15));
                current = Spotify();
                if (current is not null) await Command("auto-long-resume", current.Value.Session, current.Value.Info.Controls.IsPlayEnabled,
                    s => s.TryPlayAsync().AsTask(), "Playing", TimeSpan.FromSeconds(8));
            }
            current = Spotify();
            if (current is not null)
                await Transition("auto-next", current.Value.Session, current.Value.Info.Controls.IsNextEnabled, s => s.TrySkipNextAsync().AsTask());
            await PumpFor(TimeSpan.FromSeconds(3));
            current = Spotify();
            if (current is not null)
                await Transition("auto-previous", current.Value.Session, current.Value.Info.Controls.IsPreviousEnabled, s => s.TrySkipPreviousAsync().AsTask());
            await PumpFor(TimeSpan.FromSeconds(3));
            await PositionChange("auto-scrub-forward", 20);
            await PumpFor(TimeSpan.FromSeconds(3));
            await PositionChange("auto-scrub-backward", -10);
            report.VolumeSessionObservation = SpotifyAudioSessions.ReadOnlySummary();
            Note("volume-read-only", null, null, null, report.VolumeSessionObservation);
            await Save(); // Flush main evidence before lifecycle exercise.
            await Lifecycle();
            await Coexistence();
            report.AutoOutcome ??= "Automated exercises finished; inspect observed steps and raw timeline";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            report.Errors.Add(ex.GetType().Name + ": " + ex.Message);
            report.AutoOutcome ??= "Runner error; physical evidence incomplete";
        }
        finally
        {
            if (manager is not null) manager.SessionsChanged -= SessionsChanged;
            foreach (var pair in tracked) pair.Value.Detach(pair.Key);
            await Save();
            Console.WriteLine("Report: " + Path.Combine(root, "spotify-gsmtc-report.json"));
        }
    }
    private async Task PumpFor(TimeSpan duration)
    {
        var end = clock.Elapsed + duration;
        while (clock.Elapsed < end)
        {
            var tick = clock.Elapsed;
            await Pump();
            var remaining = TimeSpan.FromMilliseconds(225) - (clock.Elapsed - tick);
            if (remaining > TimeSpan.Zero) await Task.Delay(remaining);
        }
    }
    private async Task Baseline(string label)
    {
        var selected = Spotify();
        if (selected is null) { Note(label, false, null, null, "No unique Spotify session"); return; }
        var begin = clock.Elapsed.TotalSeconds;
        var first = selected.Value.Last;
        Mark(label + "-start");
        await PumpFor(TimeSpan.FromSeconds(62));
        var end = clock.Elapsed.TotalSeconds;
        Mark(label + "-end");
        var samples = selected.Value.Item.Samples.Where(s => s.ObservedMono >= begin && s.ObservedMono <= end).ToArray();
        var healthy = samples.Length > 1 && end - begin >= 60 && !string.IsNullOrWhiteSpace(first.Title) &&
            !string.IsNullOrWhiteSpace(first.Artist) && first.EndSeconds > first.StartSeconds && samples.All(s =>
            s.PlaybackStatus == "Playing" && s.Title == first.Title && s.Artist == first.Artist &&
            s.StartSeconds == first.StartSeconds && s.EndSeconds == first.EndSeconds);
        report.BaselineHealthy = healthy;
        report.BaselineDurationSeconds = end - begin;
        report.BaselineStatistics = Statistics.Calculate(samples);
        report.BaselineTitle = first.Title;
        Note(label + "-result", null, null, null,
            $"healthy={healthy}; duration={end - begin:F3}s; samples={samples.Length}; unique positions={report.BaselineStatistics.UniquePlayingPositions}");
    }
    private async Task Pump()
    {
        if (manager is null) return;
        var allSessions = manager.GetSessions();
        var sessions = allSessions.Where(s => s.SourceAppUserModelId.Contains("Spotify", StringComparison.OrdinalIgnoreCase) ||
            s.SourceAppUserModelId.Contains("AppleMusic", StringComparison.OrdinalIgnoreCase)).ToArray();
        report.MaximumOtherGsmtcSessionsSeen = Math.Max(report.MaximumOtherGsmtcSessionsSeen, allSessions.Count - sessions.Length);
        var present = new HashSet<GlobalSystemMediaTransportControlsSession>(sessions, ReferenceEqualityComparer.Instance);
        foreach (var pair in tracked.Where(p => !present.Contains(p.Key)).ToArray())
        {
            pair.Value.Item.EndedMono = clock.Elapsed.TotalSeconds;
            pair.Value.Detach(pair.Key);
            tracked.Remove(pair.Key);
            Event("SessionDisappeared", pair.Value.Item.Id, pair.Value.Item.Source);
        }
        foreach (var session in sessions)
        {
            if (!tracked.TryGetValue(session, out var sub))
            {
                if (report.Sessions.Count >= 32) { if (!report.Errors.Contains("Session bound exceeded")) report.Errors.Add("Session bound exceeded"); continue; }
                var item = new TrackedSession("session-" + (report.Sessions.Count + 1), session.SourceAppUserModelId, clock.Elapsed.TotalSeconds);
                report.Sessions.Add(item);
                sub = new Subscription(item, this);
                tracked.Add(session, sub);
                sub.Attach(session);
                Event("SessionAppeared", item.Id, item.Source);
            }
            try
            {
                var media = await session.TryGetMediaPropertiesAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
                var info = session.GetPlaybackInfo();
                var timeline = session.GetTimelineProperties();
                var now = DateTimeOffset.UtcNow;
                var mono = clock.Elapsed.TotalSeconds;
                var key = JsonSerializer.Serialize(new { media.Title, media.Artist, media.AlbumTitle, media.TrackNumber,
                    info.PlaybackStatus, timeline.StartTime, timeline.EndTime });
                if (sub.Item.LastMetadataKey != key)
                {
                    if (sub.Item.Changes.Count < 300)
                        sub.Item.Changes.Add(new(now, mono, media.Title, media.Artist, media.AlbumTitle, media.TrackNumber,
                            media.PlaybackType?.ToString() ?? "unavailable", info.PlaybackStatus.ToString(),
                            timeline.StartTime.TotalSeconds, timeline.EndTime.TotalSeconds, media.Thumbnail is not null));
                    else sub.Item.DroppedChanges++;
                    sub.Item.LastMetadataKey = key;
                    if (media.Thumbnail is not null && sub.Item.ArtworkChecks < 12)
                    {
                        sub.Item.ArtworkChecks++;
                        sub.Item.Artwork.Add(await Artwork.Check(media.Thumbnail));
                    }
                }
                var c = info.Controls;
                sub.Item.Controls = new(c.IsPlayEnabled, c.IsPauseEnabled, c.IsPlayPauseToggleEnabled,
                    c.IsPreviousEnabled, c.IsNextEnabled, c.IsShuffleEnabled, c.IsRepeatEnabled,
                    c.IsPlaybackPositionEnabled, info.IsShuffleActive, info.AutoRepeatMode?.ToString());
                sub.Info = info;
                sub.Last = new(now, mono, info.PlaybackStatus.ToString(), info.PlaybackRate,
                    timeline.StartTime.TotalSeconds, timeline.EndTime.TotalSeconds, timeline.Position.TotalSeconds,
                    timeline.LastUpdatedTime, (now - timeline.LastUpdatedTime).TotalSeconds, media.Title, media.Artist);
                if (sub.Item.Samples.Count < MaxSamplesPerSession) sub.Item.Samples.Add(sub.Last);
                else sub.Item.DroppedSamples++;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            { if (sub.Item.Errors.Count < 20) sub.Item.Errors.Add(ex.GetType().Name + ": " + ex.Message); else sub.Item.DroppedErrors++; }
        }
    }
    private (GlobalSystemMediaTransportControlsSession Session, TrackedSession Item,
        GlobalSystemMediaTransportControlsSessionPlaybackInfo Info, SampleRow Last)? Spotify()
    {
        var candidates = tracked.Where(p => p.Key.SourceAppUserModelId.Contains("Spotify", StringComparison.OrdinalIgnoreCase)
            && p.Value.Info is not null && p.Value.Last is not null).ToArray();
        if (candidates.Length != 1) return null;
        var p = candidates[0];
        return (p.Key, p.Value.Item, p.Value.Info!, p.Value.Last!);
    }
    private async Task Command(string name, GlobalSystemMediaTransportControlsSession session, bool enabled,
        Func<GlobalSystemMediaTransportControlsSession, Task<bool>> action, string expectedStatus, TimeSpan timeout)
    {
        var before = Spotify()?.Last;
        var at = clock.Elapsed.TotalSeconds; var utc = DateTimeOffset.UtcNow;
        Mark(name + "-request");
        bool? result = null; string? error = null;
        if (enabled)
        {
            try { result = await action(session).WaitAsync(TimeSpan.FromSeconds(3)); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { error = ex.GetType().Name + ": " + ex.Message; }
        }
        else error = "Capability not advertised";
        SampleRow? observed = null;
        var end = clock.Elapsed + timeout;
        while (clock.Elapsed < end)
        {
            await Pump();
            var current = Spotify();
            if (current?.Last.PlaybackStatus == expectedStatus)
            { observed = current.Value.Last; Mark(name + "-observed"); break; }
            await Task.Delay(225);
        }
        report.AutoSteps.Add(new(name, utc, at, enabled, result, expectedStatus, Describe(before),
            observed?.ObservedUtc, observed?.ObservedMono, Describe(observed), error ?? (observed is null ? "No observed state change" : null)));
    }
    private async Task Transition(string name, GlobalSystemMediaTransportControlsSession session, bool enabled,
        Func<GlobalSystemMediaTransportControlsSession, Task<bool>> action)
    {
        var before = Spotify()?.Last;
        var at = clock.Elapsed.TotalSeconds; var utc = DateTimeOffset.UtcNow;
        Mark(name + "-request");
        bool? result = null; string? error = null;
        if (enabled)
        {
            try { result = await action(session).WaitAsync(TimeSpan.FromSeconds(3)); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { error = ex.GetType().Name + ": " + ex.Message; }
        }
        else error = "Capability not advertised";
        SampleRow? observed = null;
        var end = clock.Elapsed + TimeSpan.FromSeconds(10);
        while (clock.Elapsed < end)
        {
            await Pump();
            var current = Spotify();
            if (current is not null && before is not null &&
                (current.Value.Last.Title != before.Title || current.Value.Last.Artist != before.Artist ||
                current.Value.Last.EndSeconds != before.EndSeconds ||
                current.Value.Last.PositionSeconds < before.PositionSeconds - .5))
            { observed = current.Value.Last; Mark(name + "-observed"); break; }
            await Task.Delay(225);
        }
        report.AutoSteps.Add(new(name, utc, at, enabled, result, "new recording/timeline", Describe(before),
            observed?.ObservedUtc, observed?.ObservedMono, Describe(observed),
            error ?? (observed is null ? "No observed transition within 10 s" : null)));
    }
    private async Task PositionChange(string name, double delta)
    {
        var current = Spotify();
        if (current is null) { Note(name, false, null, null, "No unique session"); return; }
        var before = current.Value.Last;
        var target = before.PositionSeconds + delta;
        var valid = current.Value.Info.Controls.IsPlaybackPositionEnabled &&
            before.EndSeconds > before.StartSeconds && target > before.StartSeconds + 5 &&
            target < before.EndSeconds - 15 && before.PlaybackStatus == "Playing";
        var at = clock.Elapsed.TotalSeconds; var utc = DateTimeOffset.UtcNow;
        Mark(name + "-request");
        bool? result = null; string? error = null;
        if (valid)
        {
            try { result = await current.Value.Session.TryChangePlaybackPositionAsync(TimeSpan.FromSeconds(target).Ticks)
                .AsTask().WaitAsync(TimeSpan.FromSeconds(3)); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { error = ex.GetType().Name + ": " + ex.Message; }
        }
        else error = "Invalid bounds/status or seek capability not advertised";
        SampleRow? observed = null;
        var end = clock.Elapsed + TimeSpan.FromSeconds(8);
        while (clock.Elapsed < end)
        {
            await Pump();
            var next = Spotify();
            if (next is not null &&
                next.Value.Last.Title == before.Title && next.Value.Last.Artist == before.Artist &&
                (delta > 0 ? next.Value.Last.PositionSeconds > before.PositionSeconds + 10 +
                    (clock.Elapsed.TotalSeconds - at) : next.Value.Last.PositionSeconds < before.PositionSeconds - 5))
            { observed = next.Value.Last; Mark(name + "-observed"); break; }
            await Task.Delay(225);
        }
        report.AutoSteps.Add(new(name, utc, at, valid, result, target.ToString("F3"), Describe(before),
            observed?.ObservedUtc, observed?.ObservedMono, Describe(observed),
            error ?? (observed is null ? "No physical position discontinuity observed" : null)));
    }
    private async Task Lifecycle()
    {
        var old = Spotify();
        if (old is null) { Note("spotify-close-reopen", false, null, null, "No unique session"); return; }
        var windows = Process.GetProcessesByName("Spotify").Where(p => p.MainWindowHandle != IntPtr.Zero).ToArray();
        if (windows.Length != 1) { Note("spotify-close", false, null, null, "Expected one Spotify main window; found " + windows.Length); return; }
        var at = clock.Elapsed.TotalSeconds; var utc = DateTimeOffset.UtcNow;
        Mark("auto-close-request");
        var accepted = windows[0].CloseMainWindow();
        await PumpFor(TimeSpan.FromSeconds(15));
        var gone = Spotify() is null;
        if (gone) Mark("auto-close-observed");
        report.AutoSteps.Add(new("auto-close", utc, at, true, accepted, "session disappears", Describe(old.Value.Last),
            gone ? DateTimeOffset.UtcNow : null, gone ? clock.Elapsed.TotalSeconds : null,
            gone ? "session disappeared" : "session remained", gone ? null : "Graceful window close did not remove GSMTC session"));
        Launch("spotify:");
        Mark("auto-reopen-request");
        await PumpFor(TimeSpan.FromSeconds(20));
        var reopened = Spotify();
        report.AutoSteps.Add(new("auto-reopen", DateTimeOffset.UtcNow, clock.Elapsed.TotalSeconds, null, null,
            "fresh session and state", old.Value.Session.SourceAppUserModelId,
            reopened is null ? null : DateTimeOffset.UtcNow, reopened is null ? null : clock.Elapsed.TotalSeconds,
            reopened is null ? null : reopened.Value.Session.SourceAppUserModelId + " " + Describe(reopened.Value.Last),
            reopened is null ? "Session not rediscovered" : null));
        if (reopened is not null)
        {
            Mark("auto-reopen-observed");
            if (reopened.Value.Info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused)
                await Command("auto-reopen-play", reopened.Value.Session, reopened.Value.Info.Controls.IsPlayEnabled,
                    s => s.TryPlayAsync().AsTask(), "Playing", TimeSpan.FromSeconds(8));
        }
    }
    private async Task Coexistence()
    {
        try
        {
            LaunchApp("AppleInc.AppleMusicWin_nzyj5cx40ttqa!App");
            Mark("auto-apple-open");
            await PumpFor(TimeSpan.FromSeconds(12));
            var apple = tracked.Where(p => p.Key.SourceAppUserModelId.Contains("AppleMusic", StringComparison.OrdinalIgnoreCase)
                && p.Value.Last is not null).ToArray();
            var spotify = Spotify();
            Note("apple-spotify-coexistence", null, null, null,
                "Spotify: " + (spotify is null ? "none" : spotify.Value.Session.SourceAppUserModelId + " " + spotify.Value.Last.PlaybackStatus) +
                "; Apple sessions: " + apple.Length + "; " +
                string.Join("; ", apple.Select(p => p.Key.SourceAppUserModelId + " " + p.Value.Last!.PlaybackStatus)));
            // Do not select Apple library content. Existing media item, if any, is observed only.
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { Note("apple-spotify-coexistence", null, null, null, ex.GetType().Name); }
    }
    private void SessionsChanged(GlobalSystemMediaTransportControlsSessionManager _, SessionsChangedEventArgs __) =>
        Event("SessionsChanged", null, null);
    private void Event(string name, string? id, string? source)
    {
        lock (report.Events)
        {
            if (report.Events.Count < MaxEvents)
                report.Events.Add(new(name, id, source, DateTimeOffset.UtcNow, clock.Elapsed.TotalSeconds));
            else report.DroppedEvents++;
        }
    }
    private void Mark(string label)
    {
        if (report.Marks.Count < 300) report.Marks.Add(new(label, DateTimeOffset.UtcNow, clock.Elapsed.TotalSeconds));
    }
    private void Note(string name, bool? advertised, bool? result, string? target, string? value)
    {
        if (report.AutoSteps.Count < 300)
            report.AutoSteps.Add(new(name, DateTimeOffset.UtcNow, clock.Elapsed.TotalSeconds, advertised, result,
                target, null, null, null, value, null));
    }
    private async Task Save()
    {
        report.EndedUtc = DateTimeOffset.UtcNow;
        foreach (var item in report.Sessions) item.Statistics = Statistics.Calculate(item.Samples);
        var json = Path.Combine(root, "spotify-gsmtc-report.json");
        await File.WriteAllTextAsync(json, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        await File.WriteAllLinesAsync(Path.Combine(root, "spotify-gsmtc-summary.txt"), [
            "Spotify GSMTC automated Phase 0 research; no production support implied.",
            "Outcome: " + report.AutoOutcome,
            "Observed Spotify source: " + (report.SpotifyIdentifier ?? "none"),
            "Sessions: " + report.Sessions.Count + "; events: " + report.Events.Count,
            "Report: " + json
        ]);
    }
    private static string? Describe(SampleRow? s) => s is null ? null :
        $"{s.Title} / {s.Artist}; {s.PlaybackStatus}; pos {s.PositionSeconds:F3}; updated {s.LastUpdatedUtc:O}; end {s.EndSeconds:F3}";
    private void Launch(string uri)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
            Note("desktop-uri", null, p is not null, uri, p is null ? "No process returned" : "Shell navigation requested");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { Note("desktop-uri", null, false, uri, ex.GetType().Name + ": " + ex.Message); }
    }
    private void LaunchApp(string id)
    {
        using var p = Process.Start(new ProcessStartInfo("explorer.exe", "shell:AppsFolder\\" + id) { UseShellExecute = true });
        Note("app-launch", null, p is not null, id, "Windows installed-app launch requested");
    }
    private static string Commit()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD")
                { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
            if (p is null || !p.WaitForExit(1500) || p.ExitCode != 0) return "unavailable";
            return p.StandardOutput.ReadToEnd().Trim();
        }
        catch { return "unavailable"; }
    }
    private sealed class Subscription(TrackedSession item, AutoValidation owner)
    {
        public TrackedSession Item { get; } = item;
        public GlobalSystemMediaTransportControlsSessionPlaybackInfo? Info { get; set; }
        public SampleRow? Last { get; set; }
        private void Media(GlobalSystemMediaTransportControlsSession _, MediaPropertiesChangedEventArgs __) =>
            owner.Event("MediaPropertiesChanged", Item.Id, Item.Source);
        private void Playback(GlobalSystemMediaTransportControlsSession _, PlaybackInfoChangedEventArgs __) =>
            owner.Event("PlaybackInfoChanged", Item.Id, Item.Source);
        private void Timeline(GlobalSystemMediaTransportControlsSession _, TimelinePropertiesChangedEventArgs __) =>
            owner.Event("TimelinePropertiesChanged", Item.Id, Item.Source);
        public void Attach(GlobalSystemMediaTransportControlsSession s)
        { s.MediaPropertiesChanged += Media; s.PlaybackInfoChanged += Playback; s.TimelinePropertiesChanged += Timeline; }
        public void Detach(GlobalSystemMediaTransportControlsSession s)
        { s.MediaPropertiesChanged -= Media; s.PlaybackInfoChanged -= Playback; s.TimelinePropertiesChanged -= Timeline; }
    }
}
