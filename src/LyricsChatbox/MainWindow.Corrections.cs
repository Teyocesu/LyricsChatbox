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
        if (!data.SaveCorrection(track, engine.Offset)) { SetError("Could not save song correction."); return; }
        savedCorrection = engine.Offset; ClearError(); UpdateCorrectionLabel();
    }
    private void ResetSongCorrection(object sender, RoutedEventArgs e)
    {
        if (engine.Track is not { } track) return;
        if (!data.ResetCorrection(track)) { SetError("Could not reset song correction."); return; }
        ClearError(); LoadCorrection(); Tick();
    }
    private void UseGlobalCorrection(object sender, RoutedEventArgs e)
    {
        var previous = settings;
        var next = settings with { Offset = engine.Offset };
        var saved = engine.Track is { } track && savedCorrection.HasValue
            ? data.SaveGlobalCorrection(track, previous, engine.Offset)
            : data.SaveSettings(next);
        if (!saved)
        {
            SetError("Could not save the global timing preference or remove this recording's correction.");
            return;
        }
        settings = next; savedCorrection = null;
        ClearError(); UpdateCorrectionLabel(); Tick();
    }
}
