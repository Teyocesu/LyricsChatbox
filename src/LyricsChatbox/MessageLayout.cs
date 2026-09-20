using System.Globalization;

namespace LyricsChatbox;

public static class MessageLayout
{
    public static readonly string[] Alignments = ["Left", "Center", "Right"];

    // OSC has no alignment command. Ordinary spaces approximate columns; never inject rich-text tags.
    public static string Align(string text, string alignment)
    {
        if (alignment is not ("Center" or "Right") || string.IsNullOrWhiteSpace(text)) return text;
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var widths = lines.Select(line => StringInfo.ParseCombiningCharacters(line.TrimEnd()).Length).ToArray();
        var columns = Math.Clamp(widths.Max(), 24, 48);
        return string.Join("\n", lines.Select((line, index) =>
        {
            if (string.IsNullOrWhiteSpace(line)) return "";
            var spaces = Math.Max(0, columns - widths[index]);
            if (alignment == "Center") spaces /= 2;
            return new string(' ', spaces) + line.TrimEnd();
        }));
    }
}
