namespace LyricsChatbox.Tests;

public class PresentationTextTests
{
    [Theory]
    [InlineData(3599, "59:59")]
    [InlineData(3600, "1:00:00")]
    public void SharedDurationFormattingHandlesHourBoundary(double seconds, string expected)
    {
        Assert.Equal(expected, DurationFormatter.Format(seconds));
        Assert.Contains(expected, new ManualCandidate("Source", new(1, "Song", "Artist", "Album", seconds, false, null)).Display);
        Assert.Equal(expected, ChatboxComposer.Time(seconds));
    }

    [Theory]
    [InlineData("No Apple Music session", "Apple Music · not detected")]
    [InlineData("Multiple Apple Music sessions · waiting", "Apple Music · multiple sessions open")]
    [InlineData("Apple Music unavailable · reconnecting", "Apple Music · reconnecting")]
    [InlineData("Checking Apple Music session", "Apple Music · reconnecting")]
    [InlineData("unexpected backend value", "Apple Music · not detected")]
    public void AppleMusicNullSnapshotStatesAreFriendly(string status, string expected)
        => Assert.Equal(expected, PresentationText.AppleMusicStatus(null, status));

    [Theory]
    [InlineData(LyricsOutcome.Found, true, false)]
    [InlineData(LyricsOutcome.Instrumental, false, false)]
    [InlineData(LyricsOutcome.NotFound, false, true)]
    [InlineData(LyricsOutcome.Ambiguous, false, true)]
    [InlineData(LyricsOutcome.Rejected, false, true)]
    [InlineData(LyricsOutcome.Timeout, false, true)]
    [InlineData(LyricsOutcome.Unavailable, false, true)]
    [InlineData(LyricsOutcome.RateLimited, false, true)]
    public void RecoveryAppearsOnlyForUsefulOutcomes(LyricsOutcome outcome, bool timeline, bool visible)
    {
        var state = PresentationText.Recovery(true, timeline, false, outcome, false);
        Assert.Equal(visible, state.Visible);
        Assert.DoesNotContain("RateLimited", state.Title + state.Hint);
        Assert.DoesNotContain("NotFound", state.Title + state.Hint);
    }

    [Fact]
    public void RecoveryHandlesLookupAndIgnoredRecording()
    {
        Assert.False(PresentationText.Recovery(true, false, false, LyricsOutcome.NotFound, true).Visible);
        var ignored = PresentationText.Recovery(true, false, true, LyricsOutcome.Ignored, false);
        Assert.True(ignored.Visible); Assert.True(ignored.Resume); Assert.False(ignored.Retry || ignored.Search || ignored.Import || ignored.Ignore);
    }

    [Fact]
    public void EmptyPreviewIsOnlyAPlaceholder()
    {
        var empty = PresentationText.Preview("");
        Assert.Equal("Nothing to send", empty.Text); Assert.True(empty.IsPlaceholder);
        var payload = PresentationText.Preview("Nothing to send");
        Assert.Equal("Nothing to send", payload.Text); Assert.False(payload.IsPlaceholder);
        Assert.Equal(0, ChatboxFormatter.Format("", false).Length);
    }

    [Fact]
    public void ReleaseMarkdownBecomesBoundedSafePlainText()
    {
        var markdown = "# Update\n\n### Improved\n\n- **Clearer** notes with [details](https://example.com/release).\n* Another item\nMalformed [link](oops and **marker";
        var plain = PresentationText.ReleaseNotes(markdown);
        Assert.Contains("Update", plain); Assert.Contains("Improved", plain);
        Assert.Contains("• Clearer notes with details (https://example.com/release).", plain);
        Assert.Contains("• Another item", plain);
        Assert.Contains("Malformed [link](oops and marker", plain);
        Assert.DoesNotContain("###", plain); Assert.DoesNotContain("**", plain);
        Assert.Equal(80, PresentationText.ReleaseNotes(new string('x', 200), 80).Length);
    }
}
