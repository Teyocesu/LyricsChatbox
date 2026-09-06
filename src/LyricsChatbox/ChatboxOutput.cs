using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace LyricsChatbox;

public static class ChatboxFormatter
{
    public static string Format(string input)
    {
        // Replacing invalid UTF-16 before grapheme enumeration also prevents invalid UTF-8 on the wire.
        input = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(input)).Replace("\r\n", "\n").Replace('\r', '\n');
        var clean = new StringBuilder();
        foreach (var c in input) if (!char.IsControl(c) || c == '\n') clean.Append(c);
        var result = new StringBuilder();
        var elements = StringInfo.GetTextElementEnumerator(clean.ToString());
        var lines = 1;
        while (elements.MoveNext())
        {
            var element = elements.GetTextElement();
            if (result.Length + element.Length > 144 || element == "\n" && ++lines > 9) break;
            result.Append(element);
        }
        return result.ToString().Trim();
    }

    // Only one fixed OSC message: three aligned strings, with payload-free T/F tags.
    // A 15-line encoder is smaller than importing a general OSC stack; no parser, bundles or generic protocol API.
    public static byte[] Packet(string formatted)
    {
        var parts = new[] { "/chatbox/input", ",sTF", formatted };
        var bytes = new List<byte>();
        foreach (var part in parts)
        {
            bytes.AddRange(Encoding.UTF8.GetBytes(part));
            bytes.Add(0);
            while (bytes.Count % 4 != 0) bytes.Add(0);
        }
        return bytes.ToArray();
    }
}

public sealed class ChatboxScheduler
{
    // VRChat 2026.2.1 documents five messages per five seconds; retain margin and never burst.
    public const double IntervalSeconds = 1.05;
    private long epoch;
    private string? last;
    private string desired = "";
    private double next;
    private bool enabled;
    public void Set(long activeEpoch, string text, bool isEnabled)
    {
        epoch = activeEpoch; desired = ChatboxFormatter.Format(text); enabled = isEnabled;
    }
    public void ReceiverChanged() => last = null;
    public (long Epoch, string Text)? Take(double now)
    {
        if (!enabled || now < next || desired == last || last is null && desired.Length == 0) return null;
        last = desired; next = now + IntervalSeconds;
        return (epoch, desired);
    }
}

public sealed class ChatboxOutput : IDisposable
{
    private Socket? socket;
    private IPEndPoint destination = new(IPAddress.Loopback, 9000);
    public string Status { get; private set; } = "OSC ready · delivery unconfirmed";

    public void Configure(string host, int port)
    {
        var address = host == "localhost" ? IPAddress.Loopback : IPAddress.Parse(host);
        var nextDestination = new IPEndPoint(address, port);
        var nextSocket = new Socket(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp) { Blocking = false };
        socket?.Dispose();
        destination = nextDestination;
        socket = nextSocket;
        Status = "OSC ready · delivery unconfirmed";
    }
    public void Send(string text)
    {
        try
        {
            socket?.SendTo(ChatboxFormatter.Packet(text), destination);
            Status = "OSC sent · delivery unconfirmed";
        }
        catch (SocketException) { Status = "OSC unavailable · next current line will be attempted"; }
        catch (ObjectDisposedException) { }
    }
    public void Dispose() => socket?.Dispose();
}
