namespace LyricsChatbox.Tests;

public sealed class BugReportTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lyricschatbox-report-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    [Fact]
    public void ReportCarriesDiagnosticsOnlyWhenTheUserIncludedThem()
    {
        var without = BugReport.Compose("It froze", "Lyrics", "Play a song", "1.0.0", "Windows 11");
        var with = BugReport.Compose("It froze", "Lyrics", "Play a song", "1.0.0", "Windows 11", "{\"Application\":\"LyricsChatbox\"}");

        Assert.DoesNotContain("Diagnostics", without);
        Assert.Contains("## Diagnostics", with);
        Assert.Contains("{\"Application\":\"LyricsChatbox\"}", with);
        foreach (var section in new[] { "## What happened\n\nIt froze", "## What I expected\n\nLyrics", "## Steps to reproduce\n\nPlay a song",
                     "- LyricsChatbox 1.0.0\n- Windows 11", "## Screenshot" })
            Assert.Contains(section, with);
    }

    [Fact]
    public void AnswersAreCleanedBoundedAndMarkedWhenEmpty()
    {
        var report = BugReport.Compose(" a\r\nb\u0007 ", new string('x', BugReport.MaximumFieldLength + 50), "  ", "1.0.0", "Windows");

        Assert.Contains("## What happened\n\na\nb\n", report);
        Assert.Contains(new string('x', BugReport.MaximumFieldLength) + "\n", report);
        Assert.DoesNotContain(new string('x', BugReport.MaximumFieldLength + 1), report);
        Assert.Contains("## Steps to reproduce\n\n_Not provided_", report);
        Assert.DoesNotContain('\u0007', report);
        Assert.DoesNotContain('\r', report);
    }

    [Fact]
    public void SkipGuidePreferenceDefaultsOffForOlderSettingsAndRoundTrips()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "settings.json"), "{\"Enabled\":true,\"Port\":9000}");
        var data = new LocalData(root);
        var older = data.ReadSettings();
        Assert.True(older.Enabled);
        Assert.False(older.SkipReportGuide);

        Assert.True(data.SaveSettings(older with { SkipReportGuide = true }));
        Assert.True(data.ReadSettings().SkipReportGuide);
    }
}
