namespace LyricsChatbox;

public enum DecorationNavigation
{
    Popular, Symbols, TextArt, Kaomoji, Dividers, Frames, Hearts, Music, Status, Favorites, MyItems
}

public enum DecorationPresentation { Dense, Compact, Wide, Art }

public sealed record DecorationNavigationOption(DecorationNavigation Mode, string Label);
public sealed record DecorationKindOption(string Kind, string Label);

public static class DecorationPickerPolicy
{
    public static readonly IReadOnlyList<DecorationNavigationOption> Options = Array.AsReadOnly(new[]
    {
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

    public static IReadOnlyList<DecorationEntry> Filter(DecorationLibrary library, DecorationNavigation mode, string? query)
    {
        if (mode == DecorationNavigation.Popular) return library.Search(query, popularOnly: true);
        if (Kind(mode) is { } kind)
            return library.Search(query, kind).Where(item => !IsUserItem(item)).ToArray();
        var matches = library.Search(query).Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        return mode == DecorationNavigation.Favorites
            ? library.Favorites.Where(item => matches.Contains(item.Id)).ToArray()
            : library.Items.Where(item => IsUserItem(item) && matches.Contains(item.Id)).ToArray();
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
