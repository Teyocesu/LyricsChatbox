using System.Text;

namespace LyricsChatbox.Tests;

public sealed class NativeChatboxTests : IDisposable
{
    private const string Stamp = "2026.11.01 10:00:00";
    private const string FirstLog = "output_log_2026-11-01_10-00-00.txt", SecondLog = "output_log_2026-11-01_11-00-00.txt";
    private static readonly string Opened = Line("OnPageAddedToStack"), Closed = Line("OnPageRemovedFromStack");
    private static readonly NativeChatboxPoll Quiet = new(true, false, []);
    private readonly string root = Path.Combine(Path.GetTempPath(), "lyricschatbox-native-" + Guid.NewGuid().ToString("N"));

    public NativeChatboxTests() => Directory.CreateDirectory(root);
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    private static string Line(string method, string page = "Popups_Chat", string level = "Warning") => $"{Stamp} {level}    -  VP {page} {method}()\r\n";
    private static NativeChatboxPoll Signal(params NativeChatboxSignal[] signals) => new(true, false, signals);
    private void Append(string text, string file = FirstLog)
    {
        using var stream = new FileStream(Path.Combine(root, file), FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        stream.Write(Encoding.UTF8.GetBytes(text));
    }
    private NativeChatboxLog Attached()
    {
        Append("boot line\r\n");
        var log = new NativeChatboxLog(root);
        Assert.Empty(log.Poll().Signals);
        return log;
    }
    private static NativeChatboxPolicy Running()
    {
        var policy = new NativeChatboxPolicy();
        policy.SetEnabled(true); policy.SessionChanged(true);
        return policy;
    }

    [Fact]
    public void DisabledOrNoProcessNeverAsksToReadTheLog()
    {
        var policy = new NativeChatboxPolicy();
        policy.SessionChanged(true);
        Assert.False(policy.ShouldPoll(100));
        Assert.Equal(NativeChatboxState.Disabled, policy.State);
        policy.SetEnabled(true); policy.SessionChanged(false);
        Assert.False(policy.ShouldPoll(100));
        Assert.Equal(NativeChatboxState.Unavailable, policy.State);
    }

    [Fact]
    public void PollsArePacedWhileEnabled()
    {
        var policy = Running();
        Assert.True(policy.ShouldPoll(10));
        Assert.False(policy.ShouldPoll(10.1));
        Assert.True(policy.ShouldPoll(10.2));
    }

    [Fact]
    public void OpenHoldsAndConfirmedCloseWaitsForCooldown()
    {
        var policy = Running();
        Assert.False(policy.Holding);
        policy.Apply(Signal(NativeChatboxSignal.Opened), 10, 15);
        Assert.Equal(NativeChatboxState.ChatboxOpen, policy.State);
        Assert.True(policy.Holding);
        policy.Apply(Signal(NativeChatboxSignal.Closed), 20, 15);
        Assert.Equal(NativeChatboxState.WaitingAfterClose, policy.State);
        Assert.Equal("Resumes in 5 s", policy.HoldSummary(30));
        policy.Advance(34.9);
        Assert.True(policy.Holding);
        policy.Advance(35);
        Assert.False(policy.Holding);
        Assert.Equal(NativeChatboxState.Normal, policy.State);
    }

    [Fact]
    public void OpenDuringCooldownHoldsAgainAndNeverExpires()
    {
        var policy = Running();
        policy.Apply(Signal(NativeChatboxSignal.Closed), 10, 5);
        policy.Apply(Signal(NativeChatboxSignal.Opened), 12, 5);
        policy.Advance(1_000_000);
        Assert.Equal(NativeChatboxState.ChatboxOpen, policy.State);
    }

    [Fact]
    public void CloseAndReopenInOnePollEndsOpen()
    {
        var policy = Running();
        policy.Apply(Signal(NativeChatboxSignal.Opened, NativeChatboxSignal.Closed, NativeChatboxSignal.Opened), 10, 15);
        Assert.Equal(NativeChatboxState.ChatboxOpen, policy.State);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LostContinuityWhileOpenStaysHeldUntilResolved(bool available)
    {
        var policy = Running();
        policy.Apply(Signal(NativeChatboxSignal.Opened), 10, 15);
        policy.Apply(new(available, true, []), 11, 15);
        policy.Advance(1_000_000);
        Assert.Equal(NativeChatboxState.Uncertain, policy.State);
        Assert.True(policy.Holding);
        policy.ResumeNow();
        Assert.False(policy.Holding);
        policy.Apply(Signal(NativeChatboxSignal.Opened), 12, 15);
        policy.Apply(new(true, true, []), 13, 15);
        policy.Apply(Signal(NativeChatboxSignal.Closed), 14, 15);
        Assert.Equal(NativeChatboxState.WaitingAfterClose, policy.State);
    }

    [Fact]
    public void ContinuityLossDuringCooldownDoesNotStrandOutput()
    {
        var policy = Running();
        policy.Apply(Signal(NativeChatboxSignal.Closed), 10, 5);
        policy.Apply(new(false, true, []), 11, 5);
        Assert.Equal(NativeChatboxState.WaitingAfterClose, policy.State);
        policy.Advance(15);
        Assert.False(policy.Holding);
    }

    [Fact]
    public void LostLogWithoutHoldIsUnavailableAndHoldsNothing()
    {
        var policy = Running();
        policy.Apply(new(false, true, []), 10, 15);
        Assert.Equal(NativeChatboxState.Unavailable, policy.State);
        Assert.False(policy.Holding);
        Assert.Contains("not active", policy.Detail(10));
        policy.Apply(Quiet, 11, 15);
        Assert.Equal(NativeChatboxState.Normal, policy.State);
    }

    [Fact]
    public void SessionChangeOrDisablingEndsTheHold()
    {
        var policy = Running();
        policy.Apply(Signal(NativeChatboxSignal.Opened), 10, 15);
        policy.SessionChanged(false);
        Assert.False(policy.Holding);
        Assert.Equal(NativeChatboxState.Unavailable, policy.State);
        policy.SessionChanged(true);
        Assert.False(policy.EventsSeen);
        policy.Apply(Signal(NativeChatboxSignal.Opened), 11, 15);
        policy.SetEnabled(false);
        Assert.False(policy.Holding);
        Assert.Equal(NativeChatboxState.Disabled, policy.State);
    }

    [Theory]
    [InlineData(-1, 5)]
    [InlineData(5, 5)]
    [InlineData(60, 60)]
    [InlineData(int.MaxValue, 60)]
    public void CooldownIsBounded(int saved, int expected) => Assert.Equal(expected, NativeChatboxPolicy.Cooldown(saved));

    [Fact]
    public void AttachingNeverReplaysHistory()
    {
        Append(Opened + Closed + Opened);
        var log = new NativeChatboxLog(root);
        Assert.Empty(log.Poll().Signals);
        Assert.Empty(log.Poll().Signals);
        Append(Closed);
        Assert.Equal([NativeChatboxSignal.Closed], log.Poll().Signals);
    }

    [Fact]
    public void AttachingMidLineDiscardsOnlyThatFragment()
    {
        Append(Opened[..30]);
        var log = new NativeChatboxLog(root);
        Assert.Empty(log.Poll().Signals);
        Append(Opened[30..]);
        Assert.Empty(log.Poll().Signals);
        Append(Closed);
        Assert.Equal([NativeChatboxSignal.Closed], log.Poll().Signals);
    }

    [Fact]
    public void PartialLinesWaitForTheirEnding()
    {
        var log = Attached();
        Append(Opened[..30]);
        Assert.Empty(log.Poll().Signals);
        Append(Opened[30..]);
        Assert.Equal([NativeChatboxSignal.Opened], log.Poll().Signals);
    }

    [Theory]
    [InlineData("2026.11.01 10:00:00 Debug      -  VP Popups_Chat OnPageAddedToStack()")]
    [InlineData("2026.11.01 10:00:00 Warning    -  [Udon] VP Popups_Chat OnPageAddedToStack()")]
    [InlineData("VP Popups_Chat OnPageAddedToStack()")]
    [InlineData("2026.11.01 10:00:00 Warning    -  VP Popups_Chat OnPageAddedToStack() Popups_Chat")]
    [InlineData("2026.11.01 10:00:00 Warning    -  VP Popups_Chat OnPageShown()")]
    [InlineData("2026.11.01 10:00:00 Warning    -  VP Popups_Chat OnPageHidden()")]
    [InlineData("2026.11.01 10:00:00 Warning    -  VP Popups_Keyboard OnPageAddedToStack()")]
    [InlineData("2026.11.01 10:00:00 Warning    -  VP Keyboard OnPageAddedToStack()")]
    [InlineData("2026.11.01 10:00:00 Warning    -  VP Keyboard OnPageRemovedFromStack()")]
    public void OnlyExactLifecycleLinesCount(string text)
    {
        var log = Attached();
        Append(text + "\r\n");
        Assert.Empty(log.Poll().Signals);
    }

    [Fact]
    public void DelayedOrDuplicateFinishCannotCloseAReopenedPopup()
    {
        var log = Attached();
        Append(Opened + Closed + Line("Finish") + Opened + Line("Finish") + Line("Finish"));
        Assert.Equal([NativeChatboxSignal.Opened, NativeChatboxSignal.Closed, NativeChatboxSignal.Opened], log.Poll().Signals);
    }

    [Fact]
    public void OversizedAndBinaryLinesAreDiscardedWithoutHidingTheNextLine()
    {
        var log = Attached();
        Append(new string('x', NativeChatboxLog.MaxLineBytes * 3) + " VP Popups_Chat OnPageAddedToStack()\r\néあ\r\n" + Closed);
        Assert.Equal([NativeChatboxSignal.Closed], log.Poll().Signals);
    }

    [Fact]
    public void ReadsAreBoundedAndCatchUpAcrossPolls()
    {
        var log = Attached();
        Append(new string('x', NativeChatboxLog.MaxBytesPerPoll + 100) + "\r\n" + Opened);
        Assert.Empty(log.Poll().Signals);
        Assert.Equal([NativeChatboxSignal.Opened], log.Poll().Signals);
    }

    [Fact]
    public void NewerLogIsReadFromItsStart()
    {
        var log = Attached();
        Append(Opened, SecondLog);
        var poll = log.Poll();
        Assert.True(poll.ContinuityLost);
        Assert.Equal([NativeChatboxSignal.Opened], poll.Signals);
    }

    [Fact]
    public void OlderSurvivorIsNeverReplayed()
    {
        Append(Opened + Closed);
        Append("boot line\r\n", SecondLog);
        var log = new NativeChatboxLog(root);
        log.Poll();
        File.Delete(Path.Combine(root, SecondLog));
        var poll = log.Poll();
        Assert.True(poll.ContinuityLost);
        Assert.Empty(poll.Signals);
    }

    [Fact]
    public void TruncationResumesAtTheEndWithoutReplay()
    {
        var log = Attached();
        Append(Opened + Closed + Opened);
        log.Poll();
        File.WriteAllText(Path.Combine(root, FirstLog), Closed);
        var poll = log.Poll();
        Assert.True(poll.ContinuityLost);
        Assert.Empty(poll.Signals);
        Append(Closed);
        Assert.Equal([NativeChatboxSignal.Closed], log.Poll().Signals);
    }

    [Fact]
    public void ReplacementByALongerFileIsDetectedWithoutReplay()
    {
        var log = Attached();
        Append(Opened);
        log.Poll();
        File.WriteAllText(Path.Combine(root, FirstLog), new string('y', 200) + Closed + Closed);
        var poll = log.Poll();
        Assert.True(poll.ContinuityLost);
        Assert.Empty(poll.Signals);
    }

    [Fact]
    public void MissingFolderOrDeletedLogIsUnavailableAndRecoversWithoutReplay()
    {
        Assert.False(new NativeChatboxLog(Path.Combine(root, "absent")).Poll().Available);
        var log = Attached();
        File.Delete(Path.Combine(root, FirstLog));
        var poll = log.Poll();
        Assert.False(poll.Available);
        Assert.True(poll.ContinuityLost);
        Append(Opened);
        Assert.Empty(log.Poll().Signals);
    }

    [Fact]
    public void LockedLogAtAttachRetriesWithoutReplay()
    {
        Append(Opened);
        var log = new NativeChatboxLog(root);
        using (new FileStream(Path.Combine(root, FirstLog), FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.False(log.Poll().Available);
        Assert.True(log.Poll().Available);
        Assert.Empty(log.Poll().Signals);
    }

    [Fact]
    public void TransientReadFailureKeepsTheOffsetSoNoEventIsMissed()
    {
        var log = Attached();
        Append(Opened);
        using (new FileStream(Path.Combine(root, FirstLog), FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.False(log.Poll().Available);
        Assert.Equal([NativeChatboxSignal.Opened], log.Poll().Signals);
    }

    [Fact]
    public void RearmSendsOnlyCurrentNonemptyTextAtTheExistingPace()
    {
        var scheduler = new ChatboxScheduler();
        scheduler.Set(1, "line one", true);
        var first = scheduler.Take(10);
        scheduler.Complete(first!.Value, true);
        scheduler.Set(1, "line two", false);
        Assert.Null(scheduler.Take(20));
        scheduler.Set(1, "line three", false);
        scheduler.Rearm();
        scheduler.Set(1, "line four", true);
        var resumed = scheduler.Take(10.5);
        Assert.Null(resumed);
        resumed = scheduler.Take(30);
        Assert.Equal("line four", resumed!.Value.Text);
        scheduler.Complete(resumed.Value, true);
        Assert.Null(scheduler.Take(40));
    }

    [Fact]
    public void RearmResendsTheSameLineOnceBecauseNativeTextMayHaveReplacedIt()
    {
        var scheduler = new ChatboxScheduler();
        scheduler.Set(1, "same line", true);
        scheduler.Complete(scheduler.Take(10)!.Value, true);
        scheduler.Rearm();
        Assert.Equal("same line", scheduler.Take(20)!.Value.Text);
    }

    [Fact]
    public void RearmNeverEmitsEmptyTextEvenAfterAReceiverChangeDuringTheHold()
    {
        var scheduler = new ChatboxScheduler();
        scheduler.Set(1, "line", true);
        scheduler.Complete(scheduler.Take(10)!.Value, true);
        scheduler.Set(1, "", false);
        scheduler.ReceiverChanged();
        scheduler.Rearm();
        scheduler.Set(1, "", true);
        Assert.Null(scheduler.Take(20));
    }

    [Fact]
    public void ReceiverChangedKeepsItsExistingEmptyResend()
    {
        var scheduler = new ChatboxScheduler();
        scheduler.ReceiverChanged();
        scheduler.Set(1, "", true);
        Assert.Equal("", scheduler.Take(10)!.Value.Text);
    }

    [Fact]
    public void SidebarShowsOffThenPauseThenHold()
    {
        Assert.Equal("Off", OutputSidebarPresentation.Describe(false, false, "Active", "VRChat Chatbox open").Primary);
        Assert.Equal("Paused", OutputSidebarPresentation.Describe(true, true, "Paused", "VRChat Chatbox open").Primary);
        var held = OutputSidebarPresentation.Describe(true, false, "Active", "VRChat Chatbox open");
        Assert.Equal(("Held", "VRChat Chatbox open", true), (held.Primary, held.Secondary, held.ShowResume));
        Assert.False(OutputSidebarPresentation.Describe(true, false, "Active").ShowResume);
    }

    [Fact]
    public void OldSettingsLoadWithProtectionOffAndNewValuesRoundTrip()
    {
        var data = new LocalData(root);
        File.WriteAllText(Path.Combine(root, "settings.json"), """{ "Enabled": true, "Host": "127.0.0.1", "Port": 9000 }""");
        var loaded = data.ReadSettings();
        Assert.True(loaded.Enabled);
        Assert.False(loaded.NativeChatboxProtection);
        Assert.Equal(NativeChatboxPolicy.DefaultCooldownSeconds, loaded.NativeChatboxCooldownSeconds);
        Assert.True(data.SaveSettings(loaded with { NativeChatboxProtection = true, NativeChatboxCooldownSeconds = 30 }));
        Assert.Equal((true, 30), (data.ReadSettings().NativeChatboxProtection, data.ReadSettings().NativeChatboxCooldownSeconds));
    }

    [Fact]
    public void DiagnosticsReportTheFeatureState()
    {
        var text = new Diagnostics().Export(null, new AppSettings(NativeChatboxProtection: true), "", "", 0,
            nativeChatboxState: nameof(NativeChatboxState.ChatboxOpen));
        Assert.Contains("\"ChatboxOpen\"", text);
    }
}
