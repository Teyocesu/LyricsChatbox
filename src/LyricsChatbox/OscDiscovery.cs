using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.Json;
using Zeroconf;

namespace LyricsChatbox;

public record OscService(string Name, string Address, int QueryPort);
public record OscDestination(string Host, int Port);
public record OscDiscoveryResult(string Status, OscDestination? Destination = null);

public sealed class OscDiscovery(HttpClient http, Func<CancellationToken, Task<IReadOnlyList<OscService>>> browse)
{
    public const string ServiceType = "_oscjson._tcp.local.";
    public static readonly TimeSpan Deadline = TimeSpan.FromSeconds(7);
    public static async Task<IReadOnlyList<OscService>> BrowseAsync(CancellationToken token)
    {
        var local = NetworkInterface.GetAllNetworkInterfaces().SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Select(a => a.Address).ToHashSet();
        var hosts = await ZeroconfResolver.ResolveAsync(ServiceType, TimeSpan.FromSeconds(2), retries: 1, cancellationToken: token);
        // Never enumerate unrelated service types or retain the network inventory.
        return hosts.Where(h => h.IPAddresses.Any(a => IsLocal(a, local)))
            .SelectMany(h => h.Services.Values.Where(s => s.Name.Equals(ServiceType, StringComparison.OrdinalIgnoreCase) &&
                s.Ttl > 0 && IsVrchatName(s.ServiceName.Replace("." + ServiceType, "", StringComparison.OrdinalIgnoreCase)))
                .Select(s => new OscService(s.ServiceName[..^(ServiceType.Length + 1)], "127.0.0.1", s.Port)))
            .Distinct().Take(9).ToArray();
    }
    private static bool IsLocal(string value, HashSet<IPAddress> local) => IPAddress.TryParse(value, out var address) &&
        (IPAddress.IsLoopback(address) || local.Contains(address));
    public static bool IsVrchatName(string? value) => value is { Length: > 14 and <= 100 } &&
        value.StartsWith("VRChat-Client-", StringComparison.OrdinalIgnoreCase) && value.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    public async Task<OscDiscoveryResult> DiscoverAsync(CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(Deadline);
        try
        {
            var candidates = (await browse(deadline.Token)).Where(s => IsVrchatName(s.Name) && s.QueryPort is > 0 and <= 65535 &&
                IPAddress.TryParse(s.Address, out var ip) && IPAddress.IsLoopback(ip)).Distinct().Take(9).ToArray();
            if (candidates.Length > 8) return new("Multiple OSCQuery services · using manual destination");
            var destinations = new HashSet<OscDestination>();
            foreach (var service in candidates)
            {
                using var attempt = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
                attempt.CancelAfter(TimeSpan.FromSeconds(2));
                try
                {
                    var origin = new UriBuilder("http", service.Address, service.QueryPort).Uri;
                    using var info = await ReadAsync(new Uri(origin, "?HOST_INFO"), attempt.Token);
                    var destination = ParseHost(info.RootElement, service);
                    if (destination is null) continue;
                    using var input = await ReadAsync(new Uri(origin, "chatbox/input"), attempt.Token);
                    if (WritableChatbox(input.RootElement)) destinations.Add(destination);
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or InvalidOperationException or OperationCanceledException)
                {
                    deadline.Token.ThrowIfCancellationRequested();
                }
            }
            token.ThrowIfCancellationRequested();
            return destinations.Count switch
            {
                1 => new("VRChat discovered via OSCQuery", destinations.Single()),
                > 1 => new("Multiple VRChat destinations · using manual destination"),
                _ => new("VRChat not discovered · using manual destination")
            };
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { return new("OSCQuery unavailable · using manual destination"); }
    }
    private async Task<JsonDocument> ReadAsync(Uri uri, CancellationToken token)
    {
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        const int limit = 16384;
        if (response.Content.Headers.ContentLength > limit) throw new IOException("Oversized OSCQuery response");
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var buffer = new MemoryStream();
        var bytes = new byte[4096]; int count;
        while ((count = await stream.ReadAsync(bytes, token)) > 0)
        {
            if (buffer.Length + count > limit) throw new IOException("Oversized OSCQuery response");
            buffer.Write(bytes, 0, count);
        }
        return JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 16 });
    }
    public static OscDestination? ParseHost(JsonElement root, OscService service)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("NAME", out var name) || name.ValueKind != JsonValueKind.String ||
            !string.Equals(name.GetString(), service.Name, StringComparison.OrdinalIgnoreCase)) return null;
        if (root.TryGetProperty("OSC_TRANSPORT", out var transport) && (transport.ValueKind != JsonValueKind.String || transport.GetString() != "UDP")) return null;
        var host = service.Address;
        if (root.TryGetProperty("OSC_IP", out var address))
        {
            if (address.ValueKind != JsonValueKind.String || !IPAddress.TryParse(address.GetString(), out var ip) || !IPAddress.IsLoopback(ip)) return null;
            host = ip.ToString();
        }
        var port = service.QueryPort;
        if (root.TryGetProperty("OSC_PORT", out var value) && (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out port))) return null;
        return port is > 0 and <= 65535 ? new(host, port) : null;
    }
    public static bool WritableChatbox(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("FULL_PATH", out var path) || path.ValueKind != JsonValueKind.String ||
            path.GetString() != "/chatbox/input" || !root.TryGetProperty("TYPE", out var type) || type.ValueKind != JsonValueKind.String) return false;
        var tags = type.GetString();
        if (tags is not { Length: 3 } || tags[0] != 's' || tags[1] is not ('T' or 'F') || tags[2] is not ('T' or 'F')) return false;
        return !root.TryGetProperty("ACCESS", out var access) || access.ValueKind == JsonValueKind.Number && access.TryGetInt32(out var mask) && mask is 2 or 3;
    }
}

// Dispatcher-owned generation: a manual destination or receiver restart invalidates old discoveries.
public sealed class OscDestinationSelection
{
    public long Revision { get; private set; }
    public bool Automatic { get; private set; }
    public OscDestination? Discovered { get; private set; }
    public long SetMode(bool automatic) { Revision++; Automatic = automatic; Discovered = null; return Revision; }
    public long Invalidate() { Revision++; Discovered = null; return Revision; }
    public bool Complete(long revision, OscDiscoveryResult result)
    {
        if (revision != Revision || !Automatic) return false;
        Discovered = result.Destination; return true;
    }
    public OscDestination Effective(AppSettings settings) => Automatic && Discovered is not null ? Discovered : new(settings.Host, settings.Port);
    public static bool InitiallyAutomatic(AppSettings settings) => settings.AutoDiscoverOsc ??
        (settings.Host is "127.0.0.1" or "localhost" && settings.Port == 9000);
}
