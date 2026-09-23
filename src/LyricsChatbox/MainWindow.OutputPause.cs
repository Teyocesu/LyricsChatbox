using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;

namespace LyricsChatbox;

public partial class MainWindow
{
    private bool visualizerAnimating;
    private bool IsOutputPaused(DateTimeOffset nowUtc) => outputPause.IsPaused(nowUtc);

    private void InitializeOutputPause()
    {
        runtimeState = runtimeState with { OutputPause = outputPause.State };
        _ = data.SaveRuntimeState(runtimeState);
        RefreshOutputPauseView(DateTimeOffset.UtcNow);
    }

    private void TickOutputPause(DateTimeOffset nowUtc)
    {
        if (outputPause.Evaluate(nowUtc, engine.Track))
        {
            runtimeState = runtimeState with { OutputPause = outputPause.State };
            _ = data.SaveRuntimeState(runtimeState);
            scheduler.ReceiverChanged();
            manual.ResumeOutput(MonotonicClock.Now);
            UpdateTray();
        }
        RefreshOutputPauseView(nowUtc);
    }

    private void OutputPauseClicked(object sender, RoutedEventArgs e)
    {
        if (OutputPauseButton.ContextMenu is not { } menu) return;
        menu.PlacementTarget = OutputPauseButton;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void ResumeOutputPauseClicked(object sender, RoutedEventArgs e) => ResumeOutputPause();

    private void PauseOutputSelected(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string option }) return;
        var nowUtc = DateTimeOffset.UtcNow;
        _ = outputPause.Evaluate(nowUtc, engine.Track);
        var change = option switch
        {
            "5" => outputPause.BeginTimed(nowUtc, TimeSpan.FromMinutes(5)),
            "15" => outputPause.BeginTimed(nowUtc, TimeSpan.FromMinutes(15)),
            "30" => outputPause.BeginTimed(nowUtc, TimeSpan.FromMinutes(30)),
            "track" => outputPause.BeginUntilTrackChanges(engine.Track),
            "resume" => outputPause.BeginUntilResumed(),
            _ => new OutputPauseChange(false, false)
        };
        if (!change.Applied) return;
        runtimeState = runtimeState with { OutputPause = outputPause.State };
        if (!data.SaveRuntimeState(runtimeState)) ErrorText.Text = "Output pause is active, but could not be saved for restart.";
        manual.PauseOutput(MonotonicClock.Now);
        scheduler.ReceiverChanged();
        var now = MonotonicClock.Now;
        typing.Suppress(now);
        var effects = OutputPauseEntryEffects.For(change, engine.Enabled);
        effects.Apply(() => _ = output.Send(""), () => output.SendTyping(false));
        RefreshOutputPauseView(nowUtc);
        UpdateTray();
        Tick();
    }

    private void ResumeOutputPause()
    {
        if (!outputPause.Resume()) return;
        runtimeState = runtimeState with { OutputPause = outputPause.State };
        if (!data.SaveRuntimeState(runtimeState)) ErrorText.Text = "Could not save the resumed output state.";
        scheduler.ReceiverChanged();
        manual.ResumeOutput(MonotonicClock.Now);
        RefreshOutputPauseView(DateTimeOffset.UtcNow);
        UpdateTray();
        Tick();
    }

    private void RefreshOutputPauseView(DateTimeOffset nowUtc)
    {
        var paused = IsOutputPaused(nowUtc);
        var state = OutputSidebarPresentation.Describe(engine.Enabled, paused, outputPause.Summary(nowUtc));
        var active = engine.Enabled && !paused;
        var sending = OutputSidebarPresentation.IsSending(engine.Enabled, paused,
            engine.Snapshot?.State == PlaybackState.Playing, manual.AutomaticAvailable(MonotonicClock.Now));
        var status = active ? OutputSidebarPresentation.ActiveStatusText : state.Primary;
        if (OutputStateText.Text != status) OutputStateText.Text = status;
        OutputStateText.SetResourceReference(TextBlock.ForegroundProperty,
            active ? "SuccessBrush" : paused ? "TextBrush" : "MutedBrush");
        OutputStatusDot.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        var activity = sending ? OutputSidebarPresentation.SendingText : OutputSidebarPresentation.ReadyText;
        if (OutputActivityText.Text != activity) OutputActivityText.Text = activity;
        OutputActivityText.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        if (OutputPauseHint.Text != state.Secondary) OutputPauseHint.Text = state.Secondary;
        OutputPauseHint.Visibility = string.IsNullOrEmpty(state.Secondary) ? Visibility.Collapsed : Visibility.Visible;
        if (!Equals(OutputPauseButton.Content, state.PauseContent)) OutputPauseButton.Content = state.PauseContent;
        OutputPauseButton.Visibility = state.ShowPause ? Visibility.Visible : Visibility.Collapsed;
        OutputResumeButton.Visibility = state.ShowResume ? Visibility.Visible : Visibility.Collapsed;
        PauseUntilTrackMenuItem.IsEnabled = engine.Track is not null;
        var effective = destinationSelection.Effective(settings);
        var destination = OutputSidebarPresentation.FormatDestination(effective.Host, effective.Port);
        if (OutputDestinationText.Text != destination) OutputDestinationText.Text = destination;
        // The motif is decorative playback activity, never measured audio or delivery proof:
        // full opacity while Active, dim otherwise; motion only while sending (and animations allowed).
        var opacity = active ? 1d : 0.35d;
        if (!OutputVisualizer.Opacity.Equals(opacity)) OutputVisualizer.Opacity = opacity;
        var animate = sending && SystemParameters.ClientAreaAnimation;
        if (animate != visualizerAnimating)
        {
            visualizerAnimating = animate;
            var story = (Storyboard)Sidebar.FindResource("OutputWaveformStory");
            if (animate) story.Begin(this, true); else story.Stop(this);
        }
    }

    private void RequestManualSend()
    {
        if (!engine.Enabled || IsOutputPaused(DateTimeOffset.UtcNow)) manual.SuppressOutput();
        else manual.Send();
        Tick();
    }
}
