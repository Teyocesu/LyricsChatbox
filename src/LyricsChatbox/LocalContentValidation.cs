namespace LyricsChatbox;

internal static class LocalContentValidation
{
    public static bool ValidId(string? id) => id is { Length: > 0 and <= 64 } &&
        id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');

    public static bool ValidText(string? text, int maximumLength, int maximumLines)
    {
        if (text is not { Length: > 0 } || text.Length > maximumLength || string.IsNullOrWhiteSpace(text)) return false;
        var lines = 1;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\n')
            {
                if (++lines > maximumLines) return false;
                continue;
            }
            if (char.IsControl(c)) return false;
            if (char.IsHighSurrogate(c))
            {
                if (++i >= text.Length || !char.IsLowSurrogate(text[i])) return false;
            }
            else if (char.IsLowSurrogate(c)) return false;
        }
        return true;
    }
}
