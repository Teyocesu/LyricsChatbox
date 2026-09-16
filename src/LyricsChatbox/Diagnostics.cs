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
    public string Export(PlaybackSnapshot? snapshot, AppSettings settings, string lyricsStatus, string oscStatus, double effectiveOffset,
        bool manualMode = false, string? applicationStatus = null, PlaybackSourceKind? selectedSource = null,
        string? sourceStatus = null, bool sourceAmbiguous = false, bool outputPaused = false,
        string? pauseMode = null, string? pauseStatus = null)
    {
        DiagnosticEvent[] recent;
        lock (events) recent = events.ToArray();
        var appleSnapshot = snapshot?.Source == PlaybackSourceKind.AppleMusic ? snapshot : null;
        // Explicit whitelist: never serialize AppSettings, Timeline, controller or HTTP objects.
        var report = new
        {
            Application = "LyricsChatbox",
            Version = typeof(App).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion,
            Windows = Environment.OSVersion.VersionString,
            Runtime = RuntimeInformation.FrameworkDescription,
            ApplicationStatus = Clean(applicationStatus),
            PlaybackSource = new { Configured = PlaybackSourceSetting.Normalize(settings.PlaybackSource),
                Selected = selectedSource?.ToString(), Status = Clean(sourceStatus), Ambiguous = sourceAmbiguous },
            Music = new { SessionPresent = snapshot is not null, Source = snapshot?.Source.ToString(),
                RawTitle = Clean(snapshot?.RawTitle), RawArtist = Clean(snapshot?.RawArtist), RawAlbum = Clean(snapshot?.RawAlbum),
                Title = Clean(snapshot?.Track.Title), Artist = Clean(snapshot?.Track.Artist), Album = Clean(snapshot?.Track.Album),
                Duration = snapshot?.Track.Duration, Position = snapshot?.Position, State = snapshot?.State.ToString() },
            AppleMusic = new
            {
                SessionPresent = appleSnapshot is not null,
                Source = appleSnapshot is null ? null : AppleMusicPlayback.AppleSource,
                RawTitle = Clean(appleSnapshot?.RawTitle),
                RawArtist = Clean(appleSnapshot?.RawArtist),
                RawAlbum = Clean(appleSnapshot?.RawAlbum),
                Title = Clean(appleSnapshot?.Track.Title),
                Artist = Clean(appleSnapshot?.Track.Artist),
                Album = Clean(appleSnapshot?.Track.Album),
                NormalizedTitle = Clean(LyricsMatching.Normalize(appleSnapshot?.Track.Title)),
                NormalizedArtist = Clean(LyricsMatching.Normalize(appleSnapshot?.Track.Artist)),
                Duration = appleSnapshot?.Track.Duration,
                Position = appleSnapshot?.Position,
                State = appleSnapshot?.State.ToString()
            },
            Lyrics = new { Status = Clean(lyricsStatus), EffectiveOffset = effectiveOffset },
            Output = new { settings.Enabled, State = !settings.Enabled ? "Disabled" : outputPaused ? "Paused" : "Enabled",
                Paused = outputPaused, PauseMode = outputPaused ? Clean(pauseMode) : null,
                PauseStatus = outputPaused ? Clean(pauseStatus) : null, Mode = manualMode ? "Manual" : "Automatic",
                settings.Compact, settings.Host, settings.Port, settings.Preset, Status = Clean(oscStatus) },
            ApplicationBehavior = new { settings.StartWithWindows, settings.StartMinimized, settings.MinimizeToTray, settings.CloseToTray, settings.AutomaticUpdateChecks },
            RecentEvents = recent,
            Privacy = "Generated on demand. No lyric bodies, custom messages, drafts, credentials or persistent listening history."
        };
        return JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
        });
    }
}
