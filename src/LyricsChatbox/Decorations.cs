using System.IO;
using System.Reflection;
using System.Text.Json;

namespace LyricsChatbox;

public static class DecorationKinds
{
    public static readonly string[] All = ["Symbol", "TextArt", "Kaomoji", "Divider", "Frame", "Heart", "Music", "Status"];
    public static bool IsValid(string? kind) => kind is not null && All.Contains(kind, StringComparer.Ordinal);
}

public sealed record DecorationEntry(string Id, string Name, string Kind, string Content, bool Popular = false,
    string? Group = null, IReadOnlyList<string>? SearchTerms = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsValid => LocalContentValidation.ValidId(Id) && LocalContentValidation.ValidText(Name, 40, 1) &&
        DecorationKinds.IsValid(Kind) && LocalContentValidation.ValidText(Content, 512, 9) &&
        (Group is null || LocalContentValidation.ValidText(Group, 32, 1)) && SearchTerms is null or { Count: <= 8 } &&
        (SearchTerms is null || SearchTerms.All(term => LocalContentValidation.ValidText(term, 24, 1)) &&
            SearchTerms.Distinct(StringComparer.OrdinalIgnoreCase).Count() == SearchTerms.Count);
}

public enum DecorationCatalogStatus { Available, Missing, Oversized, Malformed, Invalid }

public sealed class DecorationCatalog
{
    public const int Version = 1;
    public const int MaximumBytes = 1024 * 1024;
    public const int MaximumEntries = 384;
    private const string ResourceName = "LyricsChatbox.Assets.Decorations.json";
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public IReadOnlyList<DecorationEntry> Items { get; }
    public DecorationCatalogStatus Status { get; }
    public bool IsAvailable => Status == DecorationCatalogStatus.Available;

    private DecorationCatalog(IReadOnlyList<DecorationEntry> items, DecorationCatalogStatus status) =>
        (Items, Status) = (items, status);

    public static DecorationCatalog LoadBuiltIn()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
        return stream is null ? Unavailable(DecorationCatalogStatus.Missing) : Load(stream);
    }

    public static DecorationCatalog Load(Stream source)
    {
        try
        {
            using var bounded = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (bounded.Length + read > MaximumBytes) return Unavailable(DecorationCatalogStatus.Oversized);
                bounded.Write(buffer, 0, read);
            }
            bounded.Position = 0;
            var file = JsonSerializer.Deserialize<CatalogFile>(bounded, Json);
            if (file is not { Version: Version, Items: { Count: <= MaximumEntries } items } ||
                items.Any(item => item is not { IsValid: true } || item.Id.StartsWith("user-", StringComparison.OrdinalIgnoreCase)) ||
                items.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != items.Count)
                return Unavailable(DecorationCatalogStatus.Invalid);
            var immutable = items.Select(item => item with
            {
                SearchTerms = item.SearchTerms is null ? null : Array.AsReadOnly(item.SearchTerms.ToArray())
            }).ToArray();
            return new(Array.AsReadOnly(immutable), DecorationCatalogStatus.Available);
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException or ArgumentException)
        {
            return Unavailable(DecorationCatalogStatus.Malformed);
        }
    }

    private static DecorationCatalog Unavailable(DecorationCatalogStatus status) => new([], status);
    private sealed record CatalogFile(int Version, IReadOnlyList<DecorationEntry>? Items);
}

public sealed record UserDecoration(string Id, string Name, string Kind, string Content)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsValid => IsUserId(Id) && LocalContentValidation.ValidText(Name, 40, 1) &&
        DecorationKinds.IsValid(Kind) && LocalContentValidation.ValidText(Content, 512, 9);

    public static UserDecoration? Create(string name, string kind, string content)
    {
        var item = new UserDecoration("user-" + Guid.NewGuid().ToString("N"), name, kind, content);
        return item.IsValid ? item : null;
    }

    internal DecorationEntry ToEntry() => new(Id, Name, Kind, Content);

    private static bool IsUserId(string? id) => id is { Length: 37 } && id.StartsWith("user-", StringComparison.Ordinal) &&
        id[5..].All(c => char.IsAsciiDigit(c) || c is >= 'a' and <= 'f');
}

public sealed record DecorationState(int Version = 1, IReadOnlyList<string>? FavoriteIds = null,
    IReadOnlyList<UserDecoration>? MyItems = null)
{
    public const int MaximumFavorites = 128;
    public const int MaximumMyItems = 64;

    public static DecorationState Empty => new(1, [], []);

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsValid => Version == 1 && FavoriteIds is { Count: <= MaximumFavorites } && MyItems is { Count: <= MaximumMyItems } &&
        FavoriteIds.All(LocalContentValidation.ValidId) && FavoriteIds.Distinct(StringComparer.Ordinal).Count() == FavoriteIds.Count &&
        MyItems.All(item => item is { IsValid: true }) && MyItems.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() == MyItems.Count;

    public DecorationState? Favorite(string id)
    {
        if (!IsValid || !LocalContentValidation.ValidId(id)) return null;
        var favorites = FavoriteIds!;
        if (favorites.Contains(id, StringComparer.Ordinal)) return this;
        return favorites.Count < MaximumFavorites ? this with { FavoriteIds = favorites.Append(id).ToArray() } : null;
    }

    public DecorationState? Unfavorite(string id) => !IsValid || !LocalContentValidation.ValidId(id)
        ? null : this with { FavoriteIds = FavoriteIds!.Where(item => item != id).ToArray() };

    public DecorationState? AddMyItem(UserDecoration item)
    {
        if (!IsValid || item is not { IsValid: true } || MyItems!.Count >= MaximumMyItems || MyItems.Any(x => x.Id == item.Id)) return null;
        return this with { MyItems = MyItems.Append(item).ToArray() };
    }

    public DecorationState? UpdateMyItem(string id, string name, string kind, string content)
    {
        if (!IsValid) return null;
        var items = MyItems!;
        if (!items.Any(item => item.Id == id)) return null;
        var updated = new UserDecoration(id, name, kind, content);
        return updated.IsValid ? this with { MyItems = items.Select(item => item.Id == id ? updated : item).ToArray() } : null;
    }

    public DecorationState? DeleteMyItem(string id)
    {
        if (!IsValid) return null;
        var items = MyItems!;
        if (!items.Any(item => item.Id == id)) return null;
        return this with
        {
            MyItems = items.Where(item => item.Id != id).ToArray(),
            FavoriteIds = FavoriteIds!.Where(favorite => favorite != id).ToArray()
        };
    }
}

public sealed class DecorationLibrary
{
    public IReadOnlyList<DecorationEntry> Items { get; }
    public IReadOnlyList<DecorationEntry> Favorites { get; }

    private DecorationLibrary(IReadOnlyList<DecorationEntry> items, IReadOnlyList<DecorationEntry> favorites) =>
        (Items, Favorites) = (items, favorites);

    public static DecorationLibrary Create(DecorationCatalog catalog, DecorationState state)
    {
        if (!state.IsValid) state = DecorationState.Empty;
        var items = catalog.Items.Concat(state.MyItems!.Select(item => item.ToEntry())).ToArray();
        var byId = items.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var favorites = state.FavoriteIds!.Where(byId.ContainsKey).Select(id => byId[id]).ToArray();
        return new(Array.AsReadOnly(items), Array.AsReadOnly(favorites));
    }

    public IReadOnlyList<DecorationEntry> Search(string? query = null, string? kind = null, bool popularOnly = false)
    {
        if (kind is not null && !DecorationKinds.All.Contains(kind, StringComparer.OrdinalIgnoreCase)) return [];
        query = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        return Items.Where(item => (kind is null || item.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase)) &&
            (!popularOnly || item.Popular) &&
            (query is null || item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             item.Content.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             item.Kind.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             item.Group?.Contains(query, StringComparison.OrdinalIgnoreCase) == true ||
             item.SearchTerms?.Any(term => term.Contains(query, StringComparison.OrdinalIgnoreCase)) == true)).ToArray();
    }
}
