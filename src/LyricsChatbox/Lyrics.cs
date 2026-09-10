using System.Globalization;
using System.Text.RegularExpressions;

namespace LyricsChatbox;

public record LyricLine(double Seconds, string Text);

public sealed class LyricTimeline
{
    public const double HoldSeconds = 10; // Conservative provisional gap cap; physical tuning remains explicit.
    public IReadOnlyList<LyricLine> Lines { get; }
    public LyricTimeline(IEnumerable<LyricLine> lines) => Lines = lines
        .Where(l => double.IsFinite(l.Seconds) && l.Seconds >= 0)
        .OrderBy(l => l.Seconds).GroupBy(l => l.Seconds)
        .Select(g => new LyricLine(g.Key, string.Join("\n", g.Select(l => l.Text).Where(t => t.Length > 0).Distinct())))
        .ToArray();

    public string Current(double position, double offset = 0) => Context(position, offset).Current;

    public LyricContext Context(double position, double offset = 0)
    {
        var time = position - offset;
        var lo = 0; var hi = Lines.Count - 1;
        while (lo <= hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (Lines[mid].Seconds <= time) lo = mid + 1; else hi = mid - 1;
        }
        if (hi < 0 || time - Lines[hi].Seconds >= HoldSeconds || Lines[hi].Text.Length == 0) return LyricContext.Empty;
        var previous = hi > 0 && Lines[hi].Seconds - Lines[hi-1].Seconds < HoldSeconds ? Lines[hi-1].Text : "";
        var next = hi+1 < Lines.Count && Lines[hi+1].Seconds - Lines[hi].Seconds < HoldSeconds ? Lines[hi+1].Text : "";
        return new(previous,Lines[hi].Text,next);
    }
}

public static partial class LrcParser
{
    public const int MaxCharacters = 512_000;
    [GeneratedRegex(@"\[([0-9]{1,3}):([0-5][0-9])(?:[.:]([0-9]{1,3}))?\]")]
    private static partial Regex Timestamp();
    [GeneratedRegex(@"^\[offset:([+-]?[0-9]{1,7})\]$", RegexOptions.IgnoreCase)]
    private static partial Regex Offset();

    public static LyricTimeline Parse(string? text)
    {
        if (string.IsNullOrEmpty(text) || text.Length > MaxCharacters) return new([]);
        var lines = new List<LyricLine>();
        var offset = 0d;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim().TrimStart('\uFEFF');
            var off = Offset().Match(line);
            if (off.Success) { offset = int.Parse(off.Groups[1].Value, CultureInfo.InvariantCulture) / 1000d; continue; }
            var matches = Timestamp().Matches(line);
            if (matches.Count == 0 || matches[0].Index != 0) continue;
            var end = 0;
            var stamps = new List<double>();
            foreach (Match match in matches)
            {
                if (match.Index != end) break;
                var fraction = match.Groups[3].Value;
                stamps.Add(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 60 +
                    int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) +
                    (fraction.Length == 0 ? 0 : int.Parse(fraction, CultureInfo.InvariantCulture) / Math.Pow(10, fraction.Length)));
                end = match.Index + match.Length;
            }
            var lyric = line[end..].Trim();
            // Enhanced/word-level timing is outside MVP; remove inline timing tags from visible text.
            lyric = Regex.Replace(lyric, @"<\d{1,3}:[0-5]\d[.:]\d{1,3}>", "");
            lines.AddRange(stamps.Select(t => new LyricLine(t, lyric)));
        }
        // Standard LRC positive offset advances the timestamps; user offset is separate and means later.
        return new(lines.Select(l => l with { Seconds = Math.Max(0, l.Seconds - offset) }));
    }
}
