using System.Text;
using System.Text.Json;

namespace LyricsChatbox.Tests;

public class DisplayTests
{
    public static IEnumerable<object[]> CompositionTokenCases() =>
        ChatboxComposer.Tokens.Select(token => new object[] { token.Token, token.Description });

    [Theory]
    [MemberData(nameof(CompositionTokenCases))]
    public void EveryCompositionTokenUsesSharedCaretSelectionAndCapacityRules(string token, string description)
    {
        Assert.False(string.IsNullOrWhiteSpace(description));
        var atCaret = TextInsertion.Insert("beforeafter", 6, 0, 512, token);
        Assert.True(atCaret.CanInsert);
        Assert.Equal("before" + token + "after", atCaret.Text);
        Assert.Equal(6 + token.Length, atCaret.CaretIndex);

        var replacement = TextInsertion.Insert("before selected after", 7, 8, 512, token);
        Assert.True(replacement.CanInsert);
        Assert.Equal("before " + token + " after", replacement.Text);
        Assert.Equal(7 + token.Length, replacement.CaretIndex);

        var full = new string('x', 512);
        var refused = TextInsertion.Insert(full, full.Length, 0, 512, token);
        Assert.False(refused.CanInsert);
        Assert.Equal(full, refused.Text);
        Assert.Equal(full.Length, refused.CaretIndex);
    }

    [Theory]
    [InlineData("Lyrics Only", "{message}", false, true, false)]
    [InlineData("Song + Lyrics", "{message}", false, true, false)]
    [InlineData("Status / Time", "{lyrics}", true, false, false)]
    [InlineData("Custom", "{lyrics}", false, true, true)]
    [InlineData("Custom", "{message}", true, false, true)]
    [InlineData("Custom", "{message}\n{lyrics}", true, true, true)]
    [InlineData("Custom", "{Message}\n{ lyrics }", false, false, true)]
    public void DisplayDerivedStateExactlyMatchesComposerTokenSemantics(string preset, string template,
        bool message, bool lyrics, bool custom)
    {
        var state = DisplayDerivedState.From(preset, template);
        Assert.Equal(message, state.ShowStatusMessages);
        Assert.Equal(lyrics, state.LyricContextSupported);
        Assert.True(state.ShowLyricContextCard);
        Assert.Equal(custom, state.ShowCustomEditor);
        Assert.Equal(preset is "Custom" or "Status / Time", state.ShowAlignment);
        Assert.Equal(lyrics ? "" : custom
            ? "Add {lyrics} to Custom composition to use Lyric Context."
            : "This composition does not use lyrics.", state.LyricContextUnavailableText);
        Assert.Equal(message, ChatboxComposer.ConsumesToken(preset, template, "{message}"));
        Assert.Equal(lyrics, ChatboxComposer.ConsumesToken(preset, template, "{lyrics}"));
    }

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
        Assert.False(settings.Compact);
        Assert.False((settings with { CustomTemplate = null! }).IsValid);
        Assert.False((settings with { Preset = "invalid" }).IsValid);
        Assert.Equal("", ChatboxComposer.Compose(new string('x', 513), null, "", "", default, null));
        Assert.Equal("{broken", ChatboxComposer.Compose("{broken", null, "", "", default, null));
        var roundtrip = settings with { Preset = "Custom", CustomTemplate = "{message}", Message = "日本語", Compact = true };
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
        var packet = scheduler.Take(0)!.Value; Assert.Equal("hello", packet.Text); scheduler.Complete(packet, true);
        scheduler.Set(1, "hello", true, true);
        Assert.Null(scheduler.Take(0.1));
        packet = scheduler.Take(1.05)!.Value; Assert.Equal("hello" + ChatboxFormatter.CompactSuffix, packet.Text); scheduler.Complete(packet, true);
        scheduler.Set(1, "hello", true);
        packet = scheduler.Take(2.1)!.Value; Assert.Equal("hello", packet.Text); scheduler.Complete(packet, true);
        scheduler.Set(2, "", true, true);
        packet = scheduler.Take(3.2)!.Value; Assert.Equal("", packet.Text); scheduler.Complete(packet, true);
    }

}
