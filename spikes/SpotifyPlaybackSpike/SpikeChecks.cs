internal static class SpikeChecks
{
    public static void Run()
    {
        var now = DateTimeOffset.UtcNow;
        var samples = new[]
        {
            Row(0, 0, "Playing", "Song A"), Row(.25, 0, "Playing", "Song A"),
            Row(.5, 1, "Playing", "Song A"), Row(.75, 1, "Playing", "Song A"),
            Row(1, 2, "Playing", "Song A"), Row(1.25, 2, "Paused", "Song A"),
            Row(1.5, 0, "Playing", "Song B")
        };
        var stats = Statistics.Calculate(samples);
        Assert(stats.PlayingSamples == 5 && stats.UniquePlayingPositions == 3);
        Assert(stats.ContiguousPlayingSegmentDurationSeconds == 1);
        Assert(stats.MinimumPositivePositionStepSeconds == 1);
        Assert(stats.MedianPositionUpdateIntervalSeconds == .5);
        Assert(Statistics.Calculate([]).MedianPositionUpdateIntervalSeconds is null);
        Assert(Options.Parse(["--duration", "60", "--interactive"])?.Duration == 60);
        Assert(Options.Parse(["--auto-validate"])?.AutoValidate == true);
        try { Options.Parse(["--exercise", "next"]); throw new Exception("Exercise without source accepted"); }
        catch (ArgumentException) { }
        return;

        SampleRow Row(double mono, double position, string state, string title) =>
            new(now.AddSeconds(mono), mono, state, 1, 0, 180, position,
                now.AddSeconds(mono), 0, title, "Artist");
    }
    private static void Assert(bool condition)
    {
        if (!condition) throw new Exception("Spike statistical/option invariant failed");
    }
}
