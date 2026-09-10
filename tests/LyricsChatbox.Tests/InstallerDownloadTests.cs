using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LyricsChatbox.Tests;

public class InstallerDownloadTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "LyricsChatbox.Tests", Guid.NewGuid().ToString("N"));
    private static readonly byte[] Binary = Encoding.UTF8.GetBytes("MZ synthetic installer test bytes; never executed");
    private static readonly InstallerAsset Asset = new("v0.5.1", Binary.Length);
    private static string Hash => Convert.ToHexString(SHA256.HashData(Binary));
    private static HttpResponseMessage Response(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
    private static HttpResponseMessage Text(string text) => Response(Encoding.UTF8.GetBytes(text));
    private static string Release(string? url = null, bool duplicate = false) => JsonSerializer.Serialize(new
    {
        draft = false, prerelease = false, tag_name = Asset.Tag, body = "Fixes for Japanese lyrics\nNew profile controls",
        assets = new[]
        {
            new { name=Asset.FileName, size=Asset.Size, state="uploaded", browser_download_url=url ?? Asset.DownloadUri.AbsoluteUri },
            new { name=duplicate ? Asset.FileName : Asset.FileName+".sha256", size=99L, state="uploaded", browser_download_url=Asset.ChecksumUri.AbsoluteUri }
        }
    });
    [Fact]
    public async Task ReleaseOffersOnlyKnownAssetsAndManualCheckIgnoresSkip()
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Text(Release()))));
        var checker = new UpdateChecker(http);
        var result = await checker.CheckAsync(new(0, 5, 0), default);
        Assert.Equal(Asset, result.Installer); Assert.Contains("Japanese", result.Notes);
        var skipped = await checker.CheckAtStartupAsync(true,new(0,5,0),default,"0.5.1");
        Assert.Null(skipped.Release); Assert.Null(skipped.Installer);
        Assert.NotNull((await checker.CheckAsync(new(0,5,0),default)).Installer);
        using var hostile = JsonDocument.Parse(Release("https://evil.invalid/payload.exe"));
        Assert.Null(InstallerAsset.FromRelease(hostile.RootElement,Asset.Tag));
        using var duplicate = JsonDocument.Parse(Release(duplicate:true));
        Assert.Null(InstallerAsset.FromRelease(duplicate.RootElement,Asset.Tag));
    }
    [Fact]
    public async Task VerifiedDownloadIsRetainedButChangedFileCannotBeLaunched()
    {
        var requests = new List<Uri>();
        using var http = new HttpClient(new Handler((request,_) =>
        {
            requests.Add(request.RequestUri!);
            return Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith(".sha256") ? Text(Hash+"  "+Asset.FileName+"\r\n") : Response(Binary));
        }));
        var result = await new InstallerDownloader(http).DownloadAsync(Asset,root,null,default);
        var installer = Assert.IsType<VerifiedInstaller>(result.Installer);
        Assert.Equal(new[] {Asset.ChecksumUri,Asset.DownloadUri},requests);
        Assert.Equal(Binary,await File.ReadAllBytesAsync(installer.Path)); Assert.Empty(Directory.GetFiles(root,"*.part"));
        using (var readable = await installer.OpenVerifiedAsync(default)) Assert.NotNull(readable);
        var changed=Binary.ToArray(); changed[0]=(byte)'X'; await File.WriteAllBytesAsync(installer.Path,changed);
        Assert.Null(await installer.OpenVerifiedAsync(default));
    }
    [Theory]
    [InlineData("mismatch")]
    [InlineData("short")]
    [InlineData("oversized")]
    [InlineData("missing-checksum")]
    [InlineData("wrong-file")]
    public async Task FailedIntegrityNeverProducesAnInstallerAndRemovesPartial(string failure)
    {
        using var http = new HttpClient(new Handler((request,_) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith(".sha256")) return Task.FromResult(failure=="missing-checksum"
                ? new HttpResponseMessage(HttpStatusCode.NotFound) : Text((failure=="mismatch"?new string('0',64):Hash)+"  "+(failure=="wrong-file"?"another.exe":Asset.FileName)));
            var bytes=failure=="short"?Binary[..^1]:failure=="oversized"?Binary.Concat(Binary).ToArray():Binary;
            return Task.FromResult(Response(bytes));
        }));
        var result=await new InstallerDownloader(http).DownloadAsync(Asset,root,null,default);
        Assert.Null(result.Installer); Assert.True(!Directory.Exists(root)||Directory.GetFiles(root).Length==0);
    }
    [Theory]
    [InlineData("http://release-assets.githubusercontent.com/asset")]
    [InlineData("https://evil.invalid/asset")]
    [InlineData("https://release-assets.githubusercontent.com.evil.invalid/asset")]
    [InlineData("https://user@release-assets.githubusercontent.com/asset")]
    [InlineData("https://release-assets.githubusercontent.com:444/asset")]
    public async Task RedirectOutsideGithubAssetOriginIsNeverRequested(string destination)
    {
        var calls=0;
        using var http=new HttpClient(new Handler((_,_)=>
        {
            calls++; var response=new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location=new(destination); return Task.FromResult(response);
        }));
        Assert.Null((await new InstallerDownloader(http).DownloadAsync(Asset,root,null,default)).Installer);
        Assert.Equal(1,calls);
    }
    [Fact]
    public async Task OfficialCdnRedirectAndCancellationAreHandledWithoutExecuting()
    {
        using var http=new HttpClient(new Handler((request,_)=>
        {
            if(request.RequestUri!.Host=="github.com" && !request.RequestUri.AbsolutePath.EndsWith(".sha256"))
            {var redirect=new HttpResponseMessage(HttpStatusCode.Redirect);redirect.Headers.Location=new("https://release-assets.githubusercontent.com/github-production-release-asset/test");return Task.FromResult(redirect);}
            return Task.FromResult(request.RequestUri.AbsolutePath.EndsWith(".sha256")?Text(Hash+"  "+Asset.FileName):Response(Binary));
        }));
        Assert.NotNull((await new InstallerDownloader(http).DownloadAsync(Asset,root,null,default)).Installer);
        using var cancel=new CancellationTokenSource(25);
        using var slow=new HttpClient(new Handler(async(_,token)=>{await Task.Delay(Timeout.Infinite,token);return Response(Binary);}));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new InstallerDownloader(slow).DownloadAsync(Asset,root,null,cancel.Token));
        Assert.Empty(Directory.GetFiles(root,"*.part"));
        Assert.Null(InstallerDownloader.ReadChecksum(Hash+"  "+Asset.FileName+"\n"+Hash+"  evil.exe",Asset.FileName));
    }
    private sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>send(request,token);}

    [Theory]
    [InlineData("size-string")]
    [InlineData("checksum-null")]
    [InlineData("array")]
    public void MalformedAssetMetadataFailsClosed(string shape)
    {
        var json = shape == "array" ? "[]" : shape == "size-string"
            ? Release().Replace("\"size\":" + Asset.Size, "\"size\":\"large\"")
            : Release().Replace("\"size\":99", "\"size\":null");
        using var document = JsonDocument.Parse(json);
        Assert.Null(InstallerAsset.FromRelease(document.RootElement, Asset.Tag));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChunkedOversizeAndMidDownloadCancellationRemoveWrittenPartial(bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var progressReported = false;
        using var http = new HttpClient(new Handler((request, _) => Task.FromResult(
            request.RequestUri!.AbsolutePath.EndsWith(".sha256") ? Text(Hash + "  " + Asset.FileName) :
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new ChunkedStream(Binary.Concat(Binary).ToArray())) })));
        var progress = new ImmediateProgress(_ =>
        {
            progressReported = true;
            Assert.Single(Directory.GetFiles(root, "*.part"));
            if (cancel) cancellation.Cancel();
        });
        if (cancel)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                new InstallerDownloader(http).DownloadAsync(Asset, root, progress, cancellation.Token));
        else
            Assert.Null((await new InstallerDownloader(http).DownloadAsync(Asset, root, progress, default)).Installer);
        Assert.True(progressReported);
        Assert.Empty(Directory.GetFiles(root));
    }
    private sealed class ImmediateProgress(Action<double> report) : IProgress<double>
    { public void Report(double value) => report(value); }
    private sealed class ChunkedStream(byte[] bytes) : MemoryStream(bytes, false)
    {
        public override bool CanSeek => false;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
            => base.ReadAsync(buffer[..Math.Min(buffer.Length, 8)], token);
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
