using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace LyricsChatbox;

public partial class MainWindow
{
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
        var primary = !engine.Enabled ? "Off" : paused ? outputPause.Summary(nowUtc) : "Active";
        var secondary = !engine.Enabled && paused ? " · pause remains" : "";
        var text = primary + secondary;
        if (OutputStateText.Text != text) OutputStateText.Text = text;
        var button = paused ? "Change" : "Pause";
        if (!Equals(OutputPauseButton.Content, button)) OutputPauseButton.Content = button;
        OutputResumeButton.Visibility = paused ? Visibility.Visible : Visibility.Collapsed;
        PauseUntilTrackMenuItem.IsEnabled = engine.Track is not null;
    }

    private void RequestManualSend()
    {
        if (!engine.Enabled || IsOutputPaused(DateTimeOffset.UtcNow)) manual.SuppressOutput();
        else manual.Send();
        Tick();
    }
}
