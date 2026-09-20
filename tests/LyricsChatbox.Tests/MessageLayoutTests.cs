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
        var packet = scheduler.Take(0)!.Value; Assert.Equal("A", packet.Text); scheduler.Complete(packet, true);
        var centered = MessageLayout.Align("A", "Center");
        scheduler.Set(1, centered, true, preserveLayout: true);
        Assert.Null(scheduler.Take(1));
        packet = scheduler.Take(1.05)!.Value; Assert.Equal(centered, packet.Text); scheduler.Complete(packet, true);
        scheduler.Set(1, "  ", true, compact: true, preserveLayout: true);
        packet = scheduler.Take(2.1)!.Value; Assert.Equal("", packet.Text); scheduler.Complete(packet, true);
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

}
