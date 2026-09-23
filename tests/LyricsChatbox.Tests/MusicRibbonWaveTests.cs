namespace LyricsChatbox.Tests;

public sealed class MusicRibbonWaveTests
{
    [Fact]
    public void TraceCountAndDepthCoverTheRibbon()
    {
        Assert.Equal(28, MusicRibbonWaveModel.TraceCount);
        Assert.Equal(-1, MusicRibbonWaveModel.TraceDepth(0));
        Assert.Equal(1, MusicRibbonWaveModel.TraceDepth(MusicRibbonWaveModel.TraceCount - 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MusicRibbonWaveModel.TraceDepth(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MusicRibbonWaveModel.TraceDepth(MusicRibbonWaveModel.TraceCount));
    }

    [Fact]
    public void EndsConvergeToTheSharedCenterAxis()
    {
        foreach (var time in new[] { 0d, 1.2, 14.7 })
        foreach (var index in Enumerable.Range(0, MusicRibbonWaveModel.TraceCount))
        {
            var v = MusicRibbonWaveModel.TraceDepth(index);
            var left = MusicRibbonWaveModel.Point(0, v, time, 190, 74);
            var right = MusicRibbonWaveModel.Point(1, v, time, 190, 74);
            Assert.Equal(0, left.X);
            Assert.Equal(190, right.X);
            Assert.Equal(37, left.Y);
            Assert.Equal(37, right.Y);
        }
    }

    [Fact]
    public void StaticRibbonIsDeterministicAndSanitizesInvalidTime()
    {
        var first = MusicRibbonWaveModel.Point(0.43, -0.7, 0, 190, 74);
        var second = MusicRibbonWaveModel.Point(0.43, -0.7, 0, 190, 74);
        var invalidTime = MusicRibbonWaveModel.Point(0.43, -0.7, double.NaN, 190, 74);
        Assert.Equal(first, second);
        Assert.Equal(first, invalidTime);
    }

    [Fact]
    public void CoordinatesStayFiniteAndInsideTheCanvasAtDifferentScales()
    {
        foreach (var (width, height) in new[] { (190d, 74d), (380d, 148d), (172d, 74d) })
        foreach (var time in new[] { 0d, 0.55, 18.2, 240.1 })
        foreach (var index in Enumerable.Range(0, MusicRibbonWaveModel.TraceCount))
        foreach (var sample in Enumerable.Range(0, 81))
        {
            var point = MusicRibbonWaveModel.Point(sample / 80d,
                MusicRibbonWaveModel.TraceDepth(index), time, width, height);
            Assert.True(double.IsFinite(point.X) && double.IsFinite(point.Y));
            Assert.InRange(point.X, 0, width);
            Assert.InRange(point.Y, height * 0.03, height * 0.97);
        }
    }

    [Fact]
    public void TwistFieldChangesOrientationAcrossTheRibbon()
    {
        var twists = Enumerable.Range(0, 241)
            .Select(index => MusicRibbonWaveModel.TwistField(index / 240d, 3.7))
            .ToArray();
        Assert.Contains(twists, value => value > 0.25);
        Assert.Contains(twists, value => value < -0.25);

        var orientations = (from time in Enumerable.Range(0, 24).Select(step => step * 0.65)
                            from column in Enumerable.Range(0, 161).Select(step => step / 160d)
                            let far = MusicRibbonWaveModel.Point(column, -0.62, time, 190, 74)
                            let near = MusicRibbonWaveModel.Point(column, 0.62, time, 190, 74)
                            select Math.Sign(near.Y - far.Y)).ToArray();
        Assert.Contains(orientations, value => value > 0);
        Assert.Contains(orientations, value => value < 0);
    }
}
