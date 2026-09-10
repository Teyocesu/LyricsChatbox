using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LyricsChatbox.Tests;

public sealed class AppearanceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "LyricsChatbox.Tests", Guid.NewGuid().ToString("N"));
    [Fact]
    public void LegacyAndCorruptAppearancePreserveBusinessPreferences()
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "settings.json");
        File.WriteAllText(path, """{"Enabled":true,"Compact":true,"Offset":0.7,"Host":"192.168.1.4","Port":9123,"CustomTemplate":"{title}","Appearance":{"AccentPreset":"Custom","CustomAccent":"#nothex","BackgroundStyle":"invalid","ArtworkTintEnabled":true}}""");
        var data = new LocalData(root); var settings = data.ReadSettings();
        Assert.True(settings.Enabled); Assert.True(settings.Compact); Assert.Equal(.7, settings.Offset);
        Assert.Equal("192.168.1.4", settings.Host); Assert.Equal(9123, settings.Port);
        Assert.Equal("Rose", settings.Appearance!.AccentPreset); Assert.Equal("Midnight", settings.Appearance.BackgroundStyle);
        Assert.True(settings.Appearance.ArtworkTintEnabled);
        File.WriteAllText(path, """{"Enabled":true,"Offset":0.7,"Appearance":123}""");
        Assert.True(data.ReadSettings().Enabled); Assert.Equal(.7, data.ReadSettings().Offset);
        File.WriteAllText(path, """{"Enabled":true,"Compact":true}""");
        Assert.Equal(new AppearanceSettings(), AppearanceSettings.Normalize(data.ReadSettings().Appearance));
    }
    [Fact]
    public void AppearancePersistsWithoutChangingBusinessState()
    {
        var data = new LocalData(root); var original = new AppSettings(Enabled:true, Offset:.3, Message:"private", StartWithWindows:false);
        var appearance = new AppearanceSettings("Custom", "#123abc", "Tinted", true);
        Assert.True(data.SaveSettings(original with { Appearance = appearance }));
        var loaded = data.ReadSettings();
        Assert.Equal(original, loaded with { Appearance = null });
        Assert.Equal(appearance with { CustomAccent = "#123ABC" }, loaded.Appearance);
        Assert.False(ThemeColors.TryHex("#  1234", out _)); Assert.False(ThemeColors.TryHex("#FFF", out _));
    }
    [Fact]
    public void PresetsAndPathologicalCustomAccentsKeepTextAndInteractionContrast()
    {
        var appearances = ThemeColors.Accents.Keys.Select(a => new AppearanceSettings(a))
            .Concat(new[] { "#000000", "#FFFFFF", "#777777", "#0000FF", "#00FF00" }.Select(a=>new AppearanceSettings("Custom", a)));
        foreach (var choice in appearances)
        foreach (var background in ThemeColors.Backgrounds)
        {
            var tokens = ThemeColors.Create(choice with {BackgroundStyle=background});
            foreach (var surface in new[] {"SurfaceBrush", "RaisedBrush", "SelectedBrush"})
            foreach (var text in new[] {"TextBrush", "MutedBrush", "AccentBrush", "FocusBrush"})
                Assert.True(ThemeColors.Contrast(tokens[text], tokens[surface]) >= 4.5, $"{choice.AccentPreset}/{choice.CustomAccent}/{background}/{surface}/{text}");
            foreach (var pair in new[] {("AccentBrush","AccentInkBrush"), ("AccentHoverBrush","AccentHoverInkBrush"), ("AccentPressedBrush","AccentPressedInkBrush")})
                Assert.True(ThemeColors.Contrast(tokens[pair.Item1],tokens[pair.Item2]) >= 4.5);
        }
    }
    [Fact]
    public void DynamicResourceUpdatesExistingControlWithoutRecreation()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var label = new TextBlock();
                ThemeColors.Apply(label.Resources, new("Rose"));
                label.SetResourceReference(TextBlock.ForegroundProperty,"AccentBrush");
                var rose = ((SolidColorBrush)label.Foreground).Color;
                ThemeColors.Apply(label.Resources, new("Blue", BackgroundStyle:"Graphite"));
                Assert.NotEqual(rose, ((SolidColorBrush)label.Foreground).Color);
                Assert.Equal(ThemeColors.Create(new("Blue", BackgroundStyle:"Graphite"))["AccentBrush"], ((SolidColorBrush)label.Foreground).Color);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert.True(thread.Join(5000));
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
    [Theory]
    [InlineData(64,64)]
    [InlineData(1,256)]
    [InlineData(256,1)]
    public async Task ArtworkSamplingIsBoundedCancellableAndStaleColorIsRejected(int width,int height)
    {
        var bytes = new byte[width*height*4];
        for(var i=0;i<bytes.Length;i+=4) { bytes[i+2]=255; bytes[i+3]=255; }
        var source = BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,bytes,width*4); source.Freeze();
        var color = await ThemeColors.SampleArtworkAsync(source,default);
        Assert.Equal(Colors.Red,color);
        var state = new ArtworkTintState(); var old = state.Reset(); var current = state.Reset();
        Assert.True(state.Complete(current,Colors.Blue)); Assert.False(state.Complete(old,color)); Assert.Equal(Colors.Blue,state.Color);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>ThemeColors.SampleArtworkAsync(source,cancelled.Token));
        var resources = new ResourceDictionary(); ThemeColors.Apply(resources,new(ArtworkTintEnabled:true),Colors.White);
        var hero = (LinearGradientBrush)resources["HeroBrush"];
        Assert.All(hero.GradientStops, stop=>Assert.True(ThemeColors.Contrast(Colors.White,stop.Color)>=4.5));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root,true); }
}
