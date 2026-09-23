namespace LyricsChatbox;

public readonly record struct MusicRibbonPoint(double X, double Y);

/// <summary>Pure geometry for the decorative Output ribbon.</summary>
public static class MusicRibbonWaveModel
{
    public const int TraceCount = 28;
    private const double Tau = Math.PI * 2;

    public static double TraceDepth(int index)
    {
        if ((uint)index >= TraceCount) throw new ArgumentOutOfRangeException(nameof(index));
        return -1d + 2d * index / (TraceCount - 1);
    }

    public static double EdgeEnvelope(double u)
    {
        u = double.IsFinite(u) ? Math.Clamp(u, 0, 1) : 0;
        return SmoothStep(0.07, 0.18, u) * SmoothStep(0.07, 0.18, 1 - u);
    }

    public static MusicRibbonPoint Point(double u, double v, double timeSeconds, double width, double height)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
            return new(0, 0);

        u = double.IsFinite(u) ? Math.Clamp(u, 0, 1) : 0;
        v = double.IsFinite(v) ? Math.Clamp(v, -1, 1) : 0;
        timeSeconds = double.IsFinite(timeSeconds) ? timeSeconds : 0;

        var edge = EdgeEnvelope(u);
        var lobeA = Gaussian(u, 0.295, 0.092);
        var lobeB = Gaussian(u, 0.535, 0.145);
        var lobeC = Gaussian(u, 0.704, 0.078);
        var lobeD = Gaussian(u, 0.842, 0.058);
        var phaseWarp = v * (0.31 * Math.Sin(Tau * 0.74 * u + timeSeconds * 0.27)
            + 0.17 * Math.Sin(Tau * 1.63 * u - timeSeconds * 0.19 + 1.1));

        var broad = 0.57 * Math.Sin(Tau * (1.48 * u + 0.025 * Math.Sin(timeSeconds * 0.17))
            - timeSeconds * 0.55 + phaseWarp);
        var medium = 0.28 * Math.Sin(Tau * 3.27 * u + timeSeconds * 0.39 + 1.37 + phaseWarp * 0.62);
        var fine = 0.13 * Math.Sin(Tau * 5.38 * u - timeSeconds * 0.83 + 2.14 + phaseWarp * 0.91);
        var localized = 0.42 * lobeA * Math.Sin(timeSeconds * 0.48 + 0.3)
            - 0.68 * lobeB * Math.Cos(timeSeconds * 0.43 + 0.57)
            + 0.51 * lobeC * Math.Sin(timeSeconds * 0.35 + 1.62)
            - 0.23 * lobeD * Math.Sin(timeSeconds * 0.29 + 0.21);
        var centerField = broad + medium + fine + localized;

        var twist = TwistField(u, timeSeconds);
        var lobeEnergy = 2.1 + 5.3 * lobeA + 8.4 * lobeB + 4.8 * lobeC + 2.2 * lobeD;
        var spreadField = v * lobeEnergy * (0.18 + 1.02 * twist);
        var depthFold = v * v * 2.8 * twist;
        var vertical = edge * (height * 0.25 * centerField + spreadField + depthFold);
        var verticalLimit = height * 0.47;
        vertical = Math.Clamp(vertical, -verticalLimit, verticalLimit);

        var perspective = 0.58 * Math.Sin(Tau * 0.82 * u + timeSeconds * 0.31 + 0.5)
            + 0.42 * Math.Sin(Tau * 1.71 * u - timeSeconds * 0.22 + 1.8);
        var x = width * u + edge * (v * (1.35 + 0.92 * perspective) + v * v * 0.58 * twist);

        return new(Math.Clamp(x, 0, width), height * 0.5 + vertical);
    }

    public static double TwistField(double u, double timeSeconds)
    {
        u = double.IsFinite(u) ? Math.Clamp(u, 0, 1) : 0;
        timeSeconds = double.IsFinite(timeSeconds) ? timeSeconds : 0;
        return 0.63 * Math.Sin(Tau * 0.78 * u + timeSeconds * 0.34 + 0.37)
            + 0.37 * Math.Sin(Tau * 1.91 * u - timeSeconds * 0.24 + 1.53);
    }

    private static double Gaussian(double value, double center, double width)
    {
        var normalized = (value - center) / width;
        return Math.Exp(-0.5 * normalized * normalized);
    }

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        var x = Math.Clamp((value - edge0) / (edge1 - edge0), 0, 1);
        return x * x * (3 - 2 * x);
    }
}
