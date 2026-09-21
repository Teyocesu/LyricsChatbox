namespace LyricsChatbox;

public enum DecorationNavigation
{
    Favorites, Popular, Symbols, TextArt, Kaomoji, Dividers, Frames, Hearts, Music, Status, MyItems
}

public enum DecorationPresentation { Dense, Compact, Wide, Art }
public enum DecorationPreviewWording { CurrentOutput, ActiveMessage }

public sealed record DecorationNavigationOption(DecorationNavigation Mode, string Label);
public sealed record DecorationKindOption(string Kind, string Label);

public static class DecorationPickerPolicy
{
    public static readonly IReadOnlyList<DecorationNavigationOption> Options = Array.AsReadOnly(new[]
    {
        new DecorationNavigationOption(DecorationNavigation.Favorites, "Favorites"),
        new DecorationNavigationOption(DecorationNavigation.Popular, "Popular"),
        new DecorationNavigationOption(DecorationNavigation.Symbols, "Symbols"),
        new DecorationNavigationOption(DecorationNavigation.TextArt, "Text Art"),
        new DecorationNavigationOption(DecorationNavigation.Kaomoji, "Kaomoji"),
        new DecorationNavigationOption(DecorationNavigation.Dividers, "Dividers"),
        new DecorationNavigationOption(DecorationNavigation.Frames, "Frames"),
        new DecorationNavigationOption(DecorationNavigation.Hearts, "Hearts"),
        new DecorationNavigationOption(DecorationNavigation.Music, "Music"),
        new DecorationNavigationOption(DecorationNavigation.Status, "Status")
    });
    public const DecorationNavigation DefaultMode = DecorationNavigation.Popular;
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

    public static string PreviewText(DecorationInsertionPreview preview,
        DecorationPreviewWording wording = DecorationPreviewWording.CurrentOutput) => !preview.CanInsert ? "Not enough editor space"
        : wording == DecorationPreviewWording.ActiveMessage ? preview.WouldTruncate
            ? $"Output when this message is active: {preview.VisibleUnits} / {preview.Limit} · Will be truncated"
            : $"Output when this message is active: {preview.VisibleUnits} / {preview.Limit} · Fits"
        : !preview.OutputChanged ? $"Current output unchanged: {preview.VisibleUnits} / {preview.Limit}"
        : preview.WouldTruncate ? $"Current output after insertion: {preview.VisibleUnits} / {preview.Limit} · Will be truncated"
        : $"Current output after insertion: {preview.VisibleUnits} / {preview.Limit} · Fits";

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
