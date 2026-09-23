using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;

namespace LyricsChatbox;

public sealed class MusicRibbonWaveform : SKElement
{
    private const int DepthTiers = 3;
    private SKPathBuilder[]? pathBuilders;
    private SKPath?[]? framePaths;
    private SKPaint? glowPaint;
    private SKPaint? wirePaint;
    private bool renderingSubscribed;
    private long animationStartedAt;

    public static readonly DependencyProperty IsAnimatingProperty = DependencyProperty.Register(
        nameof(IsAnimating), typeof(bool), typeof(MusicRibbonWaveform),
        new FrameworkPropertyMetadata(false, OnAnimationStateChanged));

    public static readonly DependencyProperty AccentBrushProperty = DependencyProperty.Register(
        nameof(AccentBrush), typeof(Brush), typeof(MusicRibbonWaveform),
        new FrameworkPropertyMetadata(null, OnAccentChanged));

    public MusicRibbonWaveform()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    public bool IsAnimating
    {
        get => (bool)GetValue(IsAnimatingProperty);
        set => SetValue(IsAnimatingProperty, value);
    }

    public Brush? AccentBrush
    {
        get => (Brush?)GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        if (ActualWidth <= 0 || ActualHeight <= 0 || e.Info.Width <= 0 || e.Info.Height <= 0)
            return;

        EnsureResources();
        var color = AccentBrush is SolidColorBrush solid
            ? new SKColor(solid.Color.R, solid.Color.G, solid.Color.B, solid.Color.A)
            : SKColors.Transparent;
        var time = IsActuallyAnimating ? Stopwatch.GetElapsedTime(animationStartedAt).TotalSeconds : 0d;
        var steps = Math.Clamp((int)Math.Ceiling(ActualWidth * 0.82), 120, 220);
        var scaleX = (float)(e.Info.Width / ActualWidth);
        var scaleY = (float)(e.Info.Height / ActualHeight);

        foreach (var builder in pathBuilders!) builder.Reset();
        for (var trace = 0; trace < MusicRibbonWaveModel.TraceCount; trace++)
        {
            var tier = trace * DepthTiers / MusicRibbonWaveModel.TraceCount;
            BuildTrace(pathBuilders![tier], MusicRibbonWaveModel.TraceDepth(trace), steps, time);
        }

        try
        {
            for (var tier = 0; tier < DepthTiers; tier++) framePaths![tier] = pathBuilders![tier].Snapshot();
            var saveCount = canvas.Save();
            try
            {
                canvas.Scale(scaleX, scaleY);
                for (var tier = 0; tier < DepthTiers; tier++)
                {
                    var farToNear = tier / (float)(DepthTiers - 1);
                    glowPaint!.Color = color.WithAlpha(ScaledAlpha(color.Alpha, 0.035f + 0.045f * farToNear));
                    glowPaint.StrokeWidth = 1.9f + 0.65f * farToNear;
                    canvas.DrawPath(framePaths![tier]!, glowPaint);
                }

                for (var tier = 0; tier < DepthTiers; tier++)
                {
                    var farToNear = tier / (float)(DepthTiers - 1);
                    wirePaint!.Color = color.WithAlpha(ScaledAlpha(color.Alpha, 0.39f + 0.25f * farToNear));
                    wirePaint.StrokeWidth = 0.49f + 0.31f * farToNear;
                    canvas.DrawPath(framePaths![tier]!, wirePaint);
                }
            }
            finally
            {
                canvas.RestoreToCount(saveCount);
            }
        }
        finally
        {
            foreach (var path in framePaths!)
            {
                path?.Dispose();
            }
            Array.Clear(framePaths!);
        }
    }

    private bool IsActuallyAnimating => IsLoaded && IsVisible && IsAnimating;

    private void BuildTrace(SKPathBuilder path, double v, int steps, double time)
    {
        for (var sample = 0; sample <= steps; sample++)
        {
            var u = sample / (double)steps;
            var point = MusicRibbonWaveModel.Point(u, v, time, ActualWidth, ActualHeight);
            if (sample == 0) path.MoveTo((float)point.X, (float)point.Y);
            else path.LineTo((float)point.X, (float)point.Y);
        }
    }

    private void EnsureResources()
    {
        if (pathBuilders is null)
        {
            pathBuilders = new SKPathBuilder[DepthTiers];
            framePaths = new SKPath[DepthTiers];
            for (var index = 0; index < pathBuilders.Length; index++) pathBuilders[index] = new SKPathBuilder();
        }

        glowPaint ??= new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };
        wirePaint ??= new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };
    }

    private void DisposeResources()
    {
        if (pathBuilders is not null)
        {
            foreach (var builder in pathBuilders) builder.Dispose();
            pathBuilders = null;
        }
        if (framePaths is not null)
        {
            foreach (var path in framePaths) path?.Dispose();
            framePaths = null;
        }
        glowPaint?.Dispose();
        glowPaint = null;
        wirePaint?.Dispose();
        wirePaint = null;
    }

    private void RefreshAnimationSubscription()
    {
        var shouldSubscribe = IsActuallyAnimating;
        if (shouldSubscribe == renderingSubscribed)
        {
            InvalidateVisual();
            return;
        }

        renderingSubscribed = shouldSubscribe;
        if (shouldSubscribe)
        {
            animationStartedAt = Stopwatch.GetTimestamp();
            CompositionTarget.Rendering += OnRendering;
        }
        else CompositionTarget.Rendering -= OnRendering;
        InvalidateVisual();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => RefreshAnimationSubscription();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (renderingSubscribed)
        {
            CompositionTarget.Rendering -= OnRendering;
            renderingSubscribed = false;
        }
        DisposeResources();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) => RefreshAnimationSubscription();

    private void OnRendering(object? sender, EventArgs e) => InvalidateVisual();

    private static void OnAnimationStateChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((MusicRibbonWaveform)sender).RefreshAnimationSubscription();

    private static void OnAccentChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((MusicRibbonWaveform)sender).InvalidateVisual();

    private static byte ScaledAlpha(byte sourceAlpha, float opacity) =>
        (byte)Math.Clamp((int)Math.Round(sourceAlpha * opacity), 0, 255);
}
