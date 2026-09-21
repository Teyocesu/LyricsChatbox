using System.Text;
using System.Text.Json;

namespace LyricsChatbox.Tests;

public sealed class DecorationPickerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "LyricsChatbox.Tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("", 0, 0, "☆", "☆", 1)]
    [InlineData("world", 0, 0, "hello ", "hello world", 6)]
    [InlineData("ac", 1, 0, "b", "abc", 2)]
    [InlineData("ab", 2, 0, "c", "abc", 3)]
    [InlineData("hello world", 6, 5, "VRChat", "hello VRChat", 12)]
    [InlineData("日本語", 1, 1, "🎵\nline", "日🎵\nline語", 8)]
    public void InsertionPreservesTextReplacesSelectionAndReturnsCaret(string text, int start, int length,
        string content, string expected, int caret)
    {
        var result = TextInsertion.Insert(text, start, length, 512, content);
        Assert.True(result.CanInsert);
        Assert.Equal(expected, result.Text);
        Assert.Equal(caret, result.CaretIndex);
    }

    [Fact]
    public void EditorLimitAllowsExactFitRefusesOneOverAndCountsReplacement()
    {
        var exact = TextInsertion.Insert("1234", 4, 0, 5, "5");
        Assert.True(exact.CanInsert);
        Assert.Equal("12345", exact.Text);
        Assert.Equal(5, exact.CaretIndex);

        var refused = TextInsertion.Insert("1234", 4, 0, 4, "5");
        Assert.False(refused.CanInsert);
        Assert.Equal("1234", refused.Text);
        Assert.Equal(4, refused.CaretIndex);
        Assert.Equal(142, TextInsertion.Preview(refused, "", true).Limit);

        var replacement = TextInsertion.Insert("12345", 1, 4, 5, "ABCD");
        Assert.True(replacement.CanInsert);
        Assert.Equal("1ABCD", replacement.Text);
        Assert.Equal(5, replacement.CaretIndex);
    }

    [Fact]
    public void FormatterAnalysisIsTheSingleBudgetAndGraphemeSourceOfTruth()
    {
        var fit = ChatboxFormatter.Analyze(new string('a', 144));
        Assert.False(fit.WouldTruncate);
        Assert.Equal(144, fit.VisibleUnits);
        Assert.Equal(144, fit.RequiredUnits);
        Assert.Equal(144, fit.Limit);
        Assert.Equal(ChatboxFormatter.Format(new string('a', 144)), fit.Payload);

        var family = "👨‍👩‍👧‍👦";
        var input = new string('a', 141) + family;
        var truncated = ChatboxFormatter.Analyze(input);
        Assert.True(truncated.WouldTruncate);
        Assert.Equal(new string('a', 141), truncated.Payload);
        Assert.Equal(141, truncated.VisibleUnits);
        Assert.Equal(141 + family.Length, truncated.RequiredUnits);
        Assert.DoesNotContain('\u200d', truncated.Payload);
        Assert.False(char.IsHighSurrogate(truncated.Payload[^1]));
    }

    [Fact]
    public void CustomAndStatusPreviewsUseCurrentCompositionThenFormatterAnalysis()
    {
        var customInsert = TextInsertion.Insert("", 0, 0, 512, new string('c', 144));
        var customRaw = LyricContextComposer.ComposeProfile(LyricContext.Empty, null, "Custom", customInsert.Text,
            "", "Current only", false, DateTimeOffset.UnixEpoch);
        var custom = TextInsertion.Preview(customInsert, customRaw, false);
        Assert.True(custom.CanInsert);
        Assert.False(custom.WouldTruncate);
        Assert.Equal(144, custom.VisibleUnits);

        var customLong = TextInsertion.Insert("", 0, 0, 512, new string('c', 145));
        var customLongRaw = LyricContextComposer.ComposeProfile(LyricContext.Empty, null, "Custom", customLong.Text,
            "", "Current only", false, DateTimeOffset.UnixEpoch);
        Assert.True(TextInsertion.Preview(customLong, customLongRaw, false).WouldTruncate);

        var statusInsert = TextInsertion.Insert("", 0, 0, 512, "Ready");
        var statusRaw = LyricContextComposer.ComposeProfile(LyricContext.Empty, null, "Status / Time", "{lyrics}",
            statusInsert.Text, "Current only", false, new DateTimeOffset(2026, 9, 20, 12, 34, 0, TimeSpan.Zero));
        var status = TextInsertion.Preview(statusInsert, MessageLayout.Align(statusRaw, "Center"), false);
        Assert.False(status.WouldTruncate);
        Assert.Contains("Ready", statusRaw);
    }

    [Fact]
    public void UnusedEditorInsertionReportsThatCurrentVisibleOutputIsUnchanged()
    {
        var insertion = TextInsertion.Insert("unused", 6, 0, 512, " decoration");
        var current = LyricContextComposer.ComposeProfile(new("", "Visible lyric", ""), null, "Lyrics Only",
            "unused", "unused", "Current only", false);
        var prospective = LyricContextComposer.ComposeProfile(new("", "Visible lyric", ""), null, "Lyrics Only",
            insertion.Text, "unused", "Current only", false);

        var preview = TextInsertion.Preview(insertion, prospective, false, currentRawOutput: current);

        Assert.False(preview.OutputChanged);
        Assert.Equal("Current output unchanged: 13 / 144", DecorationPickerPolicy.PreviewText(preview));
    }

    [Fact]
    public void FloatingManualAndLineBudgetWarningsUseExistingFormatterRules()
    {
        var fitsFloating = TextInsertion.Insert("", 0, 0, 512, new string('f', 142));
        Assert.False(TextInsertion.Preview(fitsFloating, fitsFloating.Text, true).WouldTruncate);
        Assert.Equal(142, TextInsertion.Preview(fitsFloating, fitsFloating.Text, true).Limit);

        var truncatesFloating = TextInsertion.Insert("", 0, 0, 512, new string('f', 143));
        Assert.True(TextInsertion.Preview(truncatesFloating, truncatesFloating.Text, true).WouldTruncate);

        var manual = TextInsertion.Insert("A\nABC", 1, 0, 2048, "☆");
        var aligned = MessageLayout.Align(manual.Text, "Right");
        var manualPreview = TextInsertion.Preview(manual, aligned, false);
        Assert.False(manualPreview.WouldTruncate);
        Assert.Equal(aligned.Length, manualPreview.VisibleUnits);

        var tenLines = TextInsertion.Insert("", 0, 0, 512, string.Join('\n', Enumerable.Repeat("line", 10)));
        Assert.True(TextInsertion.Preview(tenLines, tenLines.Text, false).WouldTruncate);
        Assert.Equal(9, ChatboxFormatter.Analyze(tenLines.Text).Payload.Split('\n').Length);
    }

    [Fact]
    public void NavigationMapsEveryModeToExistingLibraryFilteringAndPresentation()
    {
        var catalog = Load([
            Entry("symbol-star", "Star", "Symbol", "☆", true), Entry("textart-cat", "Cat", "TextArt", "cat"),
            Entry("kaomoji-happy", "Happy", "Kaomoji", "(^_^)"), Entry("divider-star", "Star divider", "Divider", "---"),
            Entry("frame-box", "Box", "Frame", "[]"), Entry("heart-one", "Heart", "Heart", "♡"),
            Entry("music-note", "Note", "Music", "♪"), Entry("status-ready", "Ready", "Status", "Ready")]);
        var user = new UserDecoration("user-00000000000000000000000000000001", "My star", "Symbol", "✦");
        var state = new DecorationState(1, ["status-ready", user.Id, "symbol-star"], [user]);
        var library = DecorationLibrary.Create(catalog, state);

        Assert.Equal(new[] { "symbol-star" }, Ids(library, DecorationNavigation.Popular));
        Assert.Equal(new[] { "symbol-star" }, Ids(library, DecorationNavigation.Symbols));
        Assert.Equal(new[] { "textart-cat" }, Ids(library, DecorationNavigation.TextArt));
        Assert.Equal(new[] { "kaomoji-happy" }, Ids(library, DecorationNavigation.Kaomoji));
        Assert.Equal(new[] { "divider-star" }, Ids(library, DecorationNavigation.Dividers));
        Assert.Equal(new[] { "frame-box" }, Ids(library, DecorationNavigation.Frames));
        Assert.Equal(new[] { "heart-one" }, Ids(library, DecorationNavigation.Hearts));
        Assert.Equal(new[] { "music-note" }, Ids(library, DecorationNavigation.Music));
        Assert.Equal(new[] { "status-ready" }, Ids(library, DecorationNavigation.Status));
        Assert.Equal(new[] { "status-ready", user.Id, "symbol-star" }, Ids(library, DecorationNavigation.Favorites));
        Assert.Equal(new[] { user.Id }, Ids(library, DecorationNavigation.MyItems));
        Assert.Equal(new[]
        {
            DecorationNavigation.Favorites, DecorationNavigation.Popular, DecorationNavigation.Symbols,
            DecorationNavigation.TextArt, DecorationNavigation.Kaomoji, DecorationNavigation.Dividers,
            DecorationNavigation.Frames, DecorationNavigation.Hearts, DecorationNavigation.Music, DecorationNavigation.Status
        }, DecorationPickerPolicy.Options.Select(option => option.Mode));
        Assert.DoesNotContain(DecorationPickerPolicy.Options, option => option.Mode == DecorationNavigation.MyItems);
        Assert.Equal(DecorationNavigation.Popular, DecorationPickerPolicy.DefaultMode);

        Assert.Equal(DecorationPresentation.Dense, DecorationPickerPolicy.Presentation(DecorationNavigation.Symbols));
        Assert.Equal(DecorationPresentation.Art, DecorationPickerPolicy.Presentation(DecorationNavigation.TextArt));
        Assert.Equal(DecorationPresentation.Wide, DecorationPickerPolicy.Presentation(DecorationNavigation.Dividers));
        Assert.Equal(DecorationPresentation.Compact, DecorationPickerPolicy.Presentation(DecorationNavigation.Kaomoji));
        Assert.Contains(DecorationPickerPolicy.KindOptions, option => option is { Kind: "TextArt", Label: "Text Art" });
    }

    [Fact]
    public void GroupsSearchAliasesAndFitsComposeWithoutChangingResourceOrder()
    {
        var catalog = Load([
            Entry("symbol-star", "Star", "Symbol", "☆", group: "Sparkles", searchTerms: ["favorite"]),
            Entry("symbol-moon", "Moon", "Symbol", "☾", group: "Celestial", searchTerms: ["night"]),
            Entry("symbol-spark", "Spark", "Symbol", "✦", group: "Sparkles", searchTerms: ["shine"]),
            Entry("status-ready", "Ready", "Status", "Ready", group: "Presence")
        ]);
        var library = DecorationLibrary.Create(catalog, DecorationState.Empty);

        Assert.Equal(new[] { "Sparkles", "Celestial" }, DecorationPickerPolicy.Groups(library, DecorationNavigation.Symbols));
        Assert.Equal(new[] { "symbol-star", "symbol-moon", "symbol-spark" },
            DecorationPickerPolicy.Filter(library, DecorationNavigation.Symbols, null).Select(item => item.Id));
        Assert.Equal(new[] { "symbol-star", "symbol-spark" },
            DecorationPickerPolicy.Filter(library, DecorationNavigation.Symbols, null, "Sparkles").Select(item => item.Id));
        Assert.Equal(new[] { "symbol-star" },
            DecorationPickerPolicy.Filter(library, DecorationNavigation.Symbols, "FAVORITE", "Sparkles").Select(item => item.Id));
        Assert.Equal(new[] { "symbol-moon" },
            DecorationPickerPolicy.Filter(library, DecorationNavigation.Symbols, null, null, item => item.Id == "symbol-moon").Select(item => item.Id));
        foreach (var group in DecorationPickerPolicy.Groups(library, DecorationNavigation.Symbols))
            Assert.All(DecorationPickerPolicy.Filter(library, DecorationNavigation.Symbols, null, group),
                item => Assert.Equal(group, item.Group));
        Assert.Empty(DecorationPickerPolicy.Groups(library, DecorationNavigation.Popular));
    }

    [Fact]
    public void FitsFilterUsesProspectiveInsertionForNormalFloatingLinesAndEditorCapacity()
    {
        var catalog = Load([
            Entry("symbol-one", "One", "Symbol", "x"),
            Entry("symbol-two", "Two", "Symbol", "xx"),
            Entry("symbol-line", "Line", "Symbol", "\nx")
        ]);
        var library = DecorationLibrary.Create(catalog, DecorationState.Empty);

        Assert.Equal("symbol-one", Fits(library, new string('a', 143), 512, compact: false).Single().Id);
        Assert.Equal("symbol-one", Fits(library, new string('a', 141), 512, compact: true).Single().Id);
        var nineLines = Fits(library, string.Join('\n', Enumerable.Repeat("x", 9)), 512, compact: false);
        Assert.Contains(nineLines, item => item.Id == "symbol-one");
        Assert.DoesNotContain(nineLines, item => item.Id == "symbol-line");
        Assert.Empty(Fits(library, new string('a', 512), 512, compact: false));
    }

    [Fact]
    public void PreviewCopyDistinguishesFitTruncationCapacityAndUnchangedOutput()
    {
        Assert.Equal("Current output after insertion: 140 / 142 · Fits",
            DecorationPickerPolicy.PreviewText(new(true, false, 140, 142)));
        Assert.Equal("Current output after insertion: 156 / 142 · Will be truncated",
            DecorationPickerPolicy.PreviewText(new(true, true, 156, 142)));
        Assert.Equal("Not enough editor space",
            DecorationPickerPolicy.PreviewText(new(false, false, 0, 142)));
        Assert.Equal("Current output unchanged: 0 / 142",
            DecorationPickerPolicy.PreviewText(new(true, false, 0, 142, OutputChanged: false)));

        var insertion = TextInsertion.Insert("unused", 6, 0, 512, " decoration");
        Assert.False(TextInsertion.Preview(insertion, "", true, currentRawOutput: "").OutputChanged);
        Assert.True(TextInsertion.Preview(insertion, "visible", true, currentRawOutput: "").OutputChanged);
    }

    [Fact]
    public void SearchPreservesActiveKindAndFavoriteOrderAndReturnsEmptyStateText()
    {
        var catalog = Load([
            Entry("symbol-star", "Bright Star", "Symbol", "☆"),
            Entry("symbol-moon", "Moon", "Symbol", "☾"),
            Entry("status-star", "Star status", "Status", "Ready")]);
        var state = new DecorationState(1, ["status-star", "symbol-star"], []);
        var library = DecorationLibrary.Create(catalog, state);

        Assert.Equal(new[] { "symbol-star" }, DecorationPickerPolicy.Filter(library, DecorationNavigation.Symbols, "STAR").Select(item => item.Id));
        Assert.Equal(new[] { "status-star", "symbol-star" }, DecorationPickerPolicy.Filter(library, DecorationNavigation.Favorites, "star").Select(item => item.Id));
        Assert.Empty(DecorationPickerPolicy.Filter(library, DecorationNavigation.Symbols, "missing"));
        Assert.Equal("No decorations found.", DecorationPickerPolicy.EmptyMessage(DecorationNavigation.Symbols, "missing", true));
        Assert.Equal("Favorite items to find them here.", DecorationPickerPolicy.EmptyMessage(DecorationNavigation.Favorites, null, true));
        Assert.Equal("Save your own symbols or text art here.", DecorationPickerPolicy.EmptyMessage(DecorationNavigation.MyItems, null, true));
    }

    [Fact]
    public void CatalogFailureKeepsMyItemsAvailableWhileBuiltInViewsExplainFailure()
    {
        var badCatalog = LoadRaw("{bad");
        var user = new UserDecoration("user-00000000000000000000000000000001", "Mine", "Symbol", "✦");
        var library = DecorationLibrary.Create(badCatalog, new DecorationState(1, [], [user]));

        Assert.Empty(DecorationPickerPolicy.Filter(library, DecorationNavigation.Symbols, null));
        Assert.Equal("Built-in decorations are unavailable.", DecorationPickerPolicy.EmptyMessage(DecorationNavigation.Symbols, null, false));
        Assert.Equal(user.Id, DecorationPickerPolicy.Filter(library, DecorationNavigation.MyItems, null).Single().Id);
    }

    [Fact]
    public void FavoriteMutationStaysValidWhenLocalSaveFails()
    {
        Directory.CreateDirectory(root);
        var blockedRoot = Path.Combine(root, "not-a-directory");
        File.WriteAllText(blockedRoot, "owned test file");
        var next = DecorationState.Empty.Favorite("symbol-star")!;

        Assert.False(new LocalData(blockedRoot).SaveDecorationState(next));
        Assert.Equal(new[] { "symbol-star" }, next.FavoriteIds);
        Assert.Equal("owned test file", File.ReadAllText(blockedRoot));
        Assert.True(next.Unfavorite("symbol-star")!.IsValid);
    }

    private static string[] Ids(DecorationLibrary library, DecorationNavigation mode) =>
        DecorationPickerPolicy.Filter(library, mode, null).Select(item => item.Id).ToArray();

    private static IReadOnlyList<DecorationEntry> Fits(DecorationLibrary library, string current, int maximumLength, bool compact) =>
        DecorationPickerPolicy.Filter(library, DecorationNavigation.Symbols, null, null, entry =>
        {
            var insertion = TextInsertion.Insert(current, current.Length, 0, maximumLength, entry.Content);
            return TextInsertion.Preview(insertion, insertion.CanInsert ? insertion.Text : current, compact) is
                { CanInsert: true, WouldTruncate: false };
        });

    private static DecorationEntry Entry(string id, string name, string kind, string content, bool popular = false,
        string? group = null, IReadOnlyList<string>? searchTerms = null) =>
        new(id, name, kind, content, popular, group, searchTerms);

    private static DecorationCatalog Load(IEnumerable<DecorationEntry> items) =>
        LoadRaw(JsonSerializer.Serialize(new { version = 1, items }));

    private static DecorationCatalog LoadRaw(string json) =>
        DecorationCatalog.Load(new MemoryStream(Encoding.UTF8.GetBytes(json)));

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
