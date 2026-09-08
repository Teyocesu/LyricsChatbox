using System.IO;
using System.Net;
using System.Text.Json;

namespace LyricsChatbox;

public record AppSettings(bool Enabled = false, double Offset = 0, string Host = "127.0.0.1", int Port = 9000,
    string Preset = "Lyrics Only", string CustomTemplate = "{lyrics}", string Message = "",
    bool Compact = false, bool TypingIndicator = false, bool LiveEdit = false,
    string CustomAlignment = "Left", string ManualAlignment = "Left")
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsValid => double.IsFinite(Offset) && Offset is >= -5 and <= 5 && Port is >= 1 and <= 65535 &&
        (Host == "localhost" || IPAddress.TryParse(Host, out _)) && ChatboxComposer.Presets.Contains(Preset) &&
        CustomTemplate is { Length: <= 512 } && Message is { Length: <= 512 } &&
        MessageLayout.Alignments.Contains(CustomAlignment) && MessageLayout.Alignments.Contains(ManualAlignment);
}

public sealed class LocalData(string root)
{
    public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LyricsChatbox");
    public string Root { get; } = root;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    public string LocalLrcPath(TrackIdentity track) => Path.Combine(Root, "lyrics", track.Key + ".lrc");
    private string CachePath(TrackIdentity track) => Path.Combine(Root, "cache", track.Key + ".json");

    public AppSettings ReadSettings()
    {
        var settings = Read<AppSettings>(Path.Combine(Root, "settings.json"), 16_384);
        return settings is { IsValid: true } ? settings : new();
    }
    public bool SaveSettings(AppSettings settings) => settings.IsValid && Write(Path.Combine(Root, "settings.json"), JsonSerializer.Serialize(settings, Json));
    public string? ReadLocal(TrackIdentity track) => ReadText(LocalLrcPath(track), LrcParser.MaxCharacters * 4);
    public bool SaveLocal(TrackIdentity track, string lrc) => LrcParser.Parse(lrc).Lines.Count > 0 && Write(LocalLrcPath(track), lrc);
    public LyricsRecord? ReadCache(TrackIdentity track) => ReadCachedLyrics(track)?.Record;
    public CachedLyrics? ReadCachedLyrics(TrackIdentity track)
    {
        var entry = Read<CacheEntry>(CachePath(track), 2_100_000);
        return entry is { Version: 1, Record: not null } && entry.TrackKey == track.Key &&
            entry.StoredUtc <= DateTimeOffset.UtcNow.AddMinutes(5) && entry.StoredUtc > DateTimeOffset.UtcNow.AddDays(-30) &&
            LyricsMatching.Score(track, entry.Record).HasValue && entry.Provider is "LRCLIB" or "NetEase"
            ? new(entry.Record, entry.Provider) : null;
    }
    public void SaveCache(TrackIdentity track, LyricsRecord record, string provider = "LRCLIB") => Write(CachePath(track),
        JsonSerializer.Serialize(new CacheEntry(1, track.Key, DateTimeOffset.UtcNow, record, provider), Json));

    private static T? Read<T>(string path, int max)
    {
        try { var text = ReadText(path, max); return text is null ? default : JsonSerializer.Deserialize<T>(text, Json); }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException) { return default; }
    }
    private static string? ReadText(string path, int max)
    {
        try
        {
            using var stream = File.OpenRead(path);
            if (stream.Length > max) return null;
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true);
            var chars = new char[max + 1];
            var count = reader.ReadBlock(chars, 0, chars.Length);
            return count > max ? null : new string(chars, 0, count);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }
    private static bool Write(string path, string text)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(temp, text, new System.Text.UTF8Encoding(false));
            File.Move(temp, path, true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
        finally { try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
    private record CacheEntry(int Version, string TrackKey, DateTimeOffset StoredUtc, LyricsRecord Record, string Provider = "LRCLIB");
    public record CachedLyrics(LyricsRecord Record, string Provider);
}
