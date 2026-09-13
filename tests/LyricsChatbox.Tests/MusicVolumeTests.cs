namespace LyricsChatbox.Tests;

public sealed class MusicVolumeTests
{
    [Fact]
    public void ActiveRendererWinsOverRetainedInactiveSession()
    {
        var inactive = new MusicVolume(17, "inactive-renderer", 1);
        var active = new MusicVolume(17, "active-shared-renderer", .4f);
        Assert.Same(active, AppleMusicVolume.SelectSession([(inactive, false), (active, true)]));
        Assert.Same(active, AppleMusicVolume.SelectSession([(active, true), (inactive, false)]));
        Assert.Null(AppleMusicVolume.SelectSession([(inactive, false), (active, true)], inactive));
    }

    [Fact]
    public void MultipleActiveOutputsNeverChooseAnArbitraryDevice()
    {
        var first = new MusicVolume(17, "headset", .4f);
        var second = new MusicVolume(17, "speakers", .8f);
        Assert.Null(AppleMusicVolume.SelectSession([(first, true), (second, true)]));
        Assert.Null(AppleMusicVolume.SelectSession([(first, true), (second, true)], first));
        Assert.Null(AppleMusicVolume.SelectSession([(first, false), (second, false)]));
        Assert.Null(AppleMusicVolume.SelectSession([]));
    }

    [Fact]
    public void StaleVolumeGestureCannotMoveAReplacementSession()
    {
        var expected = new MusicVolume(17, "original", .5f);
        Assert.Null(AppleMusicVolume.SelectSession([(new(18, "original", .5f), true)], expected));
        Assert.Null(AppleMusicVolume.SelectSession([(new(17, "replacement", .5f), true)], expected));
        var paused = expected with { Level = .7f };
        Assert.Same(paused, AppleMusicVolume.SelectSession([(paused, false)], expected));
    }
}
