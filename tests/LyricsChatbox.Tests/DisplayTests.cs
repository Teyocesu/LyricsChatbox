using System.Text;
using System.Text.Json;

namespace LyricsChatbox.Tests;

public class DisplayTests
{
    [Theory]
    [InlineData("{lyrics}", "日本語 🎵")]
    [InlineData("{title} — {artist}", "Song — Artist")]
    [InlineData("{title} — {album}", "Song")]
    [InlineData("{album} | {artist}", "Artist")]
    [InlineData("{album}\n{unknown}\n{message}\n{time}", "Ready {title}\n21:07")]
    [InlineData("{elapsed} / {duration}", "0:10 / 2:00")]
    [InlineData("{lyrics}\n{album}", "日本語 🎵")]
    public void ComposerHandlesTokensMissingFieldsAndDoesNotInterpretInsertedText(string template, string expected)
    {
        Assert.Equal(expected, ChatboxComposer.Compose(template, CoreTests.Track with { Album = "" }, "日本語 🎵",
            "Ready {title}", new DateTimeOffset(2026, 9, 7, 21, 7, 0, TimeSpan.Zero), 10));
    }

    [Fact]
    public void DefaultsMigrationAndMalformedTemplatesAreSafe()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{\"Enabled\":true,\"Offset\":1.2,\"Host\":\"127.0.0.1\",\"Port\":9000}")!;
        Assert.True(settings.IsValid);
        Assert.True(settings.Enabled);
        Assert.Equal(1.2, settings.Offset);
        Assert.Equal("{lyrics}", ChatboxComposer.Template(settings.Preset, settings.CustomTemplate));
        Assert.False(settings.Compact || settings.LiveEdit || settings.TypingIndicator);
        Assert.False((settings with { CustomTemplate = null! }).IsValid);
        Assert.False((settings with { Preset = "invalid" }).IsValid);
        Assert.Equal("", ChatboxComposer.Compose(new string('x', 513), null, "", "", default, null));
        Assert.Equal("{broken", ChatboxComposer.Compose("{broken", null, "", "", default, null));
        var roundtrip = settings with { Preset = "Custom", CustomTemplate = "{message}", Message = "日本語", Compact = true, LiveEdit = true };
        Assert.Equal(roundtrip, JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(roundtrip)));
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("日本語")]
    [InlineData("👩‍🚀")]
    [InlineData("é")]
    public void CompactReservesActualBudgetAndPreservesGraphemes(string text)
    {
        Assert.Equal(text, ChatboxFormatter.Format(text));
        Assert.Equal(text, ChatboxFormatter.Visible(ChatboxFormatter.Format(text, true)));
        var longText = string.Concat(Enumerable.Repeat(text, 160));
        var payload = ChatboxFormatter.Format(longText, true);
        Assert.EndsWith(ChatboxFormatter.CompactSuffix, payload);
        Assert.InRange(payload.Length, 3, 144);
        var visible = ChatboxFormatter.Visible(payload);
        Assert.StartsWith(visible, longText);
        Assert.Contains(visible.Length, System.Globalization.StringInfo.ParseCombiningCharacters(longText));
        Assert.Equal(payload, new UTF8Encoding(false, true).GetString(new UTF8Encoding(false, true).GetBytes(payload)));
        Assert.Equal("", ChatboxFormatter.Format(" \n\u0003\u001F", true));
    }

    [Fact]
    public void AppearanceToggleResendsPayloadAndClearRemainsEmpty()
    {
        var scheduler = new ChatboxScheduler();
        scheduler.Set(1, "hello", true);
        Assert.Equal("hello", scheduler.Take(0)!.Value.Text);
        scheduler.Set(1, "hello", true, true);
        Assert.Null(scheduler.Take(0.1));
        Assert.Equal("hello" + ChatboxFormatter.CompactSuffix, scheduler.Take(1.05)!.Value.Text);
        scheduler.Set(1, "hello", true);
        Assert.Equal("hello", scheduler.Take(2.1)!.Value.Text);
        scheduler.Set(2, "", true, true);
        Assert.Equal("", scheduler.Take(3.2)!.Value.Text);
        scheduler.ReceiverChanged();
        scheduler.Set(2, "", true, true, forceSend: true);
        Assert.Equal("", scheduler.Take(4.3)!.Value.Text); // Explicit manual clear works even before any text.
    }

    [Fact]
    public void ManualSuppressesAutomaticAndLiveEditsCoalesceBeforeCurrentAutomaticResume()
    {
        var manual = new ManualChat();
        var scheduler = new ChatboxScheduler();
        Assert.Equal("A", manual.Desired("A", 0));
        manual.Focus(true);
        manual.Edit("draft", false, 0);
        Assert.Null(manual.Desired("B", 0.2));
        manual.LiveChanged(true);
        foreach (var draft in new[] { "H", "He", "Hello" })
        {
            manual.Edit(draft, true, 0.3);
            scheduler.Set(1, manual.Desired("ignored lyrics", 0.3)!, true, true);
        }
        Assert.Equal("Hello" + ChatboxFormatter.CompactSuffix, scheduler.Take(0.3)!.Value.Text);
        manual.Send();
        Assert.False(manual.Typing(0.4));
        Assert.Equal("Hello", manual.Desired("C", 20)); // Hold starts after emission, not button click.
        manual.Sent(20);
        Assert.Equal("Hello", manual.Desired("D", 27.9));
        Assert.Equal("current E", manual.Desired("current E", 28));
        manual.Focus(true); manual.Edit("new", true, 29); manual.Resume();
        Assert.Equal("current F", manual.Desired("current F", 29));
    }

    [Fact]
    public void TypingClearsOnIdleFocusLossSendClearResumeAndShutdownSignal()
    {
        var manual = new ManualChat(); var signal = new TypingSignal();
        manual.Focus(true); manual.Edit("hello", false, 0);
        Assert.True(signal.Take(manual.Typing(0), 0));
        Assert.Null(signal.Take(manual.Typing(1), 1));
        Assert.True(signal.Take(manual.Typing(2), 2));
        Assert.False(signal.Take(manual.Typing(3), 3));
        manual.Edit("hello!", false, 4); manual.Focus(false); Assert.False(manual.Typing(4));
        manual.Focus(true); manual.Send(); Assert.False(manual.Typing(4));
        manual.Edit("", true, 5); Assert.False(manual.Typing(5));
        manual.Edit("x", true, 6); manual.Resume(); Assert.False(manual.Typing(6));
        signal.Take(true, 7); Assert.False(signal.Take(false, 8));
        Assert.Equal("/chatbox/typing\0,T\0\0", Encoding.UTF8.GetString(ChatboxFormatter.TypingPacket(true)));
        Assert.Equal("/chatbox/typing\0,F\0\0", Encoding.UTF8.GetString(ChatboxFormatter.TypingPacket(false)));
    }
}
