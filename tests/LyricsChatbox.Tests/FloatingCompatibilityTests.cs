using System.Text.Json;

namespace LyricsChatbox.Tests;

public sealed class FloatingCompatibilityTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "LyricsChatbox.Tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(false, false, false, 144)]
    [InlineData(true, false, false, 144)]
    [InlineData(false, true, false, 144)]
    [InlineData(true, true, true, 142)]
    public void PolicySeparatesSavedPreferenceFromEffectiveFloating(bool saved, bool available, bool effective, int limit)
    {
        Assert.Equal(effective, FloatingCompatibility.Effective(saved, available));
        Assert.Equal(limit, ChatboxFormatter.Analyze("x", FloatingCompatibility.Effective(saved, available)).Limit);
    }

    [Fact]
    public void CurrentPolicyIsUnavailableAndSummariesNeverImplyActiveFloating()
    {
        Assert.False(FloatingCompatibility.Available);
        Assert.False(FloatingCompatibility.Effective(true));
        Assert.Equal(" · Floating unavailable", FloatingCompatibility.SummaryLabel(true));
        Assert.Equal("", FloatingCompatibility.SummaryLabel(false));
        Assert.Equal(" · Floating", FloatingCompatibility.SummaryLabel(true, available: true));
    }

    [Fact]
    public void SavedPreferenceSendsFullNormalPayloadAndCleanClearWhileUnavailable()
    {
        var floating = FloatingCompatibility.Effective(savedPreference: true);
        var text = new string('m', 144);

        var scheduler = new ChatboxScheduler();
        scheduler.Set(1, text, true, floating);
        var packet = scheduler.Take(0)!.Value;
        Assert.Equal(text, packet.Text);

        scheduler.Complete(packet, true);
        scheduler.Set(2, "", true, floating);
        Assert.Equal("", scheduler.Take(ChatboxScheduler.IntervalSeconds)!.Value.Text);
    }

    [Fact]
    public void AvailablePolicyRestoresLegacyBudgetAndSuffixWithoutMigration()
    {
        var legacy = FloatingCompatibility.Effective(savedPreference: true, available: true);

        var payload = ChatboxFormatter.Format(new string('y', 143), legacy);

        Assert.Equal(new string('y', 142) + ChatboxFormatter.CompactSuffix, payload);
        Assert.Equal("", ChatboxFormatter.Format("  ", legacy));
    }

    [Fact]
    public void AdaptiveContextUsesTheUnitsLegacyFloatingWouldReserve()
    {
        string Compose(bool floating) => LyricContextComposer.ComposeProfile(new("", new string('c', 142), "n"), CoreTests.Track,
            "Lyrics Only", "{lyrics}", "", "Adaptive", floating, new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero));

        Assert.EndsWith("\nn", Compose(FloatingCompatibility.Effective(savedPreference: true)), StringComparison.Ordinal);
        Assert.Equal(new string('c', 142), Compose(FloatingCompatibility.Effective(savedPreference: true, available: true)));
    }

    [Fact]
    public void StoredPreferenceSurvivesProfileSaveSwitchUnrelatedEditAndRestart()
    {
        var data = new LocalData(root);
        var settings = new AppSettings(Compact: true);
        Assert.True(data.SaveSettings(settings));
        var library = data.ReadProfiles(settings);
        var minimal = library.Items.Single(profile => profile.Id == "minimal");
        Assert.True(minimal.Compact);

        var copy = library.Duplicate(minimal)!;
        Assert.True(copy.Selected.Compact);
        library = library.Select("minimal")!;

        var updatedSettings = settings with { CustomAlignment = "Right" };
        library = library.Save(library.Selected.UpdatePresentation(updatedSettings, library.Selected.ContextMode))!;
        Assert.True(library.Selected.Compact);
        Assert.True(data.SaveProfiles(library));
        Assert.True(data.SaveSettings(updatedSettings));

        var restarted = new LocalData(root);
        var restoredSettings = restarted.ReadSettings();
        var restoredProfiles = restarted.ReadProfiles(restoredSettings);
        Assert.True(restoredSettings.Compact);
        Assert.True(restoredProfiles.Items.Single(profile => profile.Id == "minimal").Compact);
        Assert.False(restoredProfiles.Select("lyrics")!.Selected.Compact);
        Assert.True(restoredProfiles.Select("minimal")!.Selected.Compact);
        Assert.Contains("Floating unavailable", restoredProfiles.Items.Single(profile => profile.Id == "minimal").Summary);
        Assert.DoesNotContain("Floating", restoredProfiles.Items.Single(profile => profile.Id == "lyrics").Summary);
        Assert.Equal(1, JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "profiles.json")))
            .RootElement.GetProperty("Version").GetInt32());
    }

    [Fact]
    public void DiagnosticsKeepsCompactAsStoredPreferenceAndReportsEffectiveState()
    {
        var settings = new AppSettings(Compact: true);
        using var report = JsonDocument.Parse(new Diagnostics().Export(null, settings, "", "", 0));
        var output = report.RootElement.GetProperty("Output");
        Assert.True(output.GetProperty("Compact").GetBoolean());
        Assert.False(output.GetProperty("FloatingEffective").GetBoolean());
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
