using System.IO;
using System.Text.Json;

namespace LyricsChatbox;

public sealed partial class LocalData
{
    // JSON can escape each UTF-16 unit as six ASCII bytes. These limits fit a full valid Unicode library.
    private const int ProfileBytes = 160 * 1024;
    private const int QuickMessageBytes = 64 * 1024;
    public ProfileLibrary ReadProfiles(AppSettings legacy)
    {
        var library = Read<ProfileLibrary>(Path.Combine(Root,"profiles.json"),ProfileBytes);
        return library is {IsValid:true} ? library : ProfileLibrary.Migrate(legacy);
    }
    public bool SaveProfiles(ProfileLibrary library) => library.IsValid && WritePresentation("profiles.json", library, ProfileBytes);
    public QuickMessageLibrary ReadQuickMessages()
    {
        var library = Read<QuickMessageLibrary>(Path.Combine(Root,"quick-messages.json"),QuickMessageBytes);
        return library is {IsValid:true} ? library : QuickMessageLibrary.Defaults();
    }
    public bool SaveQuickMessages(QuickMessageLibrary library) => library.IsValid && WritePresentation("quick-messages.json", library, QuickMessageBytes);
    private bool WritePresentation<T>(string name, T value, int maximum)
    {
        var json = JsonSerializer.Serialize(value, Json);
        return System.Text.Encoding.UTF8.GetByteCount(json) <= maximum && Write(Path.Combine(Root, name), json);
    }
    private string IgnorePath(TrackIdentity track) => Path.Combine(Root,"ignored",track.Key+".json");
    public bool IsIgnored(TrackIdentity track) => Read<IgnoreDecision>(IgnorePath(track),4096) is {Version:1,Ignored:true} decision && decision.TrackKey==track.Key;
    public bool SetIgnored(TrackIdentity track, bool ignored) => Write(IgnorePath(track),JsonSerializer.Serialize(new IgnoreDecision(1,track.Key,ignored),Json));
    private record IgnoreDecision(int Version,string TrackKey,bool Ignored);
}
