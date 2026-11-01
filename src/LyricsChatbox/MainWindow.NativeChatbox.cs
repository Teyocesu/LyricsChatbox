using System.Globalization;
using System.Windows;

namespace LyricsChatbox;

public partial class MainWindow
{
    private readonly NativeChatboxPolicy nativeChatbox = new();
    private readonly PresentationChangeGate<(string Detail, bool Resume)> nativeChatboxView = new();
    private NativeChatboxLog? nativeLog;
    private Task<NativeChatboxPoll>? nativePoll;
    private int nativeProcess = -1;

    private bool IsOutputBlocked(DateTimeOffset nowUtc) => IsOutputPaused(nowUtc) || nativeChatbox.Holding;

    private void InitializeNativeChatbox()
    {
        var cooldown = NativeChatboxPolicy.Cooldown(settings.NativeChatboxCooldownSeconds);
        NativeChatboxBox.IsChecked = settings.NativeChatboxProtection;
        NativeChatboxCooldownSlider.Value = cooldown;
        NativeChatboxCooldownText.Text = CooldownLabel(cooldown);
        nativeChatbox.SetEnabled(settings.NativeChatboxProtection);
    }

    private static string CooldownLabel(int seconds) =>
        "Wait " + seconds.ToString(CultureInfo.InvariantCulture) + " s after the Chatbox closes";

    // Log access starts only here, and only while enabled with VRChat running. Disk work stays off the dispatcher.
    private void TickNativeChatbox(double now)
    {
        if (!settings.NativeChatboxProtection) return;
        var wasHeld = nativeChatbox.Holding;
        if (receiverPid != nativeProcess) RestartNativeLog();
        if (nativePoll is { IsCompleted: true } completed)
        {
            nativePoll = null;
            nativeChatbox.Apply(completed.Result, now, settings.NativeChatboxCooldownSeconds);
        }
        nativeChatbox.Advance(now);
        if (nativeLog is { } log && nativePoll is null && nativeChatbox.ShouldPoll(now)) nativePoll = Task.Run(log.Poll);
        ApplyNativeHoldChange(wasHeld);
    }

    private void RestartNativeLog()
    {
        nativeProcess = receiverPid;
        // A poll still running belongs to the old reader, which is abandoned with it.
        nativePoll = null;
        nativeLog = receiverPid == 0 ? null : new(NativeChatboxLog.DefaultFolder);
        nativeChatbox.SessionChanged(receiverPid != 0);
    }

    private void ApplyNativeHoldChange(bool wasHeld)
    {
        var held = nativeChatbox.Holding;
        if (held == wasHeld) return;
        if (!held) scheduler.Rearm();
        diagnostics.Add(DiagnosticCategory.OSC, held ? "Native Chatbox hold started" : "Native Chatbox hold ended");
        UpdateTray();
    }

    private void ApplyNativeChatboxView(double now)
    {
        var view = (nativeChatbox.Detail(now), nativeChatbox.Holding);
        if (!nativeChatboxView.ShouldApply(view)) return;
        NativeChatboxStatus.Text = view.Item1;
        NativeChatboxResumeButton.Visibility = view.Holding ? Visibility.Visible : Visibility.Collapsed;
    }

    private void NativeChatboxChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        var wasHeld = nativeChatbox.Holding;
        settings = settings with { NativeChatboxProtection = NativeChatboxBox.IsChecked == true };
        nativeLog = null; nativePoll = null; nativeProcess = -1;
        nativeChatbox.SetEnabled(settings.NativeChatboxProtection);
        ApplyNativeHoldChange(wasHeld);
        Save(); Tick();
    }

    private void NativeChatboxCooldownChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready) return;
        var cooldown = (int)Math.Round(e.NewValue);
        settings = settings with { NativeChatboxCooldownSeconds = cooldown };
        NativeChatboxCooldownText.Text = CooldownLabel(cooldown);
        saveTimer.Stop(); saveTimer.Start();
    }

    private void ResumeNativeChatbox(object sender, RoutedEventArgs e)
    {
        var wasHeld = nativeChatbox.Holding;
        nativeChatbox.ResumeNow();
        ApplyNativeHoldChange(wasHeld);
        Tick();
    }
}
