using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace LyricsChatbox.Tests;

public sealed class PresentationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(),"LyricsChatbox.Tests",Guid.NewGuid().ToString("N"));
    [Fact]
    public void ProfilesMigrateExactPresentationAndPersistCreateEditSelectDelete()
    {
        var data = new LocalData(root);
        var old = new AppSettings(Enabled:true, Offset:.5, Host:"192.168.1.5", Port:9003, Preset:"Custom", CustomTemplate:"{message}\n{lyrics}",
            Message:"日本語 🎵", Compact:true, CustomAlignment:"Center", Appearance:new("Blue"));
        var library = data.ReadProfiles(old);
        Assert.Equal(4,library.Items.Count(p=>p.BuiltIn)); Assert.Equal(old,library.Selected.Apply(old));
        Assert.Equal("Current only",library.Selected.ContextMode);
        library = library.Create("My copy",library.Selected)!;
        var id = library.SelectedId;
        library = library.Save(library.Selected with {Name="Evening", ContextMode="Adaptive", Compact=false})!;
        Assert.True(data.SaveProfiles(library));
        library = data.ReadProfiles(new());
        Assert.Equal("Evening",library.Selected.Name); Assert.Equal("Adaptive",library.Selected.ContextMode); Assert.False(library.Selected.Compact);
        var applied = library.Selected.Apply(old);
        Assert.Equal(old.Appearance,applied.Appearance); Assert.Equal(old.Host,applied.Host); Assert.Equal(old.Offset,applied.Offset); Assert.True(applied.Enabled);
        Assert.NotNull(library.Select("lyrics")); Assert.Null(library.Delete("lyrics"));
        library = library.Delete(id)!; Assert.DoesNotContain(library.Items,p=>p.Id==id); Assert.True(data.SaveProfiles(library));
        Assert.DoesNotContain(data.ReadProfiles(old).Items,p=>p.Id==id);
        File.WriteAllText(Path.Combine(root,"profiles.json"),"{bad"); Assert.Equal(old,data.ReadProfiles(old).Selected.Apply(old));
    }
    [Theory]
    [InlineData("Current only","current")]
    [InlineData("Current + next","current\nnext")]
    [InlineData("Previous + current + next","previous\n› current\nnext")]
    [InlineData("Adaptive","previous\n› current\nnext")]
    public void ContextModesUseTheAuthoritativeLineAndClearAcrossGaps(string mode,string expected)
    {
        var timeline = LrcParser.Parse("[00:01]previous\n[00:03]current\n[00:06]next\n[00:08]\n[00:30]later");
        Assert.Equal(expected,LyricContextComposer.Compose(timeline.Context(3),CoreTests.Track,"Lyrics Only",mode,false));
        Assert.Equal("previous\n› current\nnext",LyricContextComposer.Compose(timeline.Context(4,1),CoreTests.Track,"Lyrics Only","Adaptive",false));
        Assert.Equal(LyricContext.Empty,timeline.Context(0)); Assert.Equal(LyricContext.Empty,timeline.Context(9));
        Assert.Equal(LyricContext.Empty,timeline.Context(25)); Assert.Equal(LyricContext.Empty,timeline.Context(40));
        Assert.Equal("",timeline.Context(30).Previous);
    }
    [Fact]
    public void SemanticBudgetDropsMetadataThenPreviousThenNextWithoutShorteningCurrent()
    {
        var context = new LyricContext(new('p',25),new('c',70),new('n',25));
        var track = CoreTests.Track with {Title=new('t',80),Artist=new('a',80)};
        var result = LyricContextComposer.Compose(context,track,"Song + Lyrics","Adaptive",false);
        Assert.DoesNotContain('t',result); Assert.Contains(context.Previous,result); Assert.Contains(context.Next,result); Assert.Contains(context.Current,result);
        result = LyricContextComposer.Compose(context with {Previous=new('p',60)},track,"Song + Lyrics","Adaptive",false);
        Assert.DoesNotContain('p',result); Assert.Contains(context.Current,result); Assert.Contains(context.Next,result);
        result = LyricContextComposer.Compose(context with {Current=new('c',130)},track,"Song + Lyrics","Adaptive",false);
        Assert.Equal(new string('c',130),result);
        var fullCurrent = new string('c',142);
        Assert.Equal(fullCurrent,LyricContextComposer.Compose(new("previous",fullCurrent,"next"),track,"Song + Lyrics","Adaptive",true));
    }
    [Theory]
    [InlineData("日本語")]
    [InlineData("👨‍👩‍👧‍👦")]
    [InlineData("é")]
    [InlineData("🇯🇵")]
    public void CurrentTruncationPreservesGraphemesAndCompactAndLineLimits(string element)
    {
        var current = string.Concat(Enumerable.Repeat(element,160));
        var expected = ChatboxFormatter.Visible(ChatboxFormatter.Format(current,true));
        var result = LyricContextComposer.Compose(new("old",current,"next"),CoreTests.Track,"Song + Lyrics","Adaptive",true);
        Assert.Equal(expected,result); Assert.InRange(ChatboxFormatter.Format(result,true).Length,0,144);
        Assert.Equal(result,new UTF8Encoding(false,true).GetString(new UTF8Encoding(false,true).GetBytes(result)));
        var nine = string.Join('\n',Enumerable.Repeat("current",9));
        Assert.Equal(nine,LyricContextComposer.Compose(new("old",nine,"next"),CoreTests.Track,"Song + Lyrics","Adaptive",false));
        var giant = "e" + new string('\u0301',300);
        Assert.Equal("",LyricContextComposer.Compose(new("old",giant,"next"),CoreTests.Track,"Lyrics Only","Adaptive",true));
    }
    [Fact]
    public async Task IgnoreSkipsAllSourcesRetainsImportsAndInvalidatesLateResults()
    {
        var data = new LocalData(root); var track = CoreTests.Track;
        Assert.True(data.SaveLocal(track,"[00:01]local")); Assert.True(data.SetIgnored(track,true));
        Assert.True(new LocalData(root).IsIgnored(track)); Assert.False(data.IsIgnored(track with {Title="Song (Live)"}));
        using var http = new HttpClient(new NoNetwork()); using var resolver = new LyricsResolver(http,data);
        Assert.Equal(LyricsOutcome.Ignored,(await resolver.ResolveAsync(track,default)).Outcome);
        Assert.Equal("[00:01]local",data.ReadLocal(track));
        Assert.True(data.SetIgnored(track,false)); Assert.Equal(LyricsOutcome.Found,(await resolver.ResolveAsync(track,default)).Outcome);
        File.WriteAllText(Path.Combine(root,"ignored",track.Key+".json"),"{bad"); Assert.False(data.IsIgnored(track));
        var engine = new SynchronizationEngine {Enabled=true}; engine.Observe(CoreTests.Snapshot(1)); var oldEpoch=engine.Epoch;
        engine.InvalidateLyrics("Lyrics ignored for this recording");
        Assert.False(engine.Complete(oldEpoch,new(LrcParser.Parse("[00:01]stale"),"Synced"))); Assert.Equal("",engine.Output(0));
        Assert.Equal(1,engine.Position(0));
    }
    [Fact]
    public void QuickMessagesPersistButSelectingOneIsAnUnsentDraftEvenAfterLiveEditing()
    {
        var data = new LocalData(root); var library = data.ReadQuickMessages();
        var message = new QuickMessage("custom","Greeting","こんにちは 🎵");
        library = library.Save(message)!; Assert.True(data.SaveQuickMessages(library));
        Assert.Contains(message,data.ReadQuickMessages().Items);
        library = library.Save(message with {Name="Edited",Text="AFK 日本語"})!; Assert.True(data.SaveQuickMessages(library));
        Assert.Contains(data.ReadQuickMessages().Items,m=>m.Name=="Edited");
        var manual = new ManualChat(); manual.Edit("old live",true,0); manual.Send();
        manual.PrepareDraft(message.Text);
        Assert.Equal(message.Text,manual.Draft); Assert.True(manual.IsManual); Assert.Null(manual.Desired("automatic",1));
        Assert.False(manual.PendingSend); Assert.False(manual.Typing(1));
        manual.Send(); Assert.Equal(message.Text,manual.Desired("automatic",2));
        var diagnostics = new Diagnostics().Export(null,new(),"none","ready",0);
        Assert.DoesNotContain(message.Text,diagnostics);
        Assert.True(data.SaveQuickMessages(library.Delete(message.Id))); Assert.DoesNotContain(data.ReadQuickMessages().Items,m=>m.Id==message.Id);
        Assert.True(data.SaveQuickMessages(new(1,[]))); Assert.Empty(data.ReadQuickMessages().Items);
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"quick-messages.json")));
        Assert.Equal(0,json.RootElement.GetProperty("Items").GetArrayLength());
    }
    private sealed class NoNetwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken) => throw new InvalidOperationException("No network expected");
    }
    public void Dispose() { if(Directory.Exists(root)) Directory.Delete(root,true); }
}
