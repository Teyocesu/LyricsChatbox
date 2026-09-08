using System.Text.Json;

namespace LyricsChatbox.Tests;

public sealed class MessageLayoutTests
{
    [Fact]
    public void LeftPreservesAsciiIndentationAndBlankLinesThroughComposerAndFinalPayload()
    {
        const string text = " /\\_/\\\n\n( o.o )\n > ^ <";
        var rendered = ChatboxComposer.Compose(text, null, "", "", DateTimeOffset.UnixEpoch, null, true);
        Assert.Equal(text, rendered);
        Assert.Equal(text, MessageLayout.Align(rendered, "Left"));
        Assert.Equal(text, ChatboxFormatter.Visible(ChatboxFormatter.Format(rendered, true, true)));
        Assert.Equal("", ChatboxFormatter.Format("  \n  ", true, true));
    }

    [Theory]
    [InlineData("Center", 11, 10)]
    [InlineData("Right", 23, 21)]
    public void AlignmentProducesRealPayloadSpaces(string alignment, int firstPadding, int secondPadding)
    {
        var aligned = MessageLayout.Align("A\nABC", alignment);
        Assert.Equal(new string(' ', firstPadding) + "A\n" + new string(' ', secondPadding) + "ABC", aligned);
        Assert.Equal(aligned, ChatboxFormatter.Format(aligned, preserveLayout: true));
    }

    [Fact]
    public void OrdinaryLyricsKeepTheirPreviousWhitespaceBehavior()
    {
        Assert.Equal("hello", ChatboxFormatter.Format("  hello  "));
        Assert.Equal("first\nsecond", ChatboxComposer.Compose("  first  \n\n second ", null, "", "", DateTimeOffset.UnixEpoch, null));
    }

    [Fact]
    public void AlignedUnicodeStillRespectsCompactGraphemeAndLineBudgets()
    {
        var input = string.Join("\n", Enumerable.Repeat("👨‍👩‍👧‍👦 日本語", 12));
        var payload = ChatboxFormatter.Format(MessageLayout.Align(input, "Center"), true, true);
        Assert.True(payload.Length <= 144);
        Assert.EndsWith(ChatboxFormatter.CompactSuffix, payload);
        var visible = ChatboxFormatter.Visible(payload);
        Assert.True(visible.Count(c => c == '\n') <= 8);
        Assert.False(char.IsHighSurrogate(visible[^1]));
        Assert.False(visible.EndsWith("\u200d", StringComparison.Ordinal));
    }

    [Fact]
    public void SchedulerPreservesLayoutWithoutChangingPacingAndClearsRemainEmpty()
    {
        var scheduler = new ChatboxScheduler();
        scheduler.Set(1, "A", true, preserveLayout: true);
        Assert.Equal("A", scheduler.Take(0)!.Value.Text);
        var centered = MessageLayout.Align("A", "Center");
        scheduler.Set(1, centered, true, preserveLayout: true);
        Assert.Null(scheduler.Take(1));
        Assert.Equal(centered, scheduler.Take(1.05)!.Value.Text);
        scheduler.Set(1, "  ", true, compact: true, preserveLayout: true);
        Assert.Equal("", scheduler.Take(2.1)!.Value.Text);
    }

    [Fact]
    public void OldSettingsKeepLeftAlignmentAndUnknownValuesAreRejected()
    {
        var old = JsonSerializer.Deserialize<AppSettings>("{\"Enabled\":true,\"Preset\":\"Custom\",\"Compact\":true}")!;
        Assert.Equal("Left", old.CustomAlignment);
        Assert.Equal("Left", old.ManualAlignment);
        Assert.True(old.IsValid);
        Assert.False((old with { CustomAlignment = "<center>" }).IsValid);
        Assert.True((old with { CustomAlignment = "Center", ManualAlignment = "Right" }).IsValid);
    }

    [Fact]
    public void EveryAsciiTemplateIsEditableAsciiAndFitsWithoutFormatting()
    {
        foreach (var template in MessageLayout.Templates)
        {
            Assert.All(template.Custom + template.Manual, c => Assert.True(c is >= ' ' and <= '~' or '\n'));
            Assert.True(template.Manual.Length <= 142);
            Assert.True(template.Manual.Count(c => c == '\n') <= 8);
            Assert.Equal(template.Manual, ChatboxFormatter.Visible(ChatboxFormatter.Format(template.Manual, true, true)));
        }
    }
}
