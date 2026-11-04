using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LyricsChatbox;

public partial class MainWindow
{
    private sealed record PlaybackView(string Track, string Artist, string Album, string Lyric, string LyricsStatus);
    private sealed record ProgressView(double Value, string Position, string Duration);
    private sealed record PreviewView(string Text, bool Placeholder, bool PreserveLayout, int VisibleUnits, int Limit,
        string Profile, bool Compact, string Owner);
    private sealed record LyricsDetailsView(bool HasTrack, bool CanSearch, string? SearchHint, string IgnoreContent,
        RecoveryPresentation Recovery, string Source, string Cache, string Match, string Duration,
        string GlobalOffset, string SavedOffset, string EffectiveOffset, string Manual, string Ignored);

    private readonly PresentationChangeGate<PlaybackView> playbackView = new();
    private readonly PresentationChangeGate<ProgressView> progressView = new();
    private readonly PresentationChangeGate<PreviewView> previewView = new();
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
    }

    private void ApplyProgressView(double now, double? position)
    {
        if (!progressCadence.IsDue(now)) return;
        var value = engine.Track?.Duration > 0 && position.HasValue ? Math.Clamp(position.Value / engine.Track.Duration, 0, 1) : 0;
        var view = position.HasValue
            ? new ProgressView(value, DurationFormatter.Format(position), DurationFormatter.Format(Math.Clamp(engine.Track?.Duration ?? 0, 0, 86400)))
            : new ProgressView(value, "No playback position", "");
        if (!progressView.ShouldApply(view)) return;
        PositionProgress.Value = view.Value; PositionText.Text = view.Position; DurationText.Text = view.Duration;
    }

    private void ApplyPreviewView(string visible, bool preserveLayout, bool showContext, int limit, bool floating)
    {
        var preview = PresentationText.Preview(visible);
        var profile = profiles.Selected.Name + " · " + (showContext ? contextMode : settings.Preset) + FloatingCompatibility.SummaryLabel(settings.Compact);
        var owner = !engine.Enabled ? "Output off · preview" : IsOutputPaused(DateTimeOffset.UtcNow) ? "Output paused · preview" : nativeChatbox.Holding ? "Held for VRChat Chatbox · preview" : "Automatic · preview";
        var view = new PreviewView(preview.Text, preview.IsPlaceholder, preserveLayout, visible.Length,
            limit, profile, floating, owner);
        if (!previewView.ShouldApply(view)) return;
        PreviewText.Text = view.Text;
        PreviewText.SetResourceReference(TextBlock.ForegroundProperty, view.Placeholder ? "MutedBrush" : "TextBrush");
        PreviewText.FontStyle = view.Placeholder ? FontStyles.Italic : FontStyles.Normal;
        PreviewText.FontFamily = view.PreserveLayout ? LayoutFont : TextFont;
        BudgetText.Text = view.VisibleUnits + " / " + view.Limit;
        PreviewProfileText.Text = view.Profile;
        if (view.Compact) PreviewBubble.Background = Brushes.Transparent;
        else PreviewBubble.SetResourceReference(Border.BackgroundProperty, "InputBrush");
        PreviewBubble.Padding = view.Compact ? new Thickness(0, 4, 0, 4) : new Thickness(12, 8, 12, 8);
        OwnerText.Text = view.Owner;
    }

    private void ApplyOscView()
    {
        var text = !engine.Enabled ? "Output off" : IsOutputPaused(DateTimeOffset.UtcNow) ? "Output paused" : nativeChatbox.Holding ? "Held for VRChat Chatbox" :
            output.Status.StartsWith("OSC unavailable") ? "OSC unavailable · check Settings" : "OSC ready · no delivery receipt";
        if (oscView.ShouldApply(text)) OscText.Text = text;
        ApplyOscRouteView();
    }
}
