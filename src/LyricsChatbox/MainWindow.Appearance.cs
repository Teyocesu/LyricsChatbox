using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace LyricsChatbox;

public partial class MainWindow
{
    private readonly ArtworkTintState artworkTint = new();
    private CancellationTokenSource? tintCancellation;
    private bool changingAppearance;

    private void InitializeAppearance()
    {
        var appearance = AppearanceSettings.Normalize(settings.Appearance);
        AccentBox.ItemsSource = ThemeColors.Accents.Keys.Append("Custom").ToArray();
        BackgroundBox.ItemsSource = ThemeColors.Backgrounds;
        AccentBox.SelectedItem = appearance.AccentPreset; BackgroundBox.SelectedItem = appearance.BackgroundStyle;
        ArtworkTintBox.IsChecked = appearance.ArtworkTintEnabled;
        CustomColorBox.Text = appearance.CustomAccent ?? ThemeColors.Accents["Rose"];
        ApplyAppearance();
    }
    private void ApplyAppearance()
    {
        var appearance = AppearanceSettings.Normalize(settings.Appearance);
        ThemeColors.Apply(Application.Current.Resources, appearance, artworkTint.Color);
        CustomAccentPanel.Visibility = appearance.AccentPreset == "Custom" ? Visibility.Visible : Visibility.Collapsed;
        var actual = ThemeColors.Hex(ThemeColors.Create(appearance)["AccentBrush"]);
        AppearanceStatus.Text = appearance.AccentPreset == "Custom" && actual != appearance.CustomAccent
            ? "Display accent " + actual + " · adjusted for readable controls" : "Colors update throughout the app.";
    }
    private void AppearanceChanged(object sender, RoutedEventArgs e)
    {
        if (!ready || changingAppearance) return;
        var previous = AppearanceSettings.Normalize(settings.Appearance);
        var appearance = AppearanceSettings.Normalize(new(AccentBox.SelectedItem as string ?? "Rose",
            ThemeColors.TryHex(CustomColorBox.Text, out var color) ? ThemeColors.Hex(color) : previous.CustomAccent,
            BackgroundBox.SelectedItem as string ?? "Midnight", ArtworkTintBox.IsChecked == true));
        settings = settings with { Appearance = appearance };
        ApplyAppearance(); Save();
        if (previous.ArtworkTintEnabled != appearance.ArtworkTintEnabled) RefreshArtworkTint();
    }
    private void CustomAccentChanged(object sender, TextChangedEventArgs e)
    {
        if (!ready || changingAppearance || AccentBox.SelectedItem as string != "Custom") return;
        if (!ThemeColors.TryHex(CustomColorBox.Text, out _)) { AppearanceStatus.Text = "Enter a color as #RRGGBB."; return; }
        AppearanceChanged(sender, e);
    }
    private void ChooseAccentColor(object sender, RoutedEventArgs e)
    {
        var current = AppearanceSettings.Normalize(settings.Appearance);
        var color = ThemeColors.Parse(current.CustomAccent ?? ThemeColors.Accents["Rose"]);
        using var dialog = new Forms.ColorDialog { FullOpen = true, Color = System.Drawing.Color.FromArgb(color.R, color.G, color.B) };
        if (dialog.ShowDialog(new ColorDialogOwner(new System.Windows.Interop.WindowInteropHelper(this).Handle)) != Forms.DialogResult.OK) return;
        changingAppearance = true;
        try { CustomColorBox.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}"; AccentBox.SelectedItem = "Custom"; }
        finally { changingAppearance = false; }
        AppearanceChanged(sender, e);
    }
    private sealed class ColorDialogOwner(IntPtr handle) : Forms.IWin32Window { public IntPtr Handle => handle; }

    private void ClearArtworkTint()
    {
        tintCancellation?.Cancel(); tintCancellation?.Dispose(); tintCancellation = null;
        artworkTint.Reset();
        ThemeColors.Apply(Application.Current.Resources, settings.Appearance);
    }
    private void RefreshArtworkTint()
    {
        ClearArtworkTint();
        if (closing || !AppearanceSettings.Normalize(settings.Appearance).ArtworkTintEnabled || ArtImage.Source is not BitmapSource image) return;
        tintCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var generation = artworkTint.Reset(); var revision = playback.Revision;
        var task = LoadArtworkTintAsync(image, generation, revision, tintCancellation.Token);
        pending.Add(task);
        _ = task.ContinueWith(_ => Dispatcher.InvokeAsync(() => pending.Remove(task)), TaskScheduler.Default);
    }
    private async Task LoadArtworkTintAsync(BitmapSource image, long generation, long revision, CancellationToken token)
    {
        try
        {
            var color = await ThemeColors.SampleArtworkAsync(image, token);
            if (closing || token.IsCancellationRequested || revision != playback.Revision || !ReferenceEquals(image, ArtImage.Source) || !artworkTint.Complete(generation, color)) return;
            ThemeColors.Apply(Application.Current.Resources, settings.Appearance, artworkTint.Color);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or System.Runtime.InteropServices.COMException)
        { /* The fallback hero surface remains usable when sampling is unavailable. */ }
    }
}
