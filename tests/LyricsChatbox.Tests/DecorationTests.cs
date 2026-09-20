using System.Reflection;
using System.Text;
using System.Text.Json;

namespace LyricsChatbox.Tests;

public sealed class DecorationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "LyricsChatbox.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void EmbeddedCatalogLoadsWithinBoundsAndEveryEntryIsValid()
    {
        var catalog = DecorationCatalog.LoadBuiltIn();
        using var resource = Assembly.GetAssembly(typeof(DecorationCatalog))!
            .GetManifestResourceStream("LyricsChatbox.Assets.Decorations.json");

        Assert.True(catalog.IsAvailable);
        Assert.Equal(DecorationCatalogStatus.Available, catalog.Status);
        Assert.NotNull(resource);
        Assert.InRange(resource.Length, 1, DecorationCatalog.MaximumBytes);
        Assert.InRange(catalog.Items.Count, 80, DecorationCatalog.MaximumEntries);
        Assert.All(catalog.Items, item => Assert.True(item.IsValid, item.Id));
        Assert.Equal(catalog.Items.Count, catalog.Items.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(catalog.Items, item => item.Id.StartsWith("user-", StringComparison.OrdinalIgnoreCase));
        Assert.All(DecorationKinds.All, kind => Assert.Contains(catalog.Items, item => item.Kind == kind));
        Assert.All(catalog.Items, item =>
        {
            Assert.InRange(item.Id.Length, 1, 64);
            Assert.InRange(item.Name.Length, 1, 40);
            Assert.InRange(item.Content.Length, 1, 512);
            Assert.InRange(item.Content.Count(c => c == '\n') + 1, 1, 9);
            Assert.DoesNotContain('\0', item.Content);
            Assert.DoesNotContain('\t', item.Content);
            Assert.DoesNotContain(new[] { "{lyrics}", "{title}", "{artist}", "{album}", "{time}", "{message}", "{elapsed}", "{duration}" },
                token => item.Content.Contains(token, StringComparison.Ordinal));
        });
        Assert.Equal(new Dictionary<string, int>
        {
            ["Symbol"] = 16, ["TextArt"] = 8, ["Kaomoji"] = 12, ["Divider"] = 12,
            ["Frame"] = 8, ["Heart"] = 12, ["Music"] = 12, ["Status"] = 10
        }, catalog.Items.GroupBy(item => item.Kind).ToDictionary(group => group.Key, group => group.Count()));
    }

    [Fact]
    public void LoaderPreservesResourceOrderAndStableIdsRatherThanGeneratingIndexes()
    {
        var items = new[] { Entry("symbol-alpha", "Alpha", "α"), Entry("symbol-beta", "Beta", "β") };
        var reversed = Load(items.Reverse());

        Assert.Equal(new[] { "symbol-beta", "symbol-alpha" }, reversed.Items.Select(item => item.Id));
    }

    [Fact]
    public void LoaderFailsClosedForVersionCountIdsKindsAndTextBounds()
    {
        AssertRejected(Load([Entry()], 2));
        AssertRejected(Load([Entry("same"), Entry("same")]));
        AssertRejected(Load(Enumerable.Range(0, 257).Select(i => Entry("symbol-" + i))));
        AssertRejected(Load([Entry(kind: "Unknown")]));
        AssertRejected(Load([Entry(name: "")]));
        AssertRejected(Load([Entry(name: new string('n', 41))]));
        AssertRejected(Load([Entry(content: "")]));
        AssertRejected(Load([Entry(content: new string('x', 513))]));
        AssertRejected(Load([Entry(content: string.Join('\n', Enumerable.Repeat("x", 10)))]));
        AssertRejected(Load([Entry(content: "x\0y")]));
        AssertRejected(Load([Entry(content: "x\ty")]));
        AssertRejected(Load([Entry(content: "x\u0085y")]));
        AssertRejected(Load([Entry("user-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]));
        AssertRejected(LoadRaw("{not json"));
        AssertRejected(LoadRaw("""{"version":1,"items":[{"id":"symbol-bad","name":"Bad","kind":"Symbol","content":"\uD800"}]}"""));

        using var oversized = new MemoryStream(new byte[DecorationCatalog.MaximumBytes + 1]);
        var result = DecorationCatalog.Load(oversized);
        Assert.False(result.IsAvailable);
        Assert.Equal(DecorationCatalogStatus.Oversized, result.Status);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void SearchCombinesOptionalQueryKindAndPopularWithoutChangingOrder()
    {
        var catalog = Load([
            Entry("symbol-star", "Bright Star", "★", popular: true),
            Entry("status-listening", "Listening", "Music now", "Status"),
            Entry("music-note", "Quiet note", "♪", "Music", true),
            Entry("symbol-moon", "Moon", "☾")
        ]);
        var library = DecorationLibrary.Create(catalog, DecorationState.Empty);

        Assert.Equal(catalog.Items, library.Search());
        Assert.Equal(new[] { "symbol-star" }, library.Search("STAR").Select(item => item.Id));
        Assert.Equal(new[] { "status-listening" }, library.Search("music").Select(item => item.Id));
        Assert.Equal(new[] { "music-note" }, library.Search(kind: "music").Select(item => item.Id));
        Assert.Equal(new[] { "symbol-star", "music-note" }, library.Search(popularOnly: true).Select(item => item.Id));
        Assert.Equal(new[] { "music-note" }, library.Search("note", "Music", true).Select(item => item.Id));
        Assert.Empty(library.Search("missing"));
        Assert.Empty(library.Search(kind: "Unknown"));
    }

    [Fact]
    public void FavoritesAreOrderedDeduplicatedBoundedAndResolveStaleIdsSafely()
    {
        var state = DecorationState.Empty.Favorite("symbol-beta")!.Favorite("symbol-alpha")!.Favorite("symbol-beta")!;
        Assert.Equal(new[] { "symbol-beta", "symbol-alpha" }, state.FavoriteIds);
        state = state.Unfavorite("symbol-beta")!;
        Assert.Equal(new[] { "symbol-alpha" }, state.FavoriteIds);

        var full = DecorationState.Empty;
        for (var i = 0; i < DecorationState.MaximumFavorites; i++) full = full.Favorite("favorite-" + i)!;
        Assert.Equal(DecorationState.MaximumFavorites, full.FavoriteIds!.Count);
        Assert.Null(full.Favorite("favorite-overflow"));

        var catalog = Load([Entry("symbol-alpha"), Entry("symbol-beta")]);
        var stale = new DecorationState(1, ["removed", "symbol-beta"], []);
        Assert.Equal(new[] { "symbol-beta" }, DecorationLibrary.Create(catalog, stale).Favorites.Select(item => item.Id));
    }

    [Fact]
    public void MyItemsSupportCreateUpdateFavoriteDeleteAndCollisionRefusal()
    {
        var item = UserDecoration.Create("My sparkle", "Symbol", "日本語 ✦")!;
        var state = DecorationState.Empty.AddMyItem(item)!;
        Assert.StartsWith("user-", item.Id);
        Assert.Equal(37, item.Id.Length);
        Assert.Null(state.AddMyItem(item));

        state = state.UpdateMyItem(item.Id, "Edited", "Kaomoji", "(•̀ᴗ•́)و")!;
        var myItem = state.MyItems!.Single();
        Assert.Equal(item.Id, myItem.Id);
        Assert.Equal("Edited", myItem.Name);
        state = state.Favorite(item.Id)!;
        var library = DecorationLibrary.Create(Load([Entry()]), state);
        Assert.Contains(library.Items, entry => entry.Id == item.Id);
        Assert.Equal(item.Id, library.Favorites.Single().Id);

        state = state.DeleteMyItem(item.Id)!;
        Assert.Empty(state.MyItems!);
        Assert.Empty(state.FavoriteIds!);
        Assert.Null(state.DeleteMyItem(item.Id));
    }

    [Fact]
    public void MyItemsEnforceCountNamespaceKindsAndPlainTextRules()
    {
        var items = Enumerable.Range(0, DecorationState.MaximumMyItems).Select(UserItem).ToArray();
        var state = new DecorationState(1, [], items);
        Assert.True(state.IsValid);
        Assert.Null(state.AddMyItem(UserItem(64)));
        Assert.True(new UserDecoration(UserId(90), "日本語", "TextArt", "🎵\nline").IsValid);

        Assert.False(new UserDecoration("plain-id", "Name", "Symbol", "x").IsValid);
        Assert.False(new UserDecoration("user-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "Name", "Symbol", "x").IsValid);
        Assert.False(new UserDecoration(UserId(70), " ", "Symbol", "x").IsValid);
        Assert.False(new UserDecoration(UserId(70), new string('n', 41), "Symbol", "x").IsValid);
        Assert.False(new UserDecoration(UserId(70), "Name", "Unknown", "x").IsValid);
        Assert.False(new UserDecoration(UserId(70), "Name", "Symbol", " \n ").IsValid);
        Assert.False(new UserDecoration(UserId(70), "Name", "Symbol", new string('x', 513)).IsValid);
        Assert.False(new UserDecoration(UserId(70), "Name", "Symbol", string.Join('\n', Enumerable.Repeat("x", 10))).IsValid);
        Assert.False(new UserDecoration(UserId(70), "Name", "Symbol", "x\0y").IsValid);
        Assert.False(new UserDecoration(UserId(70), "Name", "Symbol", "x\ty").IsValid);
        Assert.False(new UserDecoration(UserId(70), "Name", "Symbol", "x\u0085y").IsValid);
        Assert.False(new UserDecoration(UserId(70), "Name", "Symbol", "\uD800").IsValid);
    }

    [Fact]
    public void MissingAndValidDecorationStateRoundTripIndependently()
    {
        var data = new LocalData(root);
        var empty = data.ReadDecorationState();
        Assert.True(empty.IsValid);
        Assert.Empty(empty.FavoriteIds!);
        Assert.Empty(empty.MyItems!);
        Assert.False(File.Exists(Path.Combine(root, "decorations.json")));

        var item = UserDecoration.Create("Unicode", "Music", "日本語 🎵")!;
        var saved = DecorationState.Empty.AddMyItem(item)!.Favorite("symbol-star-outline")!.Favorite(item.Id)!;
        Assert.True(data.SaveDecorationState(saved));
        var restored = data.ReadDecorationState();
        Assert.Equal(saved.FavoriteIds, restored.FavoriteIds);
        Assert.Equal(saved.MyItems, restored.MyItems);
    }

    [Fact]
    public void MaximumValidDecorationStateFitsTheFileLimit()
    {
        var content = new string('\uFFFF', 512);
        var name = new string('\uFFFF', 40);
        var items = Enumerable.Range(0, DecorationState.MaximumMyItems)
            .Select(i => new UserDecoration(UserId(i), name, "TextArt", content)).ToArray();
        var favorites = Enumerable.Range(0, DecorationState.MaximumFavorites)
            .Select(i => ("favorite-" + i.ToString("D3")).PadRight(64, 'x')).ToArray();
        var state = new DecorationState(1, favorites, items);

        Assert.True(state.IsValid);
        Assert.True(new LocalData(root).SaveDecorationState(state));
        Assert.InRange(new FileInfo(Path.Combine(root, "decorations.json")).Length, 1, 256 * 1024);
        Assert.Equal(DecorationState.MaximumMyItems, new LocalData(root).ReadDecorationState().MyItems!.Count);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("invalid")]
    [InlineData("oversized")]
    public void BadDecorationStateReturnsEmptyAndPreservesTheOriginalFile(string kind)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "decorations.json");
        var bytes = kind switch
        {
            "malformed" => Encoding.UTF8.GetBytes("{bad"),
            "invalid" => Encoding.UTF8.GetBytes("""{"Version":2,"FavoriteIds":[],"MyItems":[]}"""),
            _ => new byte[256 * 1024 + 1]
        };
        File.WriteAllBytes(path, bytes);

        var state = new LocalData(root).ReadDecorationState();

        Assert.True(state.IsValid);
        Assert.Empty(state.FavoriteIds!);
        Assert.Empty(state.MyItems!);
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void BadDecorationStateCannotResetSettingsProfilesOrRotations()
    {
        var data = new LocalData(root);
        var settings = new AppSettings(Enabled: true, Message: "settings", Compact: true);
        var profile = new DisplayProfile("profile", "Profile", Message: "A",
            Rotation: new(Enabled: true, Items: [new("a", "A"), new("b", "B")]));
        Assert.True(data.SaveSettings(settings));
        Assert.True(data.SaveProfiles(new(1, profile.Id, [profile])));
        var settingsPath = Path.Combine(root, "settings.json");
        var profilesPath = Path.Combine(root, "profiles.json");
        var settingsBytes = File.ReadAllBytes(settingsPath);
        var profilesBytes = File.ReadAllBytes(profilesPath);
        File.WriteAllText(Path.Combine(root, "decorations.json"), "{bad");

        Assert.Equal(settings, data.ReadSettings());
        var restoredProfile = data.ReadProfiles(new()).Selected;
        Assert.Equal(JsonSerializer.Serialize(profile.Rotation), JsonSerializer.Serialize(restoredProfile.Rotation));
        Assert.Empty(data.ReadDecorationState().MyItems!);
        Assert.Equal(settingsBytes, File.ReadAllBytes(settingsPath));
        Assert.Equal(profilesBytes, File.ReadAllBytes(profilesPath));
    }

    [Fact]
    public void ExplicitSaveAtomicallyReplacesAPreviouslyBadDecorationFile()
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "decorations.json");
        File.WriteAllText(path, "{bad");
        var state = DecorationState.Empty.AddMyItem(UserItem(1))!.Favorite(UserId(1))!;

        Assert.True(new LocalData(root).SaveDecorationState(state));

        var restored = new LocalData(root).ReadDecorationState();
        Assert.Equal(state.FavoriteIds, restored.FavoriteIds);
        Assert.Equal(state.MyItems, restored.MyItems);
        Assert.Empty(Directory.EnumerateFiles(root, "*.tmp", SearchOption.TopDirectoryOnly));
    }

    private static DecorationEntry Entry(string id = "symbol-one", string name = "One", string content = "☆",
        string kind = "Symbol", bool popular = false) => new(id, name, kind, content, popular);

    private static UserDecoration UserItem(int index) => new(UserId(index), "Item " + index, "Symbol", "☆ " + index);
    private static string UserId(int index) => "user-" + index.ToString("x32");

    private static DecorationCatalog Load(IEnumerable<DecorationEntry> items, int version = 1) =>
        LoadRaw(JsonSerializer.Serialize(new { version, items }));

    private static DecorationCatalog LoadRaw(string json) =>
        DecorationCatalog.Load(new MemoryStream(Encoding.UTF8.GetBytes(json)));

    private static void AssertRejected(DecorationCatalog catalog)
    {
        Assert.False(catalog.IsAvailable);
        Assert.Empty(catalog.Items);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
