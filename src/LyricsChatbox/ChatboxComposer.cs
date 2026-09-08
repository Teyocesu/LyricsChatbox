using System.Globalization;
using System.Text.RegularExpressions;

namespace LyricsChatbox;

public static class ChatboxComposer
{
    public static readonly string[] Presets = ["Lyrics Only", "Song + Lyrics", "Status / Time", "Custom"];
    public static string Template(string preset, string custom) => preset switch
    {
        "Song + Lyrics" => "♫ {title} — {artist}\n{lyrics}",
        "Status / Time" => "{message}\n{time}",
        "Custom" => custom,
        _ => "{lyrics}"
    };
    public static string Time(double? seconds) => seconds is double s && double.IsFinite(s) && s >= 0 && s <= 86400
        ? TimeSpan.FromSeconds(s).ToString(s >= 3600 ? @"h\:mm\:ss" : @"m\:ss", CultureInfo.InvariantCulture) : "";

    public static string Compose(string? template, TrackIdentity? track, string lyrics, string message,
        DateTimeOffset localTime, double? position, bool preserveLayout = false)
    {
        if (template is null || template.Length > 512) return "";
        var values = new Dictionary<string, string>
        {
            ["lyrics"] = lyrics, ["title"] = track?.Title ?? "", ["artist"] = track?.Artist ?? "",
            ["album"] = track?.Album ?? "", ["time"] = localTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            ["message"] = message, ["elapsed"] = Time(position), ["duration"] = Time(track?.Duration)
        };
        var lines = new List<string>();
        foreach (var line in template.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var missing = false;
            var populated = false;
            var hadToken = false;
            // Single pass: braces supplied inside lyrics or a manual status stay literal.
            var rendered = Regex.Replace(line, @"\{([^{}\r\n]*)\}", m =>
            {
                hadToken = true;
                var value = values.GetValueOrDefault(m.Groups[1].Value, "");
                missing |= value.Length == 0;
                populated |= value.Length > 0;
                return value;
            });
            if (hadToken && !populated) continue;
            if (missing)
            {
                rendered = Regex.Replace(rendered, @"([—–|·:/-])\s*([—–|·:/-])", "$1");
                rendered = rendered.Trim().Trim(' ', '—', '–', '|', '·', ':', '/', '-', '♫').Trim();
            }
            if (!string.IsNullOrWhiteSpace(rendered)) lines.Add(preserveLayout ? rendered : rendered.Trim());
            else if (preserveLayout && !hadToken) lines.Add("");
        }
        return string.Join("\n", lines).Trim('\n');
    }
}
