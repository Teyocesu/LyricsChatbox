using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace LyricsChatbox;

public enum NativeChatboxState { Disabled, Normal, ChatboxOpen, WaitingAfterClose, Uncertain, Unavailable }
public enum NativeChatboxSignal { Opened, Closed }
public sealed record NativeChatboxPoll(bool Available, bool ContinuityLost, IReadOnlyList<NativeChatboxSignal> Signals);

// Dispatcher-owned and transient. A hold ends only through a cooldown, a session change or the user, never a timeout.
public sealed class NativeChatboxPolicy
{
    public const int DefaultCooldownSeconds = 15;
    public const double PollSeconds = 0.2;
    private NativeChatboxState hold = NativeChatboxState.Normal;
    private bool enabled, running, available = true;
    private double resumeAt, nextPoll;
    public bool EventsSeen { get; private set; }

    public static int Cooldown(int seconds) => Math.Clamp(seconds, 5, 60);
    public bool Holding => hold != NativeChatboxState.Normal;
    public NativeChatboxState State => !enabled ? NativeChatboxState.Disabled : Holding ? hold
        : running && available ? NativeChatboxState.Normal : NativeChatboxState.Unavailable;
    public string Detail(double now) => State switch
    {
        NativeChatboxState.Disabled => "Off · VRChat's log is not read",
        NativeChatboxState.Normal => EventsSeen ? "Watching · Chatbox events seen this session" : "Watching · no Chatbox events seen yet this session",
        NativeChatboxState.Unavailable => (running ? "Unavailable · VRChat's log can't be read" : "Unavailable · VRChat isn't running") + " · protection is not active",
        _ => "Automatic output held · " + HoldSummary(now)
    };
    public string HoldSummary(double now) => hold switch
    {
        NativeChatboxState.ChatboxOpen => "Chatbox detected open",
        NativeChatboxState.WaitingAfterClose => $"Resumes in {Math.Ceiling(Math.Max(0, resumeAt - now)):0} s",
        NativeChatboxState.Uncertain => "Chatbox state uncertain",
        _ => ""
    };

    public void SetEnabled(bool value) { enabled = value; SessionChanged(running); }
    public void SessionChanged(bool processRunning)
    {
        running = processRunning;
        hold = NativeChatboxState.Normal; EventsSeen = false; available = true; nextPoll = 0;
    }
    public bool ShouldPoll(double now)
    {
        if (!enabled || !running || now < nextPoll) return false;
        nextPoll = now + PollSeconds;
        return true;
    }
    public void Apply(NativeChatboxPoll poll, double now, int cooldownSeconds)
    {
        if (!enabled) return;
        available = poll.Available;
        // Only a known-open popup needs Uncertain. A confirmed close keeps its cooldown, like Normal after the same loss.
        if (poll.ContinuityLost && hold == NativeChatboxState.ChatboxOpen) hold = NativeChatboxState.Uncertain;
        foreach (var signal in poll.Signals)
        {
            EventsSeen = true;
            hold = signal == NativeChatboxSignal.Opened ? NativeChatboxState.ChatboxOpen : NativeChatboxState.WaitingAfterClose;
            if (hold == NativeChatboxState.WaitingAfterClose) resumeAt = now + Cooldown(cooldownSeconds);
        }
    }
    public void Advance(double now) { if (hold == NativeChatboxState.WaitingAfterClose && now >= resumeAt) hold = NativeChatboxState.Normal; }
    public void ResumeNow() => hold = NativeChatboxState.Normal;
}

// Tails one log by byte offset. Used by one poll at a time, and replaced rather than reset when a session changes.
public sealed partial class NativeChatboxLog(string folder)
{
    public const int MaxBytesPerPoll = 256 * 1024;
    public const int MaxLineBytes = 256;
    private const int FingerprintBytes = 16;
    private static readonly byte[] Marker = "VP Popups_Chat "u8.ToArray();
    private readonly byte[] buffer = new byte[64 * 1024];
    private readonly byte[] line = new byte[MaxLineBytes];
    private readonly byte[] fingerprint = new byte[FingerprintBytes];
    private readonly byte[] check = new byte[FingerprintBytes];
    private string? current;
    private long position;
    private int lineLength, fingerprintLength;
    private bool skipLine;

    public static string DefaultFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "AppData", "LocalLow", "VRChat", "VRChat");

    // Whole lines only: nothing is captured, and a match yields only which lifecycle call it was.
    [GeneratedRegex(@"\A\d{4}\.\d{2}\.\d{2} \d{2}:\d{2}:\d{2} Warning\s+-\s+VP Popups_Chat (OnPageAddedToStack|OnPageRemovedFromStack)\(\)\z", RegexOptions.CultureInvariant)]
    private static partial Regex Lifecycle();

    public NativeChatboxPoll Poll()
    {
        var signals = new List<NativeChatboxSignal>();
        try
        {
            if (Newest() is not { } newest)
            {
                current = null;
                return new(false, true, signals);
            }
            var lost = false;
            if (current is null) AttachAtEnd(newest);
            else if (!string.Equals(current, newest, StringComparison.OrdinalIgnoreCase))
            {
                // Only a newer boot timestamp is a new session; an older survivor would replay history.
                lost = true;
                if (string.CompareOrdinal(Path.GetFileName(newest), Path.GetFileName(current)) > 0) Attach(newest, 0);
                else AttachAtEnd(newest);
            }
            lost |= Read(signals);
            return new(true, lost, signals);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return new(false, true, signals); }
    }

    private string? Newest()
    {
        if (!Directory.Exists(folder)) return null;
        // The boot timestamp in the name sorts correctly; last-write metadata can lag behind.
        return Directory.EnumerateFiles(folder, "output_log_*.txt").MaxBy(Path.GetFileName, StringComparer.Ordinal);
    }

    private static FileStream Open(string file) => new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1);

    private void Attach(string file, long offset)
    {
        current = file; position = offset;
        lineLength = 0; skipLine = false; fingerprintLength = 0;
    }

    private void AttachAtEnd(string file)
    {
        using var stream = Open(file);
        var end = stream.Length;
        var tail = end > 0;
        if (tail)
        {
            stream.Seek(end - 1, SeekOrigin.Begin);
            tail = stream.ReadByte() != '\n';
        }
        Attach(file, end);
        // An unterminated tail is a fragment of a line that was never seen.
        skipLine = tail;
        Remember(stream);
    }

    private void Remember(FileStream stream)
    {
        fingerprintLength = (int)Math.Min(position, FingerprintBytes);
        stream.Seek(position - fingerprintLength, SeekOrigin.Begin);
        stream.ReadExactly(fingerprint, 0, fingerprintLength);
    }

    private bool Read(List<NativeChatboxSignal> signals)
    {
        using var stream = Open(current!);
        if (stream.Length == position) return false;
        if (stream.Length < position || Replaced(stream))
        {
            AttachAtEnd(current!);
            return true;
        }
        stream.Seek(position, SeekOrigin.Begin);
        var remaining = MaxBytesPerPoll;
        while (remaining > 0)
        {
            var read = stream.Read(buffer, 0, Math.Min(buffer.Length, remaining));
            if (read == 0) break;
            remaining -= read; position += read;
            Consume(buffer.AsSpan(0, read), signals);
        }
        Remember(stream);
        return false;
    }

    private bool Replaced(FileStream stream)
    {
        stream.Seek(position - fingerprintLength, SeekOrigin.Begin);
        stream.ReadExactly(check, 0, fingerprintLength);
        return !check.AsSpan(0, fingerprintLength).SequenceEqual(fingerprint.AsSpan(0, fingerprintLength));
    }

    private void Consume(ReadOnlySpan<byte> data, List<NativeChatboxSignal> signals)
    {
        while (data.Length > 0)
        {
            var end = data.IndexOf((byte)'\n');
            var part = end < 0 ? data : data[..end];
            if (!skipLine)
            {
                if (lineLength + part.Length > MaxLineBytes) skipLine = true;
                else { part.CopyTo(line.AsSpan(lineLength)); lineLength += part.Length; }
            }
            if (end < 0) return;
            if (!skipLine && Match(line.AsSpan(0, lineLength)) is { } signal) signals.Add(signal);
            lineLength = 0; skipLine = false;
            data = data[(end + 1)..];
        }
    }

    private static NativeChatboxSignal? Match(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > 0 && bytes[^1] == '\r') bytes = bytes[..^1];
        // Unrelated lines are never decoded, so their text cannot reach a string.
        if (bytes.IndexOf(Marker) < 0) return null;
        var match = Lifecycle().Match(Encoding.Latin1.GetString(bytes));
        if (!match.Success) return null;
        return match.Groups[1].ValueSpan.SequenceEqual("OnPageAddedToStack") ? NativeChatboxSignal.Opened : NativeChatboxSignal.Closed;
    }
}
