using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace LyricsChatbox.Tests;

public class BoundaryTests
{
    [Theory]
    [InlineData("Song (feat. Guest)", "Song - featuring Guest", "Artist", "Artist", 120, true)]
    [InlineData("Song (2011 Remaster)", "Song - 2011 Remaster", "Artist", "Artist", 120, true)]
    [InlineData("Song (Live)", "Song", "Artist", "Artist", 120, false)]
    [InlineData("Song (DJ Remix)", "Song (Other Remix)", "Artist", "Artist", 120, false)]
    [InlineData("Song", "Song", "Artist", "Cover Singer", 120, false)]
    [InlineData("Song", "Song", "Artist", "Artist", 123, false)]
    [InlineData("Song (Clean)", "Song (Explicit)", "Artist", "Artist", 120, false)]
    [InlineData("夜に駆ける", "夜に駆ける", "YOASOBI", "yoasobi", 120, true)]
    public void MatchingRejectsWrongRecordings(string title, string other, string artist, string otherArtist, double duration, bool accepted)
    {
        var score = LyricsMatching.Score(new(title, artist, "Album", 120), new(1, other, otherArtist, "Album", duration, false, "[00:01]text"));
        Assert.Equal(accepted, score.HasValue);
    }

    [Fact]
    public void SearchAmbiguityFailsClosedButIdenticalDuplicatesAreSafe()
    {
        var record = new LyricsRecord(1, "Song", "Artist", "Album", 120, false, "[00:01]one");
        Assert.True(LyricsMatching.Choose(CoreTests.Track, [record, record with { Id = 2, SyncedLyrics = "[00:02]other" }]).Ambiguous);
        Assert.NotNull(LyricsMatching.Choose(CoreTests.Track, [record, record with { Id = 2 }]).Record);
        Assert.Null(LyricsMatching.Score(CoreTests.Track, record with { AlbumName = "Live at Venue" }));
    }

    [Fact]
    public void SchedulerDedupeLatestEpochDisableAndReceiverRecovery()
    {
        var scheduler = new ChatboxScheduler();
        scheduler.Set(1, "A", true);
        var packet = scheduler.Take(0)!.Value; Assert.Equal("A", packet.Text); scheduler.Complete(packet, true);
        scheduler.Set(1, "B", true); Assert.Null(scheduler.Take(0.1));
        scheduler.Set(2, "C", true); Assert.Null(scheduler.Take(0.2));
        packet = scheduler.Take(1.5)!.Value; Assert.Equal((2L, "C"), packet); scheduler.Complete(packet, true);
        Assert.Null(scheduler.Take(3));
        scheduler.Set(2, "queued", false); Assert.Null(scheduler.Take(4));
        scheduler.Set(3, "new", true); packet = scheduler.Take(5)!.Value; Assert.Equal((3L, "new"), packet); scheduler.Complete(packet, true);
        scheduler.Set(3, "", true); packet = scheduler.Take(7)!.Value; Assert.Equal("", packet.Text); scheduler.Complete(packet, true);
        scheduler.Set(4, "current", true); packet = scheduler.Take(9)!.Value; scheduler.Complete(packet, true);
        scheduler.ReceiverChanged(); packet = scheduler.Take(11)!.Value; Assert.Equal("current", packet.Text); scheduler.Complete(packet, true);
    }

    [Fact]
    public void SchedulerCommitsOnlySuccessfulSendsAndRetriesLatestStateWithoutBursting()
    {
        var scheduler = new ChatboxScheduler();
        scheduler.Set(1, "A", true);
        var failed = scheduler.Take(0)!.Value;
        scheduler.Complete(failed, false);
        Assert.Null(scheduler.Take(.5));
        Assert.Equal(failed, scheduler.Take(1.05));

        scheduler.Set(2, "B", true);
        scheduler.Complete(failed, false);
        Assert.Null(scheduler.Take(1.1));
        var current = scheduler.Take(2.1)!.Value;
        Assert.Equal((2L, "B"), current);
        scheduler.Complete(current, true);
        Assert.Null(scheduler.Take(3.2));

        scheduler.Set(3, "C", true);
        var nextFailure = scheduler.Take(3.3)!.Value;
        scheduler.Complete(nextFailure, false);
        scheduler.Set(4, "D", true);
        Assert.Null(scheduler.Take(3.4));
        var recovered = scheduler.Take(4.35)!.Value;
        Assert.Equal((4L, "D"), recovered);
        scheduler.Complete(recovered, true);
        Assert.Null(scheduler.Take(5.5));
    }

    [Fact]
    public async Task ManualWhileReceiverAbsentReturnsOnlyCurrentDraftThenCurrentAutomatic()
    {
        int port;
        using (var reservation = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            port = ((IPEndPoint)reservation.Client.LocalEndPoint!).Port;
        using var output = new ChatboxOutput();
        output.Configure("127.0.0.1", port);
        var manual = new ManualChat();
        var scheduler = new ChatboxScheduler();
        manual.Focus(true);
        foreach (var (time, draft) in new[] { (0d, "old draft A"), (2d, "old draft B") })
        {
            manual.Edit(draft, true, time);
            scheduler.Set(1, manual.Desired("old automatic", time)!, true);
            var absentAttempt = scheduler.Take(time)!.Value;
            scheduler.Complete(absentAttempt, output.Send(absentAttempt.Text)); // UDP accepts sends even when no receiver is bound.
        }
        manual.Edit("current draft 日本語", true, 3);
        scheduler.Set(2, manual.Desired("new song", 3)!, false);
        Assert.Null(scheduler.Take(3));
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        scheduler.ReceiverChanged();
        scheduler.Set(2, manual.Desired("new song", 4)!, true, compact: true);
        var attempted = scheduler.Take(4)!.Value;
        scheduler.Complete(attempted, output.Send(attempted.Text));
        Assert.Equal(ChatboxFormatter.Packet(ChatboxFormatter.Format("current draft 日本語", true)),
            (await receiver.ReceiveAsync(deadline.Token)).Buffer);
        Assert.Null(scheduler.Take(6));
        manual.Resume();
        scheduler.Set(3, manual.Desired("current lyric", 7)!, true);
        attempted = scheduler.Take(7)!.Value;
        scheduler.Complete(attempted, output.Send(attempted.Text));
        Assert.Equal(ChatboxFormatter.Packet("current lyric"), (await receiver.ReceiveAsync(deadline.Token)).Buffer);
        Assert.Null(scheduler.Take(9));
    }

    [Fact]
    public void TrackChangeClearsOrReplacesWithinBudgetWithoutBursting()
    {
        var scheduler = new ChatboxScheduler();
        scheduler.Set(1, "previous song", true);
        var packet = scheduler.Take(0)!.Value; scheduler.Complete(packet, true);
        scheduler.Set(2, "", true);
        Assert.Null(scheduler.Take(0.5));
        packet = scheduler.Take(1.05)!.Value; Assert.Equal("", packet.Text); scheduler.Complete(packet, true);
        scheduler.Set(3, "new song", true);
        Assert.Null(scheduler.Take(1.1));
        packet = scheduler.Take(2.1)!.Value; Assert.Equal((3L, "new song"), packet); scheduler.Complete(packet, true);
        var sent = new List<double>();
        for (var tick = 220; tick < 2500; tick++)
        {
            var now = tick / 100d;
            scheduler.Set(tick, tick.ToString(), true);
            if (scheduler.Take(now) is { } next) { sent.Add(now); scheduler.Complete(next, true); }
        }
        Assert.All(sent, start => Assert.InRange(sent.Count(t => t >= start && t < start + 5), 1, 5));
    }

    [Theory]
    [InlineData("日本語")]
    [InlineData("👨‍👩‍👧‍👦")]
    [InlineData("é")]
    [InlineData("🇯🇵")]
    public void UnicodeTruncationNeverSplitsGraphemes(string element)
    {
        var text = string.Concat(Enumerable.Repeat(element, 150));
        var formatted = ChatboxFormatter.Format(text);
        Assert.InRange(formatted.Length, 1, 144);
        var strict = new UTF8Encoding(false, true);
        Assert.Equal(formatted, strict.GetString(strict.GetBytes(formatted)));
        Assert.StartsWith(formatted, text, StringComparison.Ordinal);
        Assert.Contains(formatted.Length, StringInfo.ParseCombiningCharacters(text));
        Assert.DoesNotContain('\0', ChatboxFormatter.Format("a\0b"));
        Assert.Equal(9, ChatboxFormatter.Format(string.Join('\n', Enumerable.Repeat("a", 15))).Split('\n').Length);
    }

    [Fact]
    public async Task RealUdpPacketHasAlignedUtf8AndBooleanTagsAndMissingReceiverIsSafe()
    {
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)receiver.Client.LocalEndPoint!).Port;
        using var output = new ChatboxOutput();
        output.Configure("127.0.0.1", port);
        output.Send("日本語 🎵");
        var packet = (await receiver.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2))).Buffer;
        Assert.Equal(0, packet.Length % 4);
        var cursor = 0;
        string ReadString()
        {
            var end = Array.IndexOf(packet, (byte)0, cursor);
            Assert.True(end >= cursor);
            var text = new UTF8Encoding(false, true).GetString(packet, cursor, end - cursor);
            cursor = (end + 4) & ~3;
            return text;
        }
        Assert.Equal("/chatbox/input", ReadString());
        Assert.Equal(",sTF", ReadString());
        Assert.Equal("日本語 🎵", ReadString());
        Assert.Equal(packet.Length, cursor);
        foreach (var typing in new[] { true, false })
        {
            output.SendTyping(typing);
            var signal = (await receiver.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2))).Buffer;
            Assert.Equal(typing ? "/chatbox/typing\0,T\0\0" : "/chatbox/typing\0,F\0\0", Encoding.UTF8.GetString(signal));
        }
        receiver.Dispose();
        output.Send("receiver gone");
        output.SendTyping(false);
    }
}
