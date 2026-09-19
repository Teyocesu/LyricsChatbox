namespace LyricsChatbox;

public record LyricContext(string Previous, string Current, string Next)
{
    public static readonly LyricContext Empty = new("","","");
}

public static class LyricContextComposer
{
    public static readonly string[] Modes = ["Current only", "Current + next", "Previous + current + next", "Adaptive"];
    public static bool SupportsContext(string preset, string customTemplate) =>
        ChatboxComposer.Template(preset, customTemplate).Contains("{lyrics}", System.StringComparison.Ordinal);
    public static string ComposeProfile(LyricContext context, TrackIdentity? track, string preset, string customTemplate,
        string message, string mode, bool compact, DateTimeOffset localTime = default, double? position = null)
    {
        static string Clean(string text) => ChatboxFormatter.CleanText(text).Trim();
        var template = ChatboxComposer.Template(preset, customTemplate);
        string WithLyrics(string lyrics) => ChatboxComposer.Compose(template, track, lyrics, message, localTime, position);
        static bool Fits(string text, bool floating) => text.Length <= (floating ? 142 : 144) && text.Count(c => c == '\n') < 9;
        var current = Clean(context.Current);
        if (current.Length == 0) return WithLyrics("");
        string Lyrics(string? previous, string? next)
        {
            var lines = new System.Collections.Generic.List<string>();
            if (previous is { Length: > 0 }) { lines.Add(previous); lines.Add("› " + current); }
            else lines.Add(current);
            if (next is { Length: > 0 }) lines.Add(next);
            return string.Join("\n", lines);
        }
        var previous = Clean(context.Previous);
        var next = Clean(context.Next);
        var options = mode switch
        {
            "Current + next" => new (string?, string?)[] { (null, next), (null, null) },
            "Previous + current + next" or "Adaptive" => new (string?, string?)[] { (previous, next), (null, next), (previous, null), (null, null) },
            _ => new (string?, string?)[] { (null, null) },
        };
        foreach (var (before, after) in options)
        {
            var composed = WithLyrics(Lyrics(before, after));
            if (Fits(composed, compact)) return composed;
        }
        return WithLyrics(current);
    }
}
