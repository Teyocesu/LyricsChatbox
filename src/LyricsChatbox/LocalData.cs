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
    private string RecordingPath(string directory, string key, string extension) => Path.Combine(Root, directory, key + extension);
    private string CurrentPath(string directory, TrackIdentity track, string extension) => RecordingPath(directory, track.Key, extension);
    private string ReadablePath(string directory, TrackIdentity track, string extension)
    {
        var current = CurrentPath(directory, track, extension);
        _ = ClaimLegacyPath(directory, track, extension);
        return current;
    }
    private bool ClaimLegacyPath(string directory, TrackIdentity track, string extension)
    {
        var current = CurrentPath(directory, track, extension);
        if (File.Exists(current) || track.Key == track.LegacyKey) return true;
        var legacy = RecordingPath(directory, track.LegacyKey, extension);
        if (!File.Exists(legacy)) return true;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(current)!);
            // Rename first so one of two formerly colliding recordings can claim the legacy state,
            // while the other fails closed instead of receiving a duplicate copy.
            File.Move(legacy, current, false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return File.Exists(current); }
    }

    public string LocalLrcPath(TrackIdentity track) => CurrentPath("lyrics", track, ".lrc");
    private string CachePath(TrackIdentity track) => CurrentPath("cache", track, ".json");

    public AppSettings ReadSettings()
    {
        var settings = Read<AppSettings>(Path.Combine(Root, "settings.json"), 16_384);
        return settings is { IsValid: true } ? settings with { Appearance = settings.Appearance is null ? null : AppearanceSettings.Normalize(settings.Appearance) } : new();
    }
    public bool SaveSettings(AppSettings settings) => settings.IsValid && Write(Path.Combine(Root, "settings.json"),
        JsonSerializer.Serialize(settings with { Appearance = settings.Appearance is null ? null : AppearanceSettings.Normalize(settings.Appearance) }, Json));
    public string? ReadLocal(TrackIdentity track) => ReadText(ReadablePath("lyrics", track, ".lrc"), LrcParser.MaxCharacters * 4);
    public bool HasUsableLocalLyrics(TrackIdentity track) => LrcParser.Parse(ReadLocal(track)).Lines.Count > 0;
    public bool SaveLocal(TrackIdentity track, string lrc)
    {
        if (LrcParser.Parse(lrc).Lines.Count == 0) return false;
        if (!ClaimLegacyPath("lyrics", track, ".lrc")) return false;
        return Write(LocalLrcPath(track), lrc);
    }
    public LyricsRecord? ReadCache(TrackIdentity track) => ReadCachedLyrics(track)?.Record;
    public CachedLyrics? ReadCachedLyrics(TrackIdentity track)
    {
        var association = ReadManualAssociation(track);
        if (association is { Version: 2, Record: { } bundled, StoredUtc: { } bundledUtc } &&
            Fresh(bundledUtc) && association.Provider is "LRCLIB" or "NetEase" &&
            ManualMatching.Same(association.Metadata, bundled) && LrcParser.Parse(bundled.SyncedLyrics).Lines.Count > 0)
            return new(bundled, association.Provider, true);

        var path = ReadablePath("cache", track, ".json");
        var entry = Read<CacheEntry>(path, 2_100_000);
        if (entry is not { Version: 1, Record: not null } || !TrackKeyMatches(entry.TrackKey, track) ||
            entry.Provider is not ("LRCLIB" or "NetEase")) return null;
        if (!entry.Manual && Expired(entry.StoredUtc))
        {
            _ = DeleteIfExists(path);
            return null;
        }
        if (!Fresh(entry.StoredUtc)) return null;
        if (entry.Manual)
        {
            if (association is null || association.Provider != entry.Provider || !ManualMatching.Same(association.Metadata, entry.Record)) return null;
            var upgraded = new ManualAssociation(2, track.Key, association.Provider,
                association.Metadata with { SyncedLyrics = null }, entry.Record, entry.StoredUtc);
            if (Write(AssociationPath(track), JsonSerializer.Serialize(upgraded, Json))) _ = DeleteIfExists(path);
        }
        else if (association is not null || !LyricsMatching.Score(track, entry.Record).HasValue) return null;
        if (!entry.Manual && entry.TrackKey != track.Key)
            Write(path, JsonSerializer.Serialize(entry with { TrackKey = track.Key }, Json));
        return new(entry.Record, entry.Provider, entry.Manual);
    }
    private static bool Fresh(DateTimeOffset storedUtc) => storedUtc <= DateTimeOffset.UtcNow.AddMinutes(5) &&
        storedUtc > DateTimeOffset.UtcNow.AddDays(-30);
    private static bool Expired(DateTimeOffset storedUtc) => storedUtc <= DateTimeOffset.UtcNow.AddDays(-30);
    private static bool TrackKeyMatches(string key, TrackIdentity track) => key == track.Key || key == track.LegacyKey;
    public void SaveCache(TrackIdentity track, LyricsRecord record, string provider = "LRCLIB")
    {
        if (!ClaimLegacyPath("cache", track, ".json")) return;
        Write(CachePath(track), JsonSerializer.Serialize(new CacheEntry(1, track.Key, DateTimeOffset.UtcNow, record, provider), Json));
    }

    private string CorrectionPath(TrackIdentity track) => CurrentPath("corrections", track, ".json");
    public double? ReadCorrection(TrackIdentity track)
    {
        var path = ReadablePath("corrections", track, ".json");
        var item = Read<Correction>(path, 4096);
        if (item is not {Version: 1, Seconds: double seconds} || !TrackKeyMatches(item.TrackKey, track) ||
            !double.IsFinite(seconds) || seconds is < -5 or > 5) return null;
        if (item.TrackKey != track.Key) Write(path, JsonSerializer.Serialize(item with { TrackKey = track.Key }, Json));
        return seconds;
    }
    public bool SaveCorrection(TrackIdentity track, double seconds)
    {
        if (!double.IsFinite(seconds) || seconds is < -5 or > 5) return false;
        if (!ClaimLegacyPath("corrections", track, ".json")) return false;
        return Write(CorrectionPath(track), JsonSerializer.Serialize(new Correction(1, track.Key, seconds), Json));
    }
    public bool ResetCorrection(TrackIdentity track)
    {
        if (!ClaimLegacyPath("corrections", track, ".json")) return false;
        return DeleteIfExists(CorrectionPath(track));
    }
    public bool SaveGlobalCorrection(TrackIdentity track, AppSettings current, double seconds)
    {
        var next = current with { Offset = seconds };
        if (!next.IsValid || !SaveSettings(next)) return false;
        if (ResetCorrection(track)) return true;
        _ = SaveSettings(current);
        return false;
    }

    private string AssociationPath(TrackIdentity track) => CurrentPath("matches", track, ".json");
    public ManualAssociation? ReadManualAssociation(TrackIdentity track)
    {
        var item = Read<ManualAssociation>(ReadablePath("matches", track, ".json"), 2_100_000);
        if (item is not {Version: 1 or 2, Provider: "LRCLIB" or "NetEase"} ||
            !TrackKeyMatches(item.TrackKey, track) || !ManualMatching.Valid(item.Metadata)) return null;
        if (item.Version == 2 && (item.Record is null || item.StoredUtc is null ||
            !ManualMatching.Same(item.Metadata, item.Record) || LrcParser.Parse(item.Record.SyncedLyrics).Lines.Count == 0)) return null;
        return item.TrackKey == track.Key ? item : item with { TrackKey = track.Key };
    }
    public bool SaveManualAssociation(TrackIdentity track,ManualCandidate choice,LyricsRecord record)
    {
        if(choice.Provider is not ("LRCLIB" or "NetEase") || !ManualMatching.Same(choice.Metadata,record) || LrcParser.Parse(record.SyncedLyrics).Lines.Count==0) return false;
        if (!ClaimLegacyPath("matches", track, ".json")) return false;
        // Version 2 is one atomically replaced bundle. A failed rename leaves both the previous
        // association and its synchronized body intact; the unrelated automatic cache is untouched.
        var bundle = new ManualAssociation(2, track.Key, choice.Provider, record with { SyncedLyrics = null },
            record, DateTimeOffset.UtcNow);
        return Write(AssociationPath(track), JsonSerializer.Serialize(bundle, Json));
    }
    public bool ForgetManualAssociation(TrackIdentity track)
    {
        if (!ClaimLegacyPath("matches", track, ".json")) return false;
        return DeleteIfExists(AssociationPath(track));
    }

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
    private static bool DeleteIfExists(string path)
    {
        try
        {
            File.Delete(path);
            return !File.Exists(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
    private record CacheEntry(int Version, string TrackKey, DateTimeOffset StoredUtc, LyricsRecord Record, string Provider = "LRCLIB", bool Manual = false);
    public record CachedLyrics(LyricsRecord Record, string Provider, bool Manual = false);
    private record Correction(int Version, string TrackKey, double? Seconds);
}
