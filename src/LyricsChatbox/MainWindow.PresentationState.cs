using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LyricsChatbox;

public partial class MainWindow
{
    private sealed record PlaybackView(string Track, string Artist, string Album, string Lyric, string LyricsStatus);
    private sealed record ProgressView(double Value, string Text);
    private sealed record PreviewView(string Text, bool Placeholder, bool PreserveLayout, int PayloadLength, string Profile, bool Compact, string Owner);
    private sealed record ManualView(string Budget, string State, bool Enabled);
    private sealed record LyricsDetailsView(bool HasTrack, bool CanSearch, string? SearchHint, string IgnoreContent,
        RecoveryPresentation Recovery, string Source, string Cache, string Match, string Duration,
        string GlobalOffset, string SavedOffset, string EffectiveOffset, string Manual, string Ignored);

    private readonly PresentationChangeGate<PlaybackView> playbackView = new();
    private readonly PresentationChangeGate<ProgressView> progressView = new();
    private readonly PresentationChangeGate<PreviewView> previewView = new();
    private readonly PresentationChangeGate<ManualView> manualView = new();
    private readonly PresentationChangeGate<LyricsDetailsView> lyricsDetailsView = new();
    private readonly PresentationChangeGate<string> oscView = new();
    private readonly PresentationChangeGate<(string Text, bool ChooseVisible)> sourceView = new();
    private readonly PresentationCadence progressCadence = new(0.1);

    private void ApplyPlaybackView(string lyric)
    {
        var view = new PlaybackView(
            string.IsNullOrWhiteSpace(engine.Track?.Title) ? "No track playing" : engine.Track.Title,
            engine.Track?.Artist ?? "", engine.Track?.Album ?? "", lyric.Length > 0 ? lyric : "—", FriendlyLyricsStatus());
        if (!playbackView.ShouldApply(view)) return;
        TrackText.Text = view.Track; ArtistText.Text = view.Artist; AlbumText.Text = view.Album;
        LyricText.Text = view.Lyric; LyricsStatusText.Text = view.LyricsStatus;
    }

    // Source truth comes from the current coordinator state, so a dropped stale
    // playback event can never leave ambiguity guidance invisible.
    private void ApplySourceView()
    {
        var view = PresentationText.SourceView(playback.Mode, playback.ActiveKind, engine.Snapshot, lastSourceStatus, playback.Ambiguous);
        if (!sourceView.ShouldApply(view)) return;
        SourceText.Text = view.Text;
        ChoosePlaybackSourceButton.Visibility = view.ChooseVisible ? Visibility.Visible : Visibility.Collapsed;
        WindowLayout.Apply(this, ContentRoot.ActualHeight, playback.Ambiguous);
    }

    private void ApplyProgressView(double now, double? position)
    {
        if (!progressCadence.IsDue(now)) return;
        var value = engine.Track?.Duration > 0 && position.HasValue ? Math.Clamp(position.Value / engine.Track.Duration, 0, 1) : 0;
        var text = position.HasValue
            ? $"{DurationFormatter.Format(position)} / {DurationFormatter.Format(Math.Clamp(engine.Track?.Duration ?? 0, 0, 86400))}"
            : "No playback position";
        var view = new ProgressView(value, text);
        if (!progressView.ShouldApply(view)) return;
        PositionProgress.Value = view.Value; PositionText.Text = view.Text;
    }

    private void ApplyPreviewView(string visible, bool preserveLayout, int payloadLength, bool showContext, string? desired)
    {
        var preview = PresentationText.Preview(visible);
        var profile = (manual.IsManual ? "Manual · " + settings.ManualAlignment + " alignment"
            : profiles.Selected.Name + " · " + (showContext ? contextMode : settings.Preset)) + (settings.Compact ? " · Floating" : "");
        var owner = !engine.Enabled ? "Output off · preview" : IsOutputPaused(DateTimeOffset.UtcNow) ? "Output paused · preview" : manual.IsManual
            ? desired is null ? "Manual · unsent draft" : "Manual · preview" : "Automatic · preview";
        var view = new PreviewView(preview.Text, preview.IsPlaceholder, preserveLayout, payloadLength, profile, settings.Compact, owner);
        if (!previewView.ShouldApply(view)) return;
        PreviewText.Text = view.Text;
        PreviewText.SetResourceReference(TextBlock.ForegroundProperty, view.Placeholder ? "MutedBrush" : "TextBrush");
        PreviewText.FontStyle = view.Placeholder ? FontStyles.Italic : FontStyles.Normal;
        PreviewText.FontFamily = view.PreserveLayout ? LayoutFont : TextFont;
        BudgetText.Text = view.PayloadLength + " / 144";
        PreviewProfileText.Text = view.Profile;
        if (view.Compact) PreviewBubble.Background = Brushes.Transparent;
        else PreviewBubble.SetResourceReference(Border.BackgroundProperty, "RaisedBrush");
        PreviewBubble.Padding = view.Compact ? new Thickness(0, 4, 0, 4) : new Thickness(12, 8, 12, 8);
        OwnerText.Text = view.Owner;
    }

    private void ApplyManualView(string manualLayout, string manualPayload, string? desired, double now)
    {
        var budget = manualPayload.Length + " / 144" +
            (ChatboxFormatter.Visible(manualPayload).Length < manualLayout.Trim('\r', '\n').Length ? " · trimmed" : "");
        var paused = IsOutputPaused(DateTimeOffset.UtcNow);
        var state = paused ? "Output paused · your draft stays here" : manual.RemainingHold(now) is double remaining
            ? $"Sent · automatic resumes in {Math.Ceiling(remaining):0} s"
            : manual.IsManual ? manual.PendingSend ? "Sending your message…" : desired is null ? "Unsent draft · automatic lyrics are paused" : "Manual owns the Chatbox · automatic lyrics are paused"
            : "Automatic mode · editing a draft takes priority";
        var view = new ManualView(budget, state, engine.Enabled && !paused);
        if (!manualView.ShouldApply(view)) return;
        ManualBudgetText.Text = view.Budget; ManualStateText.Text = view.State;
        SendButton.IsEnabled = ClearButton.IsEnabled = view.Enabled;
    }

    private void ApplyOscView()
    {
        var text = !engine.Enabled ? "Output off" : IsOutputPaused(DateTimeOffset.UtcNow) ? "Output paused" :
            output.Status.StartsWith("OSC unavailable") ? "OSC unavailable · check Settings" : "OSC ready · no delivery receipt";
        if (oscView.ShouldApply(text)) OscText.Text = text;
    }
}
