namespace LyricsChatbox;

public enum DecorationNavigation
{
    Suggested, Popular, Symbols, TextArt, Kaomoji, Dividers, Frames, Hearts, Music, Status, Favorites, MyItems
}

public enum DecorationPresentation { Dense, Compact, Wide, Art }
public enum DecorationTarget { Custom, Status, Manual }

public sealed record DecorationNavigationOption(DecorationNavigation Mode, string Label);
public sealed record DecorationKindOption(string Kind, string Label);

public static class DecorationPickerPolicy
{
    public static readonly IReadOnlyList<DecorationNavigationOption> Options = Array.AsReadOnly(new[]
    {
        new DecorationNavigationOption(DecorationNavigation.Suggested, "Suggested"),
        new DecorationNavigationOption(DecorationNavigation.Popular, "Popular"),
        new DecorationNavigationOption(DecorationNavigation.Symbols, "Symbols"),
        new DecorationNavigationOption(DecorationNavigation.TextArt, "Text Art"),
        new DecorationNavigationOption(DecorationNavigation.Kaomoji, "Kaomoji"),
        new DecorationNavigationOption(DecorationNavigation.Dividers, "Dividers"),
        new DecorationNavigationOption(DecorationNavigation.Frames, "Frames"),
        new DecorationNavigationOption(DecorationNavigation.Hearts, "Hearts"),
        new DecorationNavigationOption(DecorationNavigation.Music, "Music"),
        new DecorationNavigationOption(DecorationNavigation.Status, "Status"),
        new DecorationNavigationOption(DecorationNavigation.Favorites, "Favorites"),
        new DecorationNavigationOption(DecorationNavigation.MyItems, "My items")
    });
    public static readonly IReadOnlyList<DecorationKindOption> KindOptions = Array.AsReadOnly(
        DecorationKinds.All.Select(kind => new DecorationKindOption(kind, kind == "TextArt" ? "Text Art" : kind)).ToArray());

    public static DecorationPresentation Presentation(DecorationNavigation mode) => mode switch
    {
        DecorationNavigation.Symbols or DecorationNavigation.Hearts or DecorationNavigation.Music => DecorationPresentation.Dense,
        DecorationNavigation.TextArt or DecorationNavigation.Frames => DecorationPresentation.Art,
        DecorationNavigation.Dividers => DecorationPresentation.Wide,
        _ => DecorationPresentation.Compact
    };

    public static IReadOnlyList<DecorationEntry> Filter(DecorationLibrary library, DecorationNavigation mode, string? query,
        string? group = null, Func<DecorationEntry, bool>? fits = null)
    {
        if (mode == DecorationNavigation.Suggested) return [];
        IEnumerable<DecorationEntry> entries;
        if (mode == DecorationNavigation.Popular) entries = library.Search(query, popularOnly: true);
        else if (Kind(mode) is { } kind) entries = library.Search(query, kind).Where(item => !IsUserItem(item));
        else
        {
            var matches = library.Search(query).Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
            entries = mode == DecorationNavigation.Favorites
            ? library.Favorites.Where(item => matches.Contains(item.Id)).ToArray()
            : library.Items.Where(item => IsUserItem(item) && matches.Contains(item.Id)).ToArray();
        }
        if (group is not null) entries = entries.Where(item => item.Group == group);
        if (fits is not null) entries = entries.Where(fits);
        return entries.ToArray();
    }

    public static IReadOnlyList<string> Groups(DecorationLibrary library, DecorationNavigation mode) =>
        Kind(mode) is null ? [] : Filter(library, mode, null).Select(item => item.Group).OfType<string>()
            .Distinct(StringComparer.Ordinal).ToArray();

    public static IReadOnlyList<DecorationEntry> Suggested(DecorationLibrary library, DecorationTarget target, string? query,
        Func<DecorationEntry, DecorationInsertionPreview> preview, bool fitsOnly = false, int maximum = 24)
    {
        var preferences = target switch
        {
            DecorationTarget.Manual => new[] { "Kaomoji", "Status", "Symbol", "Heart", "Divider", "TextArt", "Music", "Frame" },
            DecorationTarget.Status => new[] { "Status", "Symbol", "Divider", "Heart", "Kaomoji", "Music", "TextArt", "Frame" },
            _ => new[] { "Symbol", "Divider", "Frame", "Music", "TextArt", "Heart", "Kaomoji", "Status" }
        };
        return library.Search(query).Where(item => !IsUserItem(item))
            .Select((entry, index) => new { Entry = entry, Index = index, Preview = preview(entry) })
            .Where(item => !fitsOnly || item.Preview is { CanInsert: true, WouldTruncate: false })
            .OrderBy(item => item.Preview.CanInsert ? item.Preview.WouldTruncate ? 1 : 0 : 2)
            .ThenBy(item => Array.IndexOf(preferences, item.Entry.Kind))
            .ThenBy(item => item.Entry.Popular ? 0 : 1)
            .ThenBy(item => item.Index)
            .Take(maximum)
            .Select(item => item.Entry).ToArray();
    }

    public static string EmptyMessage(DecorationNavigation mode, string? query, bool catalogAvailable)
    {
        if (!catalogAvailable && mode is not (DecorationNavigation.Favorites or DecorationNavigation.MyItems))
            return "Built-in decorations are unavailable.";
        if (!string.IsNullOrWhiteSpace(query)) return "No decorations found.";
        return mode switch
        {
            DecorationNavigation.Favorites => "Favorite items to find them here.",
            DecorationNavigation.MyItems => "Save your own symbols or text art here.",
            _ => "No decorations found."
        };
    }

    private static string? Kind(DecorationNavigation mode) => mode switch
    {
        DecorationNavigation.Symbols => "Symbol",
        DecorationNavigation.TextArt => "TextArt",
        DecorationNavigation.Kaomoji => "Kaomoji",
        DecorationNavigation.Dividers => "Divider",
        DecorationNavigation.Frames => "Frame",
        DecorationNavigation.Hearts => "Heart",
        DecorationNavigation.Music => "Music",
        DecorationNavigation.Status => "Status",
        _ => null
    };

    private static bool IsUserItem(DecorationEntry item) => item.Id.StartsWith("user-", StringComparison.Ordinal);
}
