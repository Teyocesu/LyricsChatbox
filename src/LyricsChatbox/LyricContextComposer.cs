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
        var previous = mode is "Previous + current + next" or "Adaptive" ? Clean(context.Previous) : "";
        var next = mode is "Current + next" or "Previous + current + next" or "Adaptive" ? Clean(context.Next) : "";
        string Assemble() => string.Join("\n", new[] {metadata, previous, previous.Length>0 ? "› " + current : current, next}.Where(s=>s.Length>0));
        static bool Fits(string text, bool floating) => text.Length <= (floating ? 142 : 144) && text.Count(c=>c=='\n') < 9;
        // Drop semantic pieces in priority order. Current is never shortened to make room for context.
        if (!Fits(Assemble(),compact)) metadata="";
        if (!Fits(Assemble(),compact)) previous="";
        if (!Fits(Assemble(),compact)) next="";
        return ChatboxFormatter.Visible(ChatboxFormatter.Format(Assemble(),compact));
    }
}
