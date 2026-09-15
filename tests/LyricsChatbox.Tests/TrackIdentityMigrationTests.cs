using System.Text.Json;

namespace LyricsChatbox.Tests;

public sealed class TrackIdentityMigrationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "LyricsChatbox.Tests", Guid.NewGuid().ToString("N"));
    private static readonly TrackIdentity First = new("Same song", "Same artist", "Same album", 120.1);
    private static readonly TrackIdentity Second = First with { Duration = 120.4 };
    private static readonly LyricsRecord Automatic = new(10, First.Title, First.Artist, First.Album, First.Duration, false, "[00:01]automatic");
    private static readonly LyricsRecord Manual = new(11, "Same song (Live)", First.Artist, "Live album", 121, false, "[00:01]manual");

    [Fact]
    public void PreciseKeysSeparateDurationsThatCollidedInV05()
    {
        Assert.Equal(First.LegacyKey, Second.LegacyKey);
        Assert.NotEqual(First.Key, Second.Key);
        Assert.Equal(First.Key, new TrackIdentity(First.Title, First.Artist, First.Album, First.Duration).Key);
        Assert.NotEqual(First.Key, (First with { Artist = "Another artist" }).Key);
    }

    [Fact]
    public void LegacyFilesAreClaimedMigratedAndNotCopiedToACollidingRecording()
    {
        WriteLegacy("lyrics", ".lrc", "[00:01]legacy local");
        WriteLegacy("corrections", ".json", JsonSerializer.Serialize(new { Version = 1, TrackKey = First.LegacyKey, Seconds = .7 }));
        WriteLegacy("ignored", ".json", JsonSerializer.Serialize(new { Version = 1, TrackKey = First.LegacyKey, Ignored = true }));
        WriteLegacy("cache", ".json", JsonSerializer.Serialize(new
        {
            Version = 1, TrackKey = First.LegacyKey, StoredUtc = DateTimeOffset.UtcNow,
            Record = Automatic, Provider = "LRCLIB", Manual = false
        }));

        var data = new LocalData(root);
        Assert.Equal("[00:01]legacy local", data.ReadLocal(First));
        Assert.Equal(.7, data.ReadCorrection(First));
        Assert.True(data.IsIgnored(First));
        Assert.Equal("automatic", data.ReadCachedLyrics(First)!.Record.SyncedLyrics![7..]);

        Assert.Null(data.ReadLocal(Second));
        Assert.Null(data.ReadCorrection(Second));
        Assert.False(data.IsIgnored(Second));
        Assert.Null(data.ReadCachedLyrics(Second));
        Assert.False(File.Exists(Path.Combine(root, "lyrics", First.LegacyKey + ".lrc")));
        Assert.Contains(First.Key, File.ReadAllText(Path.Combine(root, "cache", First.Key + ".json")));
    }

    [Fact]
    public void LegacyManualPairUpgradesToOneAtomicCurrentKeyBundle()
    {
        WriteLegacy("matches", ".json", JsonSerializer.Serialize(
            new ManualAssociation(1, First.LegacyKey, "LRCLIB", Manual with { SyncedLyrics = null })));
        WriteLegacy("cache", ".json", JsonSerializer.Serialize(new
        {
            Version = 1, TrackKey = First.LegacyKey, StoredUtc = DateTimeOffset.UtcNow,
            Record = Manual, Provider = "LRCLIB", Manual = true
        }));

        var cached = new LocalData(root).ReadCachedLyrics(First);
        Assert.True(cached!.Manual);
        Assert.Equal("manual", cached.Record.SyncedLyrics![7..]);
        var bundle = File.ReadAllText(Path.Combine(root, "matches", First.Key + ".json"));
        Assert.Contains("\"Version\": 2", bundle);
        Assert.Contains(First.Key, bundle);
        Assert.False(File.Exists(Path.Combine(root, "cache", First.Key + ".json")));
        Assert.Null(new LocalData(root).ReadManualAssociation(Second));
    }

    [Fact]
    public void NewWritesUseOnlyTheCurrentIdentity()
    {
        var data = new LocalData(root);
        Assert.True(data.SaveLocal(First, "[00:01]new"));
        Assert.True(data.SaveCorrection(First, .3));
        Assert.True(data.SetIgnored(First, true));
        data.SaveCache(First, Automatic);
        Assert.True(data.SaveManualAssociation(First, new("LRCLIB", Manual with { SyncedLyrics = null }), Manual));

        foreach (var (directory, extension) in new[] { ("lyrics", ".lrc"), ("corrections", ".json"),
            ("ignored", ".json"), ("cache", ".json"), ("matches", ".json") })
        {
            Assert.True(File.Exists(Path.Combine(root, directory, First.Key + extension)));
            Assert.False(File.Exists(Path.Combine(root, directory, First.LegacyKey + extension)));
        }
    }

    [Fact]
    public void ResetOperationsClaimLegacyFilesBeforeDeletingThem()
    {
        WriteLegacy("corrections", ".json", JsonSerializer.Serialize(new { Version = 1, TrackKey = First.LegacyKey, Seconds = .7 }));
        WriteLegacy("matches", ".json", JsonSerializer.Serialize(new ManualAssociation(1, First.LegacyKey, "LRCLIB", Manual with { SyncedLyrics = null })));
        WriteLegacy("ignored", ".json", JsonSerializer.Serialize(new { Version = 1, TrackKey = First.LegacyKey, Ignored = true }));

        var data = new LocalData(root);
        Assert.True(data.ResetCorrection(First));
        Assert.True(data.ForgetManualAssociation(First));
        Assert.True(data.SetIgnored(First, false));

        foreach (var (directory, extension) in new[] { ("corrections", ".json"), ("matches", ".json"), ("ignored", ".json") })
        {
            Assert.False(File.Exists(Path.Combine(root, directory, First.Key + extension)));
            Assert.False(File.Exists(Path.Combine(root, directory, First.LegacyKey + extension)));
        }
    }

    private void WriteLegacy(string directory, string extension, string contents)
    {
        var folder = Path.Combine(root, directory);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, First.LegacyKey + extension), contents);
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
