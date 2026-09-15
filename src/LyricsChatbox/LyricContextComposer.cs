namespace LyricsChatbox;

public record LyricContext(string Previous, string Current, string Next)
{
    public static readonly LyricContext Empty = new("","","");
}

public static class LyricContextComposer
{
    public static readonly string[] Modes = ["Current only", "Current + next", "Previous + current + next", "Adaptive"];
    public static string Compose(LyricContext context, TrackIdentity? track, string preset, string mode, bool compact)
    {
        static string Clean(string text) => ChatboxFormatter.CleanText(text).Trim();
        var current = Clean(context.Current);
        var metadata = preset == "Song + Lyrics" ? Clean(ChatboxComposer.Compose("♫ {title} — {artist}",track,"","",DateTimeOffset.MinValue,null)) : "";
        if (current.Length == 0) return ChatboxFormatter.Visible(ChatboxFormatter.Format(metadata,compact));
        var previous = mode == "Previous + current + next" ? Clean(context.Previous) : "";
        var next = mode is "Current + next" or "Previous + current + next" ? Clean(context.Next) : "";
        string Assemble(string heading, string before, string after) => string.Join("\n", new[] {heading, before, before.Length>0 ? "› " + current : current, after}.Where(s=>s.Length>0));
        static bool Fits(string text, bool floating) => text.Length <= (floating ? 142 : 144) && text.Count(c=>c=='\n') < 9;

        if (mode == "Adaptive")
        {
            var adaptiveNext = Clean(context.Next);
            var withNext = Assemble("", "", adaptiveNext);
            if (adaptiveNext.Length > 0 && Fits(withNext, compact))
            {
                var adaptivePrevious = Clean(context.Previous);
                var withBoth = Assemble("", adaptivePrevious, adaptiveNext);
                return ChatboxFormatter.Visible(ChatboxFormatter.Format(
                    adaptivePrevious.Length > 0 && Fits(withBoth, compact) ? withBoth : withNext, compact));
            }
            var withMetadata = Assemble(metadata, "", "");
            return ChatboxFormatter.Visible(ChatboxFormatter.Format(
                metadata.Length > 0 && Fits(withMetadata, compact) ? withMetadata : current, compact));
        }

        string Selected() => Assemble(metadata, previous, next);
        // Drop semantic pieces in priority order. Current is never shortened to make room for context.
        if (!Fits(Selected(),compact)) metadata="";
        if (!Fits(Selected(),compact)) previous="";
        if (!Fits(Selected(),compact)) next="";
        return ChatboxFormatter.Visible(ChatboxFormatter.Format(Selected(),compact));
    }
}
