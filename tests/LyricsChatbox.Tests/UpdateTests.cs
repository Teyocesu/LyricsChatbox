using System.Net;
using System.Net.Http;

namespace LyricsChatbox.Tests;

public class UpdateTests
{
    [Theory]
    [InlineData("0.4.0", false)]
    [InlineData("0.4.1", true)]
    [InlineData("0.10.0", true)]
    [InlineData("1.0.0", true)]
    [InlineData("0.3.0", false)]
    [InlineData("0.5.0-rc.1", false)]
    [InlineData("garbage", false)]
    [InlineData("01.5.0", false)]
    public async Task StableNumericComparisonAndUntrustedUrl(string tag, bool newer)
    {
        using var http = new HttpClient(new Handler((request, _) =>
        {
            Assert.Equal(ProductIdentity.UserAgent, request.Headers.UserAgent.ToString());
            return Task.FromResult(Json(tag));
        }));
        var result = await new UpdateChecker(http).CheckAsync(new(0, 4, 0), default);
        Assert.Equal(newer, result.Release is not null);
        if (newer) Assert.StartsWith("https://github.com/Teyocesu/LyricsChatbox/releases/tag/", result.Release!.AbsoluteUri);
    }
    [Fact]
    public async Task DisabledAutomaticChecksMakeNoRequestAndRateLimitHasCooldown()
    {
        var calls = 0;
        using var http = new HttpClient(new Handler((request, _) =>
        {
            Assert.Equal(ProductIdentity.UserAgent, request.Headers.UserAgent.ToString());
            calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        }));
        var checker = new UpdateChecker(http);
        await checker.CheckAtStartupAsync(false, new(0, 4, 0), default); Assert.Equal(0, calls);
        await checker.CheckAsync(new(0, 4, 0), default); await checker.CheckAsync(new(0, 4, 0), default);
        Assert.Equal(1, calls);
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("not json")]
    [InlineData("{\"draft\":false,\"prerelease\":true,\"tag_name\":\"v9.0.0\"}")]
    public async Task MalformedOrPrereleaseCannotOfferDownload(string body)
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) })));
        Assert.Null((await new UpdateChecker(http).CheckAsync(new(0, 4, 0), default)).Release);
    }
    [Fact]
    public async Task TimeoutAndCancellationAreBounded()
    {
        using var http = new HttpClient(new Handler(async (_, token) => { await Task.Delay(Timeout.Infinite, token); return Json("v1.0.0"); }));
        var checker = new UpdateChecker(http);
        using var cancel = new CancellationTokenSource(25);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => checker.CheckAsync(new(0, 4, 0), cancel.Token));
        Assert.Null((await checker.CheckAsync(new(0, 4, 0), default)).Release);
    }
    [Fact]
    public async Task ReleaseNotesAreShownAsPlainText()
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Json("v0.5.2", "# Improved\n\n- **Clear** [notes](https://example.com)"))));
        var result = await new UpdateChecker(http).CheckAsync(new(0, 5, 1), default);
        Assert.Equal("Improved\n\n• Clear notes (https://example.com/)", result.Notes);
    }
    private static HttpResponseMessage Json(string tag, string body = "") => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
        System.Text.Json.JsonSerializer.Serialize(new { draft = false, prerelease = false, tag_name = tag, html_url = "https://evil.invalid/", body, assets = Array.Empty<object>() }))
    };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken); }
}
