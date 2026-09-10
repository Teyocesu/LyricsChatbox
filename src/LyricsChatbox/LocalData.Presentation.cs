using System.IO;
using System.Text.Json;

namespace LyricsChatbox;

public sealed partial class LocalData
{
    public ProfileLibrary ReadProfiles(AppSettings legacy)
    {
        var library = Read<ProfileLibrary>(Path.Combine(Root,"profiles.json"),64_000);
        return library is {IsValid:true} ? library : ProfileLibrary.Migrate(legacy);
    }
    public bool SaveProfiles(ProfileLibrary library) => library.IsValid && Write(Path.Combine(Root,"profiles.json"),JsonSerializer.Serialize(library,Json));
    public QuickMessageLibrary ReadQuickMessages()
    {
        var library = Read<QuickMessageLibrary>(Path.Combine(Root,"quick-messages.json"),40_000);
        return library is {IsValid:true} ? library : QuickMessageLibrary.Defaults();
    }
    public bool SaveQuickMessages(QuickMessageLibrary library) => library.IsValid && Write(Path.Combine(Root,"quick-messages.json"),JsonSerializer.Serialize(library,Json));
    private string IgnorePath(TrackIdentity track) => Path.Combine(Root,"ignored",track.Key+".json");
    public bool IsIgnored(TrackIdentity track) => Read<IgnoreDecision>(IgnorePath(track),4096) is {Version:1,Ignored:true} decision && decision.TrackKey==track.Key;
    public bool SetIgnored(TrackIdentity track, bool ignored) => Write(IgnorePath(track),JsonSerializer.Serialize(new IgnoreDecision(1,track.Key,ignored),Json));
    private record IgnoreDecision(int Version,string TrackKey,bool Ignored);
}
