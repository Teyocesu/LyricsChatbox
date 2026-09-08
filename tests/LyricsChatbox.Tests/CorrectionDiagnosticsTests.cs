using System.Text.Json;

namespace LyricsChatbox.Tests;

public sealed class CorrectionDiagnosticsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "LyricsChatbox.Tests", Guid.NewGuid().ToString("N"));
    [Fact]
    public void CorrectionsAreExactRecordingBoundedAndResettableWithoutChangingSettings()
    {
        var data = new LocalData(root); var track = CoreTests.Track;
        data.SaveSettings(new(Offset: 0.2));
        Assert.Null(data.ReadCorrection(track)); Assert.True(data.SaveCorrection(track, 0.7));
        Assert.Equal(0.7, new LocalData(root).ReadCorrection(track));
        Assert.Null(data.ReadCorrection(track with { Title = track.Title + " (Live)" }));
        Assert.Null(data.ReadCorrection(track with { Artist = "Someone else" }));
        Assert.Null(data.ReadCorrection(track with { Duration = track.Duration + 5 }));
        Assert.Equal(0.2, data.ReadSettings().Offset);
        Assert.False(data.SaveCorrection(track, 300)); Assert.False(data.SaveCorrection(track, double.NaN));
        Assert.Equal(0.7, data.ReadCorrection(track));
        Assert.True(data.ResetCorrection(track)); Assert.Null(data.ReadCorrection(track));
        File.WriteAllText(Path.Combine(root, "corrections", track.Key + ".json"), "{bad json");
        Assert.Null(data.ReadCorrection(track));
    }
    [Fact]
    public void DiagnosticsWhitelistExcludesDraftTemplateSecretsAndBoundsHistory()
    {
        var diagnostics = new Diagnostics();
        for (var i = 0; i < 150; i++) diagnostics.Add(DiagnosticCategory.Lifecycle, "Event " + i);
        diagnostics.Add(DiagnosticCategory.Update, "token=super-secret Bearer another-secret ghp_exampleSecret");
        var settings = new AppSettings(CustomTemplate: "private lyric body", Message: "private message");
        var text = diagnostics.Export(null, settings, "Synced · NetEase", "OSC ready", 0.6);
        Assert.DoesNotContain("private lyric body", text); Assert.DoesNotContain("private message", text);
        Assert.DoesNotContain("super-secret", text); Assert.DoesNotContain("another-secret", text); Assert.DoesNotContain("ghp_exampleSecret", text);
        using var document = JsonDocument.Parse(text);
        Assert.Equal(100, document.RootElement.GetProperty("RecentEvents").GetArrayLength());
        Assert.Equal(0.6, document.RootElement.GetProperty("Lyrics").GetProperty("EffectiveOffset").GetDouble());
        Assert.Equal("LyricsChatbox", document.RootElement.GetProperty("Application").GetString());
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
