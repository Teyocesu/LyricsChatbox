using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LyricsChatbox;

public enum DiagnosticCategory { Playback, Lyrics, OSC, Lifecycle, Settings, Update }
public record DiagnosticEvent(DateTimeOffset Utc, DiagnosticCategory Category, string Message);
public sealed class Diagnostics
{
    private readonly Queue<DiagnosticEvent> events = new();
    public const int Capacity = 100;
    public void Add(DiagnosticCategory category, string message)
    {
        lock (events)
        {
            if (events.Count == Capacity) events.Dequeue();
            events.Enqueue(new(DateTimeOffset.UtcNow, category, Clean(message)));
        }
    }
    public static string Clean(string? text)
    {
        text = Regex.Replace(text ?? "", @"(?:gh[pousr]_[A-Za-z0-9_]+|github_pat_[A-Za-z0-9_]+|Bearer\s+\S+|(?:password|api[_-]?key|token)\s*[:=]\s*\S+)", "[redacted]", RegexOptions.IgnoreCase);
        text = new string(text.Where(c => !char.IsControl(c)).Take(512).ToArray());
        return text;
    }
    public string Export(PlaybackSnapshot? snapshot, AppSettings settings, string lyricsStatus, string oscStatus, double effectiveOffset)
    {
        DiagnosticEvent[] recent;
        lock (events) recent = events.ToArray();
        // Explicit whitelist: never serialize AppSettings, Timeline, controller or HTTP objects.
        var report = new
        {
            Application = "LyricsChatbox",
            Version = typeof(App).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion,
            Windows = Environment.OSVersion.VersionString,
            Runtime = RuntimeInformation.FrameworkDescription,
            AppleMusic = new
            {
                SessionPresent = snapshot is not null,
                Source = snapshot is null ? null : AppleMusicPlayback.AppleSource,
                RawTitle = Clean(snapshot?.RawTitle),
                RawArtist = Clean(snapshot?.RawArtist),
                RawAlbum = Clean(snapshot?.RawAlbum),
                Title = Clean(snapshot?.Track.Title),
                Artist = Clean(snapshot?.Track.Artist),
                Album = Clean(snapshot?.Track.Album),
                NormalizedTitle = Clean(LyricsMatching.Normalize(snapshot?.Track.Title)),
                NormalizedArtist = Clean(LyricsMatching.Normalize(snapshot?.Track.Artist)),
                Duration = snapshot?.Track.Duration,
                Position = snapshot?.Position,
                State = snapshot?.State.ToString()
            },
            Lyrics = new { Status = Clean(lyricsStatus), EffectiveOffset = effectiveOffset },
            Output = new { settings.Enabled, settings.Compact, settings.Host, settings.Port, settings.Preset, Status = Clean(oscStatus) },
            ApplicationBehavior = new { settings.StartWithWindows, settings.StartMinimized, settings.MinimizeToTray, settings.CloseToTray, settings.AutomaticUpdateChecks },
            RecentEvents = recent,
            Privacy = "Generated on demand. No lyric bodies, custom messages, drafts, credentials or persistent listening history."
        };
        return JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
        });
    }
}
