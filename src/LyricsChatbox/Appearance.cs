using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LyricsChatbox;

[System.Text.Json.Serialization.JsonConverter(typeof(AppearanceSettingsConverter))]
public record AppearanceSettings(string AccentPreset = "Rose", string? CustomAccent = null,
    string BackgroundStyle = "Midnight", bool ArtworkTintEnabled = false)
{
    public static AppearanceSettings Normalize(AppearanceSettings? value)
    {
        value ??= new();
        var custom = ThemeColors.TryHex(value.CustomAccent, out var color) ? ThemeColors.Hex(color) : null;
        var requested = value.AccentPreset ?? "Rose";
        var accent = ThemeColors.Accents.ContainsKey(requested) || requested == "Custom" && custom is not null
            ? requested : "Rose";
        return value with { AccentPreset = accent, CustomAccent = custom,
            BackgroundStyle = ThemeColors.Backgrounds.Contains(value.BackgroundStyle) ? value.BackgroundStyle : "Midnight" };
    }
}

public static class ThemeColors
{
    public static readonly IReadOnlyDictionary<string, string> Accents = new Dictionary<string, string>
    {
        ["Rose"] = "#FF647E", ["Purple"] = "#B79AFF", ["Blue"] = "#75ADFF", ["Cyan"] = "#63D9E4",
        ["Green"] = "#7BD5A4", ["Orange"] = "#FFA768", ["Red"] = "#FF7373"
    };
    public static readonly string[] Backgrounds = ["Midnight", "Pure Dark", "Graphite", "Tinted"];
    public static bool TryHex(string? text, out Color color)
    {
        color = default;
        if (text is not { Length: 7 } || text[0] != '#' || !uint.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb)) return false;
        color = Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb); return true;
    }
    public static Color Parse(string text) => TryHex(text, out var value) ? value : throw new ArgumentException("Expected #RRGGBB", nameof(text));
    public static string Hex(Color value) => $"#{value.R:X2}{value.G:X2}{value.B:X2}";
    public static Color Blend(Color a, Color b, double amount) => Color.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * amount), (byte)Math.Round(a.G + (b.G - a.G) * amount), (byte)Math.Round(a.B + (b.B - a.B) * amount));
    public static double Luminance(Color color)
    {
        static double Linear(byte c) { var s = c / 255d; return s <= .04045 ? s / 12.92 : Math.Pow((s + .055) / 1.055, 2.4); }
        return .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
    }
    public static double Contrast(Color a, Color b)
    {
        var first = Luminance(a); var second = Luminance(b);
        return (Math.Max(first, second) + .05) / (Math.Min(first, second) + .05);
    }
    public static Color Foreground(Color background) => Contrast(Colors.White, background) >= Contrast(Colors.Black, background) ? Colors.White : Colors.Black;
    private static Color Readable(Color color, params Color[] backgrounds)
    {
        // Preserve the selected hue while lifting very dark accents for dark surfaces.
        for (var step = 0; step <= 100; step++)
        {
            var adjusted = Blend(color, Colors.White, step / 100d);
            if (backgrounds.All(background => Contrast(adjusted, background) >= 4.5)) return adjusted;
        }
        return Colors.White;
    }
    public static IReadOnlyDictionary<string, Color> Create(AppearanceSettings? appearance)
    {
        var settings = AppearanceSettings.Normalize(appearance);
        var raw = Parse(settings.AccentPreset == "Custom" ? settings.CustomAccent! : Accents[settings.AccentPreset]);
        var canvas = Parse(settings.BackgroundStyle switch { "Pure Dark" => "#08090B", "Graphite" => "#1B1A1D", "Tinted" => "#111218", _ => "#101218" });
        if (settings.BackgroundStyle == "Tinted") canvas = Blend(canvas, raw, .055);
        var sidebar = Blend(canvas, Colors.White, .018);
        var surface = Blend(canvas, Colors.White, .035);
        var raised = Blend(canvas, Colors.White, .075);
        var selected = Blend(surface, Readable(raw, raised), .13);
        var accent = Readable(raw, raised, selected);
        var hover = Blend(accent, Colors.White, .12);
        var pressed = Blend(accent, Colors.Black, .13);
        return new Dictionary<string, Color>
        {
            ["CanvasBrush"] = canvas, ["SidebarBrush"] = sidebar, ["SurfaceBrush"] = surface,
            ["RaisedBrush"] = raised, ["HoverBrush"] = Blend(raised, Colors.White, .035),
            ["SelectedBrush"] = selected, ["LineBrush"] = Blend(surface, Colors.White, .105),
            ["StrongLineBrush"] = Blend(surface, Colors.White, .27), ["TextBrush"] = Parse("#F5F5F8"),
            ["MutedBrush"] = Readable(Parse("#ADB0BC"), raised, selected),
            ["FaintBrush"] = Readable(Parse("#9498A7"), raised, selected),
            ["AccentBrush"] = accent, ["AccentHoverBrush"] = hover, ["AccentPressedBrush"] = pressed,
            ["AccentMutedBrush"] = Blend(surface, accent, .075), ["AccentInkBrush"] = Foreground(accent),
            ["AccentHoverInkBrush"] = Foreground(hover), ["AccentPressedInkBrush"] = Foreground(pressed),
            ["FocusBrush"] = accent, ["SuccessBrush"] = Readable(Parse("#75D49D"), raised),
            ["WarningBrush"] = Readable(Parse("#F1BD73"), raised), ["ErrorBrush"] = Readable(Parse("#FF8897"), raised)
        };
    }
    public static void Apply(ResourceDictionary resources, AppearanceSettings? appearance, Color? artworkColor = null)
    {
        var settings = AppearanceSettings.Normalize(appearance);
        var tokens = Create(settings);
        foreach (var (key, color) in tokens) { var brush = new SolidColorBrush(color); brush.Freeze(); resources[key] = brush; }
        var surface = tokens["SurfaceBrush"];
        var tint = settings.ArtworkTintEnabled && artworkColor.HasValue ? artworkColor.Value : tokens["AccentBrush"];
        var strength = settings.ArtworkTintEnabled && artworkColor.HasValue ? .13 : .025;
        var hero = new LinearGradientBrush(surface, Blend(surface, tint, strength), 25); hero.Freeze();
        resources["HeroBrush"] = hero;
    }

    public static Task<Color?> SampleArtworkAsync(BitmapSource? image, CancellationToken token) => Task.Run<Color?>(() =>
    {
        token.ThrowIfCancellationRequested();
        if (image is null) return null;
        if (!image.IsFrozen) throw new ArgumentException("Artwork must be frozen", nameof(image));
        var scale = Math.Min(1, 48d / Math.Max(image.PixelWidth, image.PixelHeight));
        var scaled = new TransformedBitmap(image, new ScaleTransform(scale, scale));
        var sample = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
        var width = sample.PixelWidth; var height = sample.PixelHeight;
        var bytes = new byte[width * height * 4]; sample.CopyPixels(bytes, width * 4, 0);
        double r = 0, g = 0, b = 0, total = 0;
        for (var y = 0; y < height; y++)
        {
            token.ThrowIfCancellationRequested();
            for (var x = 0; x < width; x++)
            {
                var i = (y * width + x) * 4;
                if (bytes[i + 3] < 128) continue;
                var max = Math.Max(bytes[i], Math.Max(bytes[i + 1], bytes[i + 2]));
                var min = Math.Min(bytes[i], Math.Min(bytes[i + 1], bytes[i + 2]));
                var weight = max < 25 || min > 235 ? .1 : .5 + (max - min) / 255d;
                b += bytes[i] * weight; g += bytes[i + 1] * weight; r += bytes[i + 2] * weight; total += weight;
            }
        }
        return total == 0 ? null : Color.FromRgb((byte)(r / total), (byte)(g / total), (byte)(b / total));
    }, token);
}

public sealed class DefaultThemeResources : ResourceDictionary
{
    public DefaultThemeResources() => ThemeColors.Apply(this, null);
}

public sealed class ArtworkTintState
{
    private long generation;
    public Color? Color { get; private set; }
    public long Reset() { Color = null; return ++generation; }
    public bool Complete(long request, Color? color)
    {
        if (generation != request) return false;
        Color = color; return true;
    }
}

public sealed class AppearanceSettingsConverter : System.Text.Json.Serialization.JsonConverter<AppearanceSettings>
{
    public override AppearanceSettings Read(ref System.Text.Json.Utf8JsonReader reader, Type type, System.Text.Json.JsonSerializerOptions options)
    {
        using var document = System.Text.Json.JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != System.Text.Json.JsonValueKind.Object) return new();
        string? Text(string key) => root.TryGetProperty(key, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String ? value.GetString() : null;
        return AppearanceSettings.Normalize(new(Text("AccentPreset") ?? "Rose", Text("CustomAccent"), Text("BackgroundStyle") ?? "Midnight",
            root.TryGetProperty("ArtworkTintEnabled", out var enabled) && enabled.ValueKind == System.Text.Json.JsonValueKind.True));
    }
    public override void Write(System.Text.Json.Utf8JsonWriter writer, AppearanceSettings value, System.Text.Json.JsonSerializerOptions options)
    {
        value = AppearanceSettings.Normalize(value);
        writer.WriteStartObject(); writer.WriteString("AccentPreset", value.AccentPreset); writer.WriteString("CustomAccent", value.CustomAccent);
        writer.WriteString("BackgroundStyle", value.BackgroundStyle); writer.WriteBoolean("ArtworkTintEnabled", value.ArtworkTintEnabled); writer.WriteEndObject();
    }
}
