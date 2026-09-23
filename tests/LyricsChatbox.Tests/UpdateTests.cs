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
    [InlineData("{\"draft\":false,\"prerelease\":true,\"tag_name\":\"v0.6.2\"}")]
    public async Task MalformedOrPrereleaseCannotOfferDownload(string body)
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) })));
        Assert.Null((await new UpdateChecker(http).CheckAsync(new(0, 4, 0), default)).Release);
    }
    [Theory]
    [InlineData("0.5.4", false, "v0.6.0", true)]
    [InlineData("0.6.0", false, "v0.6.0", false)]
    [InlineData("0.6.0", true, "v0.6.0", true)]
    [InlineData("0.6.0", true, "v0.5.4", false)]
    [InlineData("0.7.0", true, "v0.6.0", false)]
    [InlineData("0.5.4", false, "v0.5.4", false)]
    [InlineData("0.6.1", false, "v0.6.2", true)]
    [InlineData("0.6.2", false, "v0.6.2", false)]
    [InlineData("0.6.2", false, "v0.6.1", false)]
    [InlineData("0.6.2", false, "v0.7.0", true)]
    [InlineData("0.7.0", false, "v0.7.0", false)]
    public async Task PrereleaseAwareStableComparison(string current, bool prerelease, string latest, bool offered)
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Json(latest))));
        var result = await new UpdateChecker(http).CheckAsync(Version.Parse(current), default, prerelease);
        Assert.Equal(offered, result.Release is not null);
    }
    [Fact]
    public async Task PrereleasePayloadIsIgnoredEvenForPrereleaseBuilds()
    {
        const string body = "{\"draft\":false,\"prerelease\":true,\"tag_name\":\"v0.6.0-rc.1\"}";
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) })));
        Assert.Null((await new UpdateChecker(http).CheckAsync(new(0, 6, 0), default, true)).Release);
    }
    [Theory]
    [InlineData("v0.6.0-rc.1", null)]
    [InlineData("0.6.0-rc.1", null)]
    [InlineData("v0.6.0", "0.6.0")]
    [InlineData("0.5.4", "0.5.4")]
    [InlineData("v0.6.2", "0.6.2")]
    [InlineData("v0.6.2-rc.1", null)]
    [InlineData("v0.7.0", "0.7.0")]
    public void PrereleaseTagsAreNotStableVersions(string tag, string? expected)
    {
        Assert.Equal(expected is null ? null : Version.Parse(expected), UpdateChecker.StableVersion(tag));
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
