using System.Text.Json;

namespace LyricsChatbox.Tests;

public class LifecycleTests
{
    [Fact]
    public async Task ResumeInvalidationRejectsOldLookupAndWaitsForFreshClock()
    {
        await using var playback = new AppleMusicPlayback();
        var engine = new SynchronizationEngine { Enabled = true };
        engine.Observe(CoreTests.Snapshot(10));
        var beforeSleep = engine.Epoch;
        var lyrics = new LyricsResolution(LrcParser.Parse("[00:10]before\n[00:30]after"), "Synced");
        Assert.True(engine.Complete(beforeSleep, lyrics));
        Assert.Equal("before", engine.Output(0));
        long observedRevision = -1;
        playback.Observed += (snapshot, _, revision) => { engine.Observe(snapshot); observedRevision = revision; };
        playback.ReanchorAfterResume();
        Assert.Equal(playback.Revision, observedRevision);
        Assert.True(observedRevision > 0);
        Assert.Null(engine.Position(7200));
        Assert.Equal("", engine.Output(7200));
        Assert.False(engine.Complete(beforeSleep, lyrics));
        engine.Observe(CoreTests.Snapshot(30, 7200));
        Assert.True(engine.Complete(engine.Epoch, lyrics));
        Assert.Equal(30, engine.Position(7200));
        Assert.Equal("after", engine.Output(7200));
    }

    [Fact]
    public void V03MigrationKeepsPreferencesAndNeverOptsIntoBackgroundBehavior()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("""{"Enabled":true,"Compact":true,"Offset":0.7,"Preset":"Custom","CustomTemplate":"{title}"}""")!;
        Assert.True(settings.IsValid); Assert.True(settings.Enabled); Assert.True(settings.Compact);
        Assert.Equal(0.7, settings.Offset); Assert.Equal("{title}", settings.CustomTemplate);
        Assert.False(settings.StartWithWindows || settings.StartMinimized || settings.MinimizeToTray || settings.CloseToTray || settings.AutomaticUpdateChecks);
        Assert.Equal(WindowAction.Exit, LifecyclePolicy.Close(settings, false));
        Assert.Equal(WindowAction.Normal, LifecyclePolicy.Minimize(settings));
        settings = settings with { CloseToTray = true, MinimizeToTray = true };
        Assert.Equal(WindowAction.Hide, LifecyclePolicy.Close(settings, false));
        Assert.Equal(WindowAction.Hide, LifecyclePolicy.Minimize(settings));
        Assert.Equal(WindowAction.Exit, LifecyclePolicy.Close(settings, true));
    }

    [Fact]
    public void SingleInstanceExcludesAnotherThreadSignalsOwnerAndReleasesOnExit()
    {
        var name = @"Local\LyricsChatbox.Test." + Guid.NewGuid();
        using var restored = new ManualResetEventSlim();
        using (var owner = new SingleInstance(name))
        {
            Assert.True(owner.IsOwner); owner.Listen(() => restored.Set());
            bool? secondOwner = null;
            var thread = new Thread(() => { using var second = new SingleInstance(name); secondOwner = second.IsOwner; });
            thread.Start(); Assert.True(thread.Join(3000));
            Assert.False(secondOwner); Assert.True(restored.Wait(3000));
        }
        using var next = new SingleInstance(name);
        Assert.True(next.IsOwner);
    }

    [Fact]
    public void StartupWritesQuotedOwnCommandUpdatesMovesAndReportsDeniedWrites()
    {
        var store = new FakeStartup(); var startup = new StartupRegistration(store);
        Assert.True(startup.Set(true, @"C:\Music Tools\LyricsChatbox.exe"));
        Assert.Equal("\"C:\\Music Tools\\LyricsChatbox.exe\"", store.Value);
        Assert.True(startup.Set(true, @"D:\Moved\LyricsChatbox.exe"));
        Assert.Equal("\"D:\\Moved\\LyricsChatbox.exe\"", store.Value);
        store.Deny = true;
        Assert.False(startup.Set(false, @"D:\Moved\LyricsChatbox.exe"));
        Assert.NotNull(store.Value);
        store.Deny = false;
        Assert.True(startup.Set(false, @"D:\Moved\LyricsChatbox.exe")); Assert.Null(store.Value);
        Assert.False(startup.Set(true, "LyricsChatbox.exe"));
        Assert.False(startup.Set(true, "C:\\bad\"name.exe"));
    }
    private sealed class FakeStartup : IStartupStore
    {
        public string? Value; public bool Deny;
        public string? Read() => Value;
        public void Write(string? command) { if (Deny) throw new UnauthorizedAccessException(); Value = command; }
    }
}
