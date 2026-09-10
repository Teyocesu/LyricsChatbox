using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace LyricsChatbox.Tests;

public class OscDiscoveryTests
{
    private static readonly OscService Service = new("VRChat-Client-123456", "127.0.0.1", 54321);
    private const string Input = "{\"FULL_PATH\":\"/chatbox/input\",\"TYPE\":\"sTT\",\"ACCESS\":2}";
    private static string Host(string port = "9010", string address = "127.0.0.1", string transport = "UDP", string name = "VRChat-Client-123456") =>
        $$"""{"NAME":"{{name}}","OSC_IP":"{{address}}","OSC_PORT":{{port}},"OSC_TRANSPORT":"{{transport}}"}""";
    [Fact]
    public async Task RealProtocolUsesAdvertisedQueryPortAndValidatesCapability()
    {
        var paths = new List<string>();
        using var http = new HttpClient(new Handler((request, _) =>
        {
            Assert.Equal("127.0.0.1", request.RequestUri!.Host); Assert.Equal(54321, request.RequestUri.Port);
            paths.Add(request.RequestUri.PathAndQuery);
            return Task.FromResult(Json(request.RequestUri.Query.Length > 0 ? Host() : Input));
        }));
        var discovery = new OscDiscovery(http, _ => Task.FromResult<IReadOnlyList<OscService>>([Service, Service]));
        var result = await discovery.DiscoverAsync(default);
        Assert.Equal(new OscDestination("127.0.0.1", 9010), result.Destination);
        Assert.Equal(new[] {"/?HOST_INFO", "/chatbox/input"}, paths);
        Assert.DoesNotContain("connected", result.Status, StringComparison.OrdinalIgnoreCase);
    }
    [Theory]
    [InlineData("0", "127.0.0.1", "UDP", "VRChat-Client-123456")]
    [InlineData("65536", "127.0.0.1", "UDP", "VRChat-Client-123456")]
    [InlineData("\"9010\"", "127.0.0.1", "UDP", "VRChat-Client-123456")]
    [InlineData("9000", "192.168.1.10", "UDP", "VRChat-Client-123456")]
    [InlineData("9000", "0.0.0.0", "UDP", "VRChat-Client-123456")]
    [InlineData("9000", "127.0.0.1", "TCP", "VRChat-Client-123456")]
    [InlineData("9000", "127.0.0.1", "UDP", "OtherApplication")]
    public void HostInfoFailsClosed(string port, string ip, string transport, string name)
    {
        using var document = JsonDocument.Parse(Host(port, ip, transport, name));
        Assert.Null(OscDiscovery.ParseHost(document.RootElement, Service));
    }
    [Theory]
    [InlineData("/avatar/change", "sTT", 2)]
    [InlineData("/chatbox/input", "s", 2)]
    [InlineData("/chatbox/input", "sii", 2)]
    [InlineData("/chatbox/input", "sTT", 1)]
    [InlineData("/chatbox/input", "sTT", 5)]
    public void RejectsWrongPathTypesAndReadOnlyEndpoints(string path, string type, int access)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new {FULL_PATH=path,TYPE=type,ACCESS=access}));
        Assert.False(OscDiscovery.WritableChatbox(document.RootElement));
    }
    [Fact]
    public async Task DiscoveryFiltersRemoteAndUnrelatedAdvertisementsAndRejectsAmbiguity()
    {
        var calls = 0;
        using var http = new HttpClient(new Handler((request, _) =>
        {
            calls++; return Task.FromResult(Json(request.RequestUri!.Query.Length > 0 ? Host(request.RequestUri.Port == 54321 ? "9000" : "9001") : Input));
        }));
        var invalid = new OscDiscovery(http, _ => Task.FromResult<IReadOnlyList<OscService>>([
            Service with {Address="203.0.113.10"}, Service with {Name="AnotherApp"}, Service with {QueryPort=0}]));
        Assert.Null((await invalid.DiscoverAsync(default)).Destination); Assert.Equal(0, calls);
        var ambiguous = new OscDiscovery(http, _ => Task.FromResult<IReadOnlyList<OscService>>([Service,Service with {QueryPort=54322}]));
        Assert.Null((await ambiguous.DiscoverAsync(default)).Destination);
        Assert.Equal(4,calls);
    }
    [Fact]
    public async Task MissingMalformedOversizedFailedAndCancelledDiscoveryPreserveFallback()
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Json(new string('x',16385)))));
        var discovery = new OscDiscovery(http, _=>Task.FromResult<IReadOnlyList<OscService>>([Service]));
        Assert.Null((await discovery.DiscoverAsync(default)).Destination);
        using var malformed = new HttpClient(new Handler((_,_)=>Task.FromResult(Json("{bad"))));
        Assert.Null((await new OscDiscovery(malformed,_=>Task.FromResult<IReadOnlyList<OscService>>([Service])).DiscoverAsync(default)).Destination);
        var failed = new OscDiscovery(http, _=>throw new System.Net.Sockets.SocketException());
        Assert.Null((await failed.DiscoverAsync(default)).Destination);
        using var cancellation = new CancellationTokenSource(25);
        var slow = new OscDiscovery(http, async token=>{await Task.Delay(Timeout.Infinite,token);return Array.Empty<OscService>();});
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>slow.DiscoverAsync(cancellation.Token));
    }
    [Fact]
    public void ExplicitManualAndReceiverRestartRejectLateResultsAndNeverRewriteSavedLan()
    {
        var settings = new AppSettings(Host:"192.168.1.10", Port:9003);
        Assert.False(OscDestinationSelection.InitiallyAutomatic(settings));
        Assert.True(OscDestinationSelection.InitiallyAutomatic(new()));
        Assert.False(OscDestinationSelection.InitiallyAutomatic(new(AutoDiscoverOsc:false)));
        var selection = new OscDestinationSelection();
        var revision = selection.SetMode(true);
        var found = new OscDiscoveryResult("VRChat discovered",new("127.0.0.1",9008));
        Assert.True(selection.Complete(revision,found)); Assert.Equal(9008,selection.Effective(settings).Port);
        selection.SetMode(false);
        Assert.False(selection.Complete(revision,found)); Assert.Equal(new("192.168.1.10",9003),selection.Effective(settings));
        revision=selection.SetMode(true); selection.Invalidate(); Assert.False(selection.Complete(revision,found));
        Assert.Equal(settings.Host,selection.Effective(settings).Host);
        Assert.True(selection.Complete(selection.Revision,new("Unavailable")));
        Assert.Equal(9003,selection.Effective(settings).Port);
    }
    private static HttpResponseMessage Json(string text) => new(HttpStatusCode.OK) {Content=new StringContent(text)};
    private sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>send(request,token);
    }
}
