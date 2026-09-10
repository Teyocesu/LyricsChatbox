namespace LyricsChatbox;

public record DisplayProfile(string Id, string Name, string Preset = "Lyrics Only", string ContextMode = "Current only",
    bool Compact = false, string Alignment = "Left", string CustomTemplate = "{lyrics}", string Message = "", bool BuiltIn = false)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsValid => Id is {Length: > 0 and <= 64} && Name is {Length: > 0 and <= 40} && !string.IsNullOrWhiteSpace(Name) &&
        ChatboxComposer.Presets.Contains(Preset) && LyricContextComposer.Modes.Contains(ContextMode) && MessageLayout.Alignments.Contains(Alignment) &&
        CustomTemplate is {Length: <= 512} && Message is {Length: <= 512};
    [System.Text.Json.Serialization.JsonIgnore]
    public string Summary => Preset + " · " + ContextMode + (Compact ? " · Floating" : "");
    public override string ToString() => Name;
    public AppSettings Apply(AppSettings settings) => settings with
    { Preset = Preset, CustomTemplate = CustomTemplate, Message = Message, Compact = Compact, CustomAlignment = Alignment };
    public static DisplayProfile FromSettings(AppSettings settings, string id, string name) => new(id, name, settings.Preset,
        Compact: settings.Compact, Alignment: settings.CustomAlignment, CustomTemplate: settings.CustomTemplate, Message: settings.Message);
}

public record ProfileLibrary(int Version, string SelectedId, IReadOnlyList<DisplayProfile> Items)
{
    public const int Maximum = 20;
    [System.Text.Json.Serialization.JsonIgnore]
    public DisplayProfile Selected => Items.First(p => p.Id == SelectedId);
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsValid => Version == 1 && Items is {Count: > 0 and <= Maximum} && Items.All(p => p is {IsValid: true}) &&
        Items.Select(p=>p.Id).Distinct().Count() == Items.Count && Items.Any(p=>p.Id == SelectedId);
    public ProfileLibrary? Select(string id) => Items.Any(p=>p.Id==id) ? this with {SelectedId=id} : null;
    public ProfileLibrary? Save(DisplayProfile profile)
    {
        if (!profile.IsValid || !Items.Any(p=>p.Id==profile.Id)) return null;
        return this with {Items=Items.Select(p=>p.Id==profile.Id ? profile with {BuiltIn=p.BuiltIn} : p).ToArray()};
    }
    public ProfileLibrary? Create(string name, DisplayProfile source)
    {
        var profile = source with {Id=Guid.NewGuid().ToString("N"), Name=name.Trim(), BuiltIn=false};
        if (Items.Count >= Maximum || !profile.IsValid) return null;
        return this with {Items=Items.Append(profile).ToArray(), SelectedId=profile.Id};
    }
    public ProfileLibrary? Delete(string id)
    {
        if (Items.Count <= 1 || Items.FirstOrDefault(p=>p.Id==id) is not {BuiltIn:false}) return null;
        var remaining = Items.Where(p=>p.Id!=id).ToArray();
        return this with {Items=remaining, SelectedId=SelectedId==id ? remaining[0].Id : SelectedId};
    }
    public static ProfileLibrary Migrate(AppSettings legacy)
    {
        DisplayProfile[] presets = [new("lyrics","Lyrics",BuiltIn:true), new("minimal","Minimal",Compact:true,BuiltIn:true),
            new("music-info","Music Info","Song + Lyrics",BuiltIn:true), new("custom","Custom","Custom",CustomTemplate:"{message}\n{lyrics}",BuiltIn:true)];
        var current = DisplayProfile.FromSettings(legacy,"legacy","My display");
        var equivalent = presets.FirstOrDefault(p => p.Apply(legacy) == legacy);
        return equivalent is not null ? new(1,equivalent.Id,presets) : new(1,current.Id,presets.Append(current).ToArray());
    }
}

public record QuickMessage(string Id, string Name, string Text)
{
    public override string ToString() => Name;
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsValid => Id is {Length: > 0 and <= 64} && Name is {Length: > 0 and <= 32} && !string.IsNullOrWhiteSpace(Name) &&
        Text is {Length: > 0 and <= 512} && !string.IsNullOrWhiteSpace(Text);
}
public record QuickMessageLibrary(int Version, IReadOnlyList<QuickMessage> Items)
{
    public const int Maximum = 16;
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsValid => Version == 1 && Items is {Count: <= Maximum} && Items.All(p=>p is {IsValid:true}) && Items.Select(p=>p.Id).Distinct().Count()==Items.Count;
    public static QuickMessageLibrary Defaults() => new(1, new[] {"AFK","BRB","One sec","Can't talk rn"}.Select((s,i)=>new QuickMessage("quick-"+i,s,s)).ToArray());
    public QuickMessageLibrary? Save(QuickMessage message)
    {
        if (!message.IsValid || Items.Count>=Maximum && !Items.Any(p=>p.Id==message.Id)) return null;
        return this with {Items=Items.Any(p=>p.Id==message.Id) ? Items.Select(p=>p.Id==message.Id ? message : p).ToArray() : Items.Append(message).ToArray()};
    }
    public QuickMessageLibrary Delete(string id) => this with {Items=Items.Where(p=>p.Id!=id).ToArray()};
}
