using System.Globalization;
using System.Text.RegularExpressions;

namespace LyricsChatbox;

public static class DurationFormatter
{
    public static string Format(double? seconds)
    {
        if (seconds is not double value || !double.IsFinite(value) || value < 0 || value > 86400) return "";
        var wholeSeconds = (long)value;
        if (value < 3600) return TimeSpan.FromSeconds(wholeSeconds).ToString(@"m\:ss", CultureInfo.InvariantCulture);
        return $"{wholeSeconds / 3600}:{wholeSeconds % 3600 / 60:00}:{wholeSeconds % 60:00}";
    }
}

public record RecoveryPresentation(bool Visible, string Title, string Hint, bool Retry, bool Search, bool Import, bool Ignore, bool Resume);

public static partial class PresentationText
{
    public const string EmptyPreview = "Nothing to send";

    public static string AppleMusicStatus(PlaybackSnapshot? snapshot, string status) => snapshot is not null
        ? "Apple Music · " + snapshot.State.ToString().ToLowerInvariant()
        : status switch
        {
            "Multiple Apple Music sessions · waiting" => "Apple Music · multiple sessions open",
            "Apple Music unavailable · reconnecting" => "Apple Music · reconnecting",
            "Checking Apple Music session" or "Reading Apple Music" or "Updating track" or "Refreshing playback after resume" => "Apple Music · reconnecting",
            _ => "Apple Music · not detected"
        };

    public static RecoveryPresentation Recovery(bool hasTrack, bool hasTimeline, bool ignored, LyricsOutcome? outcome, bool lookingUp)
    {
        if (!hasTrack) return new(false, "", "", false, false, false, false, false);
        if (ignored) return new(true, "Lyrics paused for this song", "Resume when you want LyricsChatbox to find lyrics again.", false, false, false, false, true);
        if (hasTimeline || lookingUp || outcome is LyricsOutcome.Found or LyricsOutcome.Instrumental)
            return new(false, "", "", false, false, false, false, false);
        return outcome switch
        {
            LyricsOutcome.NotFound => new(true, "No synchronized lyrics found", "Try again, choose another recording, or import an LRC file.", true, true, true, true, false),
            LyricsOutcome.Ambiguous or LyricsOutcome.Rejected => new(true, "No confident match found", "Choose the correct recording or import an LRC file.", true, true, true, true, false),
            LyricsOutcome.RateLimited => new(true, "Lyrics are temporarily busy", "Try again in a moment or import an LRC file.", true, true, true, true, false),
            LyricsOutcome.Timeout or LyricsOutcome.Unavailable => new(true, "Lyrics are temporarily unavailable", "Try again or import an LRC file.", true, true, true, true, false),
            _ => new(false, "", "", false, false, false, false, false)
        };
    }

    public static (string Text, bool IsPlaceholder) Preview(string visiblePayload) => string.IsNullOrEmpty(visiblePayload)
        ? (EmptyPreview, true) : (visiblePayload, false);

    public static string ReleaseNotes(string? markdown, int limit = 6000)
    {
        if (string.IsNullOrEmpty(markdown) || limit <= 0) return "";
        var source = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        var output = new List<string>();
        foreach (var raw in source.Split('\n'))
        {
            var line = raw.TrimEnd();
            line = Heading().Replace(line, "$1");
            line = Bullet().Replace(line, "• ");
            line = Link().Replace(line, static match =>
            {
                var label = match.Groups[1].Value;
                var target = match.Groups[2].Value;
                return Uri.TryCreate(target, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
                    ? $"{label} ({uri.AbsoluteUri})" : label;
            });
            line = line.Replace("**", "").Replace("__", "");
            output.Add(line);
        }
        var text = string.Join("\n", output).Trim();
        if (text.Length <= limit) return text;
        const string suffix = "\nRead the complete release notes on GitHub.";
        var keep = Math.Max(0, limit - suffix.Length);
        return text[..keep].TrimEnd() + suffix;
    }

    [GeneratedRegex(@"^\s{0,3}#{1,6}\s+(.*)$")]
    private static partial Regex Heading();
    [GeneratedRegex(@"^\s*[-*+]\s+")]
    private static partial Regex Bullet();
    [GeneratedRegex(@"\[([^\]\r\n]+)\]\(([^)\s]+)\)")]
    private static partial Regex Link();
}
