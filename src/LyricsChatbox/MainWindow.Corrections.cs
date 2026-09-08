using System.Globalization;
using System.Windows;

namespace LyricsChatbox;

public partial class MainWindow
{
    private bool applyingCorrection;
    private double? savedCorrection;
    private void LoadCorrection()
    {
        savedCorrection = engine.Track is { } track ? data.ReadCorrection(track) : null;
        applyingCorrection = true;
        try { engine.Offset = savedCorrection ?? settings.Offset; OffsetSlider.Value = engine.Offset; }
        finally { applyingCorrection = false; }
        UpdateCorrectionLabel();
    }
    private void UpdateCorrectionLabel()
    {
        OffsetText.Text = engine.Offset.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " s";
        CorrectionStatus.Text = engine.Offset != (savedCorrection ?? settings.Offset) ? "Unsaved adjustment" :
            savedCorrection.HasValue ? "Saved for this recording" : "Global timing preference";
        SaveSongButton.IsEnabled = !string.IsNullOrWhiteSpace(engine.Track?.Title);
        ResetSongButton.IsEnabled = savedCorrection.HasValue;
    }
    private void SaveSongCorrection(object sender, RoutedEventArgs e)
    {
        if (engine.Track is not { } track) return;
        if (!data.SaveCorrection(track, engine.Offset)) { ErrorText.Text = "Could not save song correction."; return; }
        savedCorrection = engine.Offset; UpdateCorrectionLabel();
    }
    private void ResetSongCorrection(object sender, RoutedEventArgs e)
    {
        if (engine.Track is not { } track) return;
        if (!data.ResetCorrection(track)) { ErrorText.Text = "Could not reset song correction."; return; }
        LoadCorrection(); Tick();
    }
    private void UseGlobalCorrection(object sender, RoutedEventArgs e)
    {
        settings = settings with { Offset = engine.Offset }; Save(); UpdateCorrectionLabel();
    }
}
