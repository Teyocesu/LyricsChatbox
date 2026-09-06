using System.Text;
using System.Text.RegularExpressions;

namespace LyricsChatbox;

public record LyricsRecord(long Id, string TrackName, string ArtistName, string AlbumName,
    double Duration, bool Instrumental, string? SyncedLyrics);
public record MatchResult(LyricsRecord? Record, bool Ambiguous);

public static partial class LyricsMatching
{
    public static string Normalize(string? input)
    {
        var value = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(input ?? "")).Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        value = Regex.Replace(value, @"\b(featuring|feat|ft)\b\.?\s*", " feat ");
        value = value.Replace("’", "").Replace("'", "").Replace("`", "");
        value = Regex.Replace(value, @"[^\p{L}\p{N}\p{M}]+", " ");
        return Regex.Replace(value, @"\s+", " ").Trim();
    }

    private static string Versions(string title, string album)
    {
        var text = Normalize(title + " " + album);
        return string.Join("|", new[] { "live", "remix", "acoustic", "karaoke", "instrumental", "sped up", "slowed", "remaster", "clean", "explicit", "radio edit" }
            .Where(v => Regex.IsMatch(text, @"\b" + v + @"(?:ed)?\b")));
    }

    public static double? Score(TrackIdentity track, LyricsRecord? candidate)
    {
        if (candidate is null || candidate.Duration <= 0 || string.IsNullOrWhiteSpace(candidate.TrackName) ||
            string.IsNullOrWhiteSpace(candidate.ArtistName) || string.IsNullOrWhiteSpace(track.Title) || string.IsNullOrWhiteSpace(track.Artist) ||
            !double.IsFinite(track.Duration) || track.Duration <= 0 || !double.IsFinite(candidate.Duration) ||
            Math.Abs(track.Duration - candidate.Duration) > 2) return null;
        // Deliberately no fuzzy edit-distance: punctuation equivalence is allowed, recording annotations survive.
        var title = Normalize(track.Title);
        var artist = Normalize(track.Artist);
        if (title.Length == 0 || artist.Length == 0 || title != Normalize(candidate.TrackName) || artist != Normalize(candidate.ArtistName)) return null;
        if (Versions(track.Title, track.Album) != Versions(candidate.TrackName, candidate.AlbumName)) return null;
        return 100 - Math.Abs(track.Duration - candidate.Duration) +
            (!string.IsNullOrWhiteSpace(track.Album) && Normalize(track.Album) == Normalize(candidate.AlbumName) ? 10 : 0);
    }

    public static MatchResult Choose(TrackIdentity track, IEnumerable<LyricsRecord> candidates)
    {
        var scored = candidates.Select(c => (Record: c, Score: Score(track, c)))
            .Where(x => x.Score.HasValue).OrderByDescending(x => x.Score).ToArray();
        if (scored.Length == 0) return new(null, false);
        var best = scored[0];
        // Equivalent duplicates can share a result, but conflicting timed content within the score margin cannot.
        if (scored.Skip(1).Any(x => best.Score - x.Score < 3 &&
            (x.Record.Instrumental != best.Record.Instrumental || x.Record.SyncedLyrics != best.Record.SyncedLyrics)))
            return new(null, true);
        return new(best.Record, false);
    }
}
