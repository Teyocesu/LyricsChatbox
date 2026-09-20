using System.IO;
using System.Text.Json;

namespace LyricsChatbox;

public sealed partial class LocalData
{
    // JSON can escape each UTF-16 unit as six ASCII bytes. These limits fit a full valid Unicode library.
    private const int ProfileBytes = 2 * 1024 * 1024;
    private const int QuickMessageBytes = 64 * 1024;
    private const int DecorationBytes = 256 * 1024;
    public ProfileLibrary ReadProfiles(AppSettings legacy)
    {
        var library = Read<ProfileLibrary>(Path.Combine(Root,"profiles.json"),ProfileBytes)?.NormalizeRotations();
        return library is {IsValid:true} ? library : ProfileLibrary.Migrate(legacy);
    }
    public bool SaveProfiles(ProfileLibrary library)
    {
        var saved = library.PrepareForSave();
        return saved is {IsValid:true} && WritePresentation("profiles.json", saved, ProfileBytes);
    }
    public QuickMessageLibrary ReadQuickMessages()
    {
        var library = Read<QuickMessageLibrary>(Path.Combine(Root,"quick-messages.json"),QuickMessageBytes);
        return library is {IsValid:true} ? library : QuickMessageLibrary.Defaults();
    }
    public bool SaveQuickMessages(QuickMessageLibrary library) => library.IsValid && WritePresentation("quick-messages.json", library, QuickMessageBytes);
    public DecorationState ReadDecorationState()
    {
        var state = Read<DecorationState>(Path.Combine(Root, "decorations.json"), DecorationBytes);
        return state is { IsValid: true } ? state : DecorationState.Empty;
    }
    public bool SaveDecorationState(DecorationState state) => state.IsValid && WritePresentation("decorations.json", state, DecorationBytes);
    private bool WritePresentation<T>(string name, T value, int maximum)
    {
        var json = JsonSerializer.Serialize(value, Json);
        return System.Text.Encoding.UTF8.GetByteCount(json) <= maximum && Write(Path.Combine(Root, name), json);
    }
    private string IgnorePath(TrackIdentity track) => CurrentPath("ignored", track, ".json");
    public bool IsIgnored(TrackIdentity track)
    {
        var path = ReadablePath("ignored", track, ".json");
        var decision = Read<IgnoreDecision>(path, 4096);
        if (decision is not { Version: 1, Ignored: true } || !TrackKeyMatches(decision.TrackKey, track)) return false;
        if (decision.TrackKey != track.Key) Write(path, JsonSerializer.Serialize(decision with { TrackKey = track.Key }, Json));
        return true;
    }
    public bool SetIgnored(TrackIdentity track, bool ignored)
    {
        if (!ClaimLegacyPath("ignored", track, ".json")) return false;
        if (!ignored) return DeleteIfExists(IgnorePath(track));
        return Write(IgnorePath(track), JsonSerializer.Serialize(new IgnoreDecision(1, track.Key, ignored), Json));
    }
    private record IgnoreDecision(int Version,string TrackKey,bool Ignored);
}
