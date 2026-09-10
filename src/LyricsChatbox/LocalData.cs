using System.IO;
using System.Net;
using System.Text.Json;

namespace LyricsChatbox;

public record AppSettings(bool Enabled = false, double Offset = 0, string Host = "127.0.0.1", int Port = 9000,
    string Preset = "Lyrics Only", string CustomTemplate = "{lyrics}", string Message = "",
    bool Compact = false, bool TypingIndicator = false, bool LiveEdit = false,
    string CustomAlignment = "Left", string ManualAlignment = "Left",
    bool StartWithWindows = false, bool StartMinimized = false, bool MinimizeToTray = false,
    bool CloseToTray = false, bool AutomaticUpdateChecks = false, AppearanceSettings? Appearance = null,
    bool? AutoDiscoverOsc = null, string? SkippedUpdateVersion = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsValid => double.IsFinite(Offset) && Offset is >= -5 and <= 5 && Port is >= 1 and <= 65535 &&
        (Host == "localhost" || IPAddress.TryParse(Host, out _)) && ChatboxComposer.Presets.Contains(Preset) &&
        CustomTemplate is { Length: <= 512 } && Message is { Length: <= 512 } &&
        MessageLayout.Alignments.Contains(CustomAlignment) && MessageLayout.Alignments.Contains(ManualAlignment);
}

public sealed partial class LocalData(string root)
{
    public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LyricsChatbox");
    public string Root { get; } = root;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    public string LocalLrcPath(TrackIdentity track) => Path.Combine(Root, "lyrics", track.Key + ".lrc");
    private string CachePath(TrackIdentity track) => Path.Combine(Root, "cache", track.Key + ".json");

    public AppSettings ReadSettings()
    {
        var settings = Read<AppSettings>(Path.Combine(Root, "settings.json"), 16_384);
        return settings is { IsValid: true } ? settings with { Appearance = settings.Appearance is null ? null : AppearanceSettings.Normalize(settings.Appearance) } : new();
    }
    public bool SaveSettings(AppSettings settings) => settings.IsValid && Write(Path.Combine(Root, "settings.json"),
        JsonSerializer.Serialize(settings with { Appearance = settings.Appearance is null ? null : AppearanceSettings.Normalize(settings.Appearance) }, Json));
    public string? ReadLocal(TrackIdentity track) => ReadText(LocalLrcPath(track), LrcParser.MaxCharacters * 4);
    public bool SaveLocal(TrackIdentity track, string lrc) => LrcParser.Parse(lrc).Lines.Count > 0 && Write(LocalLrcPath(track), lrc);
    public LyricsRecord? ReadCache(TrackIdentity track) => ReadCachedLyrics(track)?.Record;
    public CachedLyrics? ReadCachedLyrics(TrackIdentity track)
    {
        var entry = Read<CacheEntry>(CachePath(track), 2_100_000);
        return entry is { Version: 1, Record: not null } && entry.TrackKey == track.Key &&
            entry.StoredUtc <= DateTimeOffset.UtcNow.AddMinutes(5) && entry.StoredUtc > DateTimeOffset.UtcNow.AddDays(-30) &&
            (entry.Manual ? ReadManualAssociation(track) is { } manual && manual.Provider == entry.Provider && ManualMatching.Same(manual.Metadata,entry.Record)
                : LyricsMatching.Score(track, entry.Record).HasValue) && entry.Provider is "LRCLIB" or "NetEase"
            ? new(entry.Record, entry.Provider, entry.Manual) : null;
    }
    public void SaveCache(TrackIdentity track, LyricsRecord record, string provider = "LRCLIB") => Write(CachePath(track),
        JsonSerializer.Serialize(new CacheEntry(1, track.Key, DateTimeOffset.UtcNow, record, provider), Json));

    private string CorrectionPath(TrackIdentity track) => Path.Combine(Root, "corrections", track.Key + ".json");
    public double? ReadCorrection(TrackIdentity track)
    {
        var item = Read<Correction>(CorrectionPath(track), 4096);
        return item is {Version: 1, Seconds: double seconds} && item.TrackKey == track.Key &&
            double.IsFinite(seconds) && seconds is >= -5 and <= 5 ? seconds : null;
    }
    public bool SaveCorrection(TrackIdentity track, double seconds) => double.IsFinite(seconds) && seconds is >= -5 and <= 5 &&
        Write(CorrectionPath(track), JsonSerializer.Serialize(new Correction(1, track.Key, seconds), Json));
    public bool ResetCorrection(TrackIdentity track) =>
        Write(CorrectionPath(track), JsonSerializer.Serialize(new Correction(1, track.Key, null), Json));

    private string AssociationPath(TrackIdentity track) => Path.Combine(Root,"matches",track.Key+".json");
    public ManualAssociation? ReadManualAssociation(TrackIdentity track)
    {
        var item = Read<ManualAssociation>(AssociationPath(track),16384);
        return item is {Version:1, Provider:"LRCLIB" or "NetEase"} && item.TrackKey==track.Key && ManualMatching.Valid(item.Metadata) ? item : null;
    }
    public bool SaveManualAssociation(TrackIdentity track,ManualCandidate choice,LyricsRecord record)
    {
        if(choice.Provider is not ("LRCLIB" or "NetEase") || !ManualMatching.Same(choice.Metadata,record) || LrcParser.Parse(record.SyncedLyrics).Lines.Count==0) return false;
        // Reuse the one existing cache body; the mapping stores metadata only.
        // Commit consent last: an interrupted cache write must not install a new association.
        if(!Write(CachePath(track),JsonSerializer.Serialize(new CacheEntry(1,track.Key,DateTimeOffset.UtcNow,record,choice.Provider,true),Json))) return false;
        return Write(AssociationPath(track),JsonSerializer.Serialize(new ManualAssociation(1,track.Key,choice.Provider,record with {SyncedLyrics=null}),Json));
    }
    public bool ForgetManualAssociation(TrackIdentity track) => Write(AssociationPath(track),"null");

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
    private record CacheEntry(int Version, string TrackKey, DateTimeOffset StoredUtc, LyricsRecord Record, string Provider = "LRCLIB", bool Manual = false);
    public record CachedLyrics(LyricsRecord Record, string Provider, bool Manual = false);
    private record Correction(int Version, string TrackKey, double? Seconds);
}
