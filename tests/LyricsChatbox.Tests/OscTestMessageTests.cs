namespace LyricsChatbox.Tests;

public sealed class OscTestMessageTests
{
    private static readonly string Test = ChatboxScheduler.TestMessage;

    [Fact]
    public void TestIsRefusedWhileOutputCannotSendAndCancelledWhenItStops()
    {
        var scheduler = new ChatboxScheduler();
        scheduler.Set(1, "line", false);
        Assert.False(scheduler.RequestTest());
        Assert.Null(scheduler.Take(0));

        scheduler.Set(1, "line", true);
        Assert.True(scheduler.RequestTest());
        Assert.False(scheduler.RequestTest());
        scheduler.Set(1, "line", false);
        Assert.False(scheduler.TestPending);
        scheduler.Set(1, "line", true);
        Assert.Equal("line", scheduler.Take(0)!.Value.Text);
    }

    [Fact]
    public void TestWaitsForPacingSendsOnceHoldsThenResendsCurrentText()
    {
        var scheduler = new ChatboxScheduler();
        scheduler.Set(1, "line", true);
        var first = scheduler.Take(0)!.Value; Assert.Null(scheduler.Complete(first, true));

        Assert.True(scheduler.RequestTest());
        Assert.Null(scheduler.Take(0.5));
        var test = scheduler.Take(ChatboxScheduler.IntervalSeconds)!.Value;
        Assert.Equal(Test, test.Text);
        Assert.True(scheduler.Complete(test, true));
        Assert.False(scheduler.TestPending);

        scheduler.Set(1, "next line", true);
        Assert.Null(scheduler.Take(2.2));
        Assert.Null(scheduler.Take(ChatboxScheduler.IntervalSeconds + ChatboxScheduler.TestHoldSeconds - 0.01));
        var resumed = scheduler.Take(ChatboxScheduler.IntervalSeconds + ChatboxScheduler.TestHoldSeconds)!.Value;
        Assert.Equal("next line", resumed.Text);
        Assert.Null(scheduler.Complete(resumed, true));
        Assert.Null(scheduler.Take(20));
    }

    [Fact]
    public void TestClearsTheChatboxWhenThereIsNoCurrentTextAndRestoresUnchangedText()
    {
        var empty = new ChatboxScheduler();
        empty.Set(1, "", true);
        Assert.True(empty.RequestTest());
        Assert.True(empty.Complete(empty.Take(0)!.Value, true));
        Assert.Equal("", empty.Take(ChatboxScheduler.TestHoldSeconds)!.Value.Text);

        var unchanged = new ChatboxScheduler();
        unchanged.Set(1, "line", true);
        unchanged.Complete(unchanged.Take(0)!.Value, true);
        unchanged.RequestTest();
        unchanged.Complete(unchanged.Take(2)!.Value, true);
        Assert.Equal("line", unchanged.Take(2 + ChatboxScheduler.TestHoldSeconds)!.Value.Text);
    }

    [Fact]
    public void FailedTestIsReportedNotRetriedAndNormalOutputContinues()
    {
        var scheduler = new ChatboxScheduler();
        scheduler.Set(1, "line", true);
        Assert.True(scheduler.RequestTest());
        var test = scheduler.Take(0)!.Value;
        Assert.False(scheduler.Complete(test, false));

        var next = scheduler.Take(ChatboxScheduler.IntervalSeconds)!.Value;
        Assert.Equal("line", next.Text);
        Assert.Null(scheduler.Complete(next, true));
        Assert.Null(scheduler.Take(10));
    }
}
