internal sealed record Options(int Duration, bool Interactive, string? Source, string? Exercise)
{
    public static Options? Parse(string[] args)
    {
        if (args.Contains("--help"))
        {
            Console.WriteLine("Usage: dotnet run --project spikes/SpotifyPlaybackSpike -c Release -- --duration 90 [--interactive]");
            Console.WriteLine("Optional opt-in: --source <observed exact SourceAppUserModelId> --exercise play|pause|next|previous|shuffle|repeat");
            Console.WriteLine("No seek, lyrics, OSC, production LocalData, or network requests. Default is observation-only.");
            return null;
        }
        int duration = 90;
        bool interactive = false;
        string? source = null, exercise = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--duration" when i + 1 < args.Length && int.TryParse(args[++i], out var seconds) && seconds is >= 5 and <= 1800:
                    duration = seconds; break;
                case "--interactive": interactive = true; break;
                case "--source" when i + 1 < args.Length: source = args[++i]; break;
                case "--exercise" when i + 1 < args.Length: exercise = args[++i].ToLowerInvariant(); break;
                default: throw new ArgumentException("Invalid argument. Use --help."); 
            }
        }
        if (exercise is not null && (source is null || exercise is not ("play" or "pause" or "next" or "previous" or "shuffle" or "repeat")))
            throw new ArgumentException("Exercise requires an observed exact --source and one supported transport command.");
        return new(duration, interactive, source, exercise);
    }
}

internal sealed class Report(string windowsVersion, string repositoryCommit, DateTimeOffset startedUtc,
    int requestedDurationSeconds, string? requestedSource, string? requestedExercise)
{
    public string SpikeVersion { get; } = "phase0-1";
    public string WindowsVersion { get; } = windowsVersion;
    public string RepositoryCommit { get; } = repositoryCommit;
    public DateTimeOffset StartedUtc { get; } = startedUtc;
    public DateTimeOffset EndedUtc { get; set; }
    public int RequestedDurationSeconds { get; } = requestedDurationSeconds;
    public string? RequestedSource { get; } = requestedSource;
    public string? RequestedExercise { get; } = requestedExercise;
    public List<TrackedSession> Sessions { get; } = [];
    public List<EventRow> Events { get; } = [];
    public List<MarkRow> Marks { get; } = [];
    public List<ExerciseRow> Exercises { get; } = [];
    public List<string> Errors { get; } = [];
    public int DroppedEvents { get; set; }
    public string VolumeSessionObservation { get; } = "Not tested (read-only audio-session investigation deferred)";
    public string FreeAdObservation { get; } = "Not classified; inspect raw metadata if naturally encountered";
}

internal sealed class TrackedSession(string id, string source, double appearedMono)
{
    public string Id { get; } = id; // Per-run instance identity; Windows GSMTC exposes no separate session GUID.
    public string Source { get; } = source;
    public double AppearedMono { get; } = appearedMono;
    public double? EndedMono { get; set; }
    public List<SampleRow> Samples { get; } = [];
    public List<MetadataRow> Changes { get; } = [];
    public List<ArtworkRow> Artwork { get; } = [];
    public CapabilityRow? Controls { get; set; }
    public List<string> Errors { get; } = [];
    public StatsRow? Statistics { get; set; }
    public int DroppedSamples { get; set; }
    public int DroppedChanges { get; set; }
    public int DroppedErrors { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public string? LastMetadataKey { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public string? LastControlsStatus { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public int ArtworkChecks { get; set; }
}
internal sealed record SampleRow(DateTimeOffset ObservedUtc, double ObservedMono, string PlaybackStatus, double? PlaybackRate,
    double StartSeconds, double EndSeconds, double PositionSeconds, DateTimeOffset LastUpdatedUtc, double LastUpdatedAgeSeconds,
    string Title, string Artist);
internal sealed record MetadataRow(DateTimeOffset ObservedUtc, double ObservedMono, string Title, string Artist, string Album,
    int TrackNumber, string PlaybackType, string PlaybackStatus, double StartSeconds, double EndSeconds, bool ThumbnailPresent);
internal sealed record EventRow(string Name, string? SessionId, string? Source, DateTimeOffset ObservedUtc, double ObservedMono);
internal sealed record MarkRow(string Label, DateTimeOffset ObservedUtc, double ObservedMono);
internal sealed record ExerciseRow(string Command, string Source, bool Advertised, bool? GsmtcReturn,
    string? Error, DateTimeOffset RequestedUtc, double RequestedMono);
internal sealed record ArtworkRow(bool Present, bool DecodeSucceeded, string? ContentType, long? BoundedBytes, string? Error);
internal sealed record CapabilityRow(bool Play, bool Pause, bool PlayPauseToggle, bool Previous, bool Next,
    bool Shuffle, bool Repeat, bool PlaybackPosition, bool? ShuffleActive, string? RepeatMode);
internal sealed record StatsRow(double ContiguousPlayingSegmentDurationSeconds, int PlayingSamples, int UniquePlayingPositions, double? MinimumPositivePositionStepSeconds,
    double? MedianPositivePositionStepSeconds, double? MedianPositionUpdateIntervalSeconds,
    double? P95PositionUpdateIntervalSeconds, double? MaximumObservedLastUpdatedAgeSeconds,
    double? MedianObservedLastUpdatedAgeSeconds, int PositionRegressionsWhilePlaying, int TimestampRegressionsWhilePlaying);

internal static class Statistics
{
    // Longest contiguous same-recording playing segment; not a viability threshold.
    public static StatsRow Calculate(IReadOnlyList<SampleRow> samples)
    {
        var segments = new List<List<SampleRow>>();
        List<SampleRow>? current = null;
        foreach (var s in samples)
        {
            if (s.PlaybackStatus != "Playing" || !double.IsFinite(s.PositionSeconds) || s.EndSeconds <= s.StartSeconds)
            { current = null; continue; }
            if (current is null || current[0].Title != s.Title || current[0].Artist != s.Artist ||
                current[0].StartSeconds != s.StartSeconds || current[0].EndSeconds != s.EndSeconds)
            { current = [s]; segments.Add(current); }
            else current.Add(s);
        }
        var playing = segments.OrderByDescending(s => s.Count).FirstOrDefault()?.ToArray() ?? [];
        var segmentDuration = playing.Length > 1 ? playing[^1].ObservedMono - playing[0].ObservedMono : 0;
        var positions = playing.Select(s => s.PositionSeconds).Distinct().Order().ToArray();
        var steps = positions.Zip(positions.Skip(1), (a, b) => b - a).Where(x => x > 0).Order().ToArray();
        var changes = new List<double>();
        int regressions = 0, timestampRegressions = 0;
        for (var i = 1; i < playing.Length; i++)
        {
            var prev = playing[i - 1]; var next = playing[i];
            if (next.PositionSeconds < prev.PositionSeconds) regressions++;
            if (next.LastUpdatedUtc < prev.LastUpdatedUtc) timestampRegressions++;
            if (next.PositionSeconds != prev.PositionSeconds && next.PositionSeconds >= prev.PositionSeconds)
                changes.Add(next.ObservedMono);
        }
        var intervals = changes.Zip(changes.Skip(1), (a, b) => b - a).Where(x => x >= 0).Order().ToArray();
        var observedAges = playing.Where(s => double.IsFinite(s.LastUpdatedAgeSeconds))
            .Select(s => s.LastUpdatedAgeSeconds).Order().ToArray();
        return new(segmentDuration, playing.Length, positions.Length, steps.Length > 0 ? steps[0] : null,
            Quantile(steps, .5), Quantile(intervals, .5), Quantile(intervals, .95),
            observedAges.Length > 0 ? observedAges[^1] : null,
            Quantile(observedAges, .5), regressions, timestampRegressions);
    }
    private static double? Quantile(double[] sorted, double p)
    {
        if (sorted.Length == 0) return null;
        var index = (sorted.Length - 1) * p;
        var lower = (int)Math.Floor(index);
        return sorted[lower] + (sorted[Math.Min(lower + 1, sorted.Length - 1)] - sorted[lower]) * (index - lower);
    }
}
