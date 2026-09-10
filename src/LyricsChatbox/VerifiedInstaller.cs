using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LyricsChatbox;

public record InstallerAsset(string Tag, long Size)
{
    public const long MaximumBytes = 250_000_000;
    public string FileName => "LyricsChatbox-Setup-" + UpdateChecker.StableVersion(Tag)?.ToString(3) + ".exe";
    public Uri DownloadUri => new("https://github.com/Teyocesu/LyricsChatbox/releases/download/" + Tag + "/" + FileName);
    public Uri ChecksumUri => new(DownloadUri.AbsoluteUri + ".sha256");
    public bool IsValid => UpdateChecker.StableVersion(Tag) is not null && Size is > 0 and <= MaximumBytes;
    public static InstallerAsset? FromRelease(JsonElement root, string tag)
    {
        if (root.ValueKind != JsonValueKind.Object || UpdateChecker.StableVersion(tag) is null ||
            !root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return null;
        var descriptor = new InstallerAsset(tag, 1);
        var expected = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var item in assets.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String) continue;
            var value = name.GetString();
            if (value != descriptor.FileName && value != descriptor.FileName + ".sha256") continue;
            if (!expected.TryAdd(value, item)) return null;
        }
        if (expected.Count != 2) return null;
        foreach (var pair in expected)
        {
            var item = pair.Value;
            var uri = pair.Key.EndsWith(".sha256", StringComparison.Ordinal) ? descriptor.ChecksumUri : descriptor.DownloadUri;
            if (!item.TryGetProperty("browser_download_url", out var link) || link.ValueKind != JsonValueKind.String || link.GetString() != uri.AbsoluteUri ||
                !item.TryGetProperty("state", out var state) || state.ValueKind != JsonValueKind.String || state.GetString() != "uploaded") return null;
        }
        var binary = expected[descriptor.FileName]; var checksum = expected[descriptor.FileName + ".sha256"];
        if (!binary.TryGetProperty("size", out var size) || size.ValueKind != JsonValueKind.Number || !size.TryGetInt64(out var length) ||
            !checksum.TryGetProperty("size", out var checksumSize) || checksumSize.ValueKind != JsonValueKind.Number ||
            !checksumSize.TryGetInt64(out var checksumLength) || checksumLength is <= 0 or > 4096) return null;
        descriptor = descriptor with { Size = length };
        return descriptor.IsValid ? descriptor : null;
    }
}

public record VerifiedInstaller(string Path, string Sha256, long Size)
{
    // Recheck immediately before offering the file to Windows; never trust a path from an earlier download alone.
    public async Task<FileStream?> OpenVerifiedAsync(CancellationToken token)
    {
        FileStream? stream = null;
        try
        {
            stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length != Size || Convert.ToHexString(await SHA256.HashDataAsync(stream, token)) != Sha256.ToUpperInvariant())
            { stream.Dispose(); return null; }
            return stream;
        }
        catch (OperationCanceledException) { stream?.Dispose(); throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { stream?.Dispose(); return null; }
    }
}
public record InstallerDownloadResult(string Status, VerifiedInstaller? Installer = null);

public sealed class InstallerDownloader(HttpClient http)
{
    public static readonly TimeSpan Deadline = TimeSpan.FromMinutes(3);
    public async Task<InstallerDownloadResult> DownloadAsync(InstallerAsset asset, string directory, IProgress<double>? progress, CancellationToken token)
    {
        if (!asset.IsValid) return new("No verifiable installer is available for this release.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(Deadline);
        string? temporary = null;
        try
        {
            using var checksumResponse = await GetAssetAsync(asset.ChecksumUri, deadline.Token);
            await using var checksumStream = await checksumResponse.Content.ReadAsStreamAsync(deadline.Token);
            using var checksum = new MemoryStream();
            await CopyBounded(checksumStream, checksum, 4096, null, deadline.Token);
            var hash = ReadChecksum(Encoding.UTF8.GetString(checksum.ToArray()), asset.FileName);
            if (hash is null) return new("The release checksum is missing or invalid. Installer was not downloaded.");
            Directory.CreateDirectory(directory);
            temporary = System.IO.Path.Combine(directory, Guid.NewGuid().ToString("N") + ".part");
            using var response = await GetAssetAsync(asset.DownloadUri, deadline.Token);
            if (response.Content.Headers.ContentLength is long announced && announced != asset.Size)
                return new("Installer size differs from the release. Download rejected.");
            await using (var stream = await response.Content.ReadAsStreamAsync(deadline.Token))
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.Asynchronous))
            {
                await CopyBounded(stream, file, asset.Size, progress, deadline.Token);
                if (file.Length != asset.Size) return new("Installer download was incomplete. Try again.");
                file.Position = 0;
                var actual = Convert.ToHexString(await SHA256.HashDataAsync(file, deadline.Token));
                if (!actual.Equals(hash, StringComparison.OrdinalIgnoreCase)) return new("SHA256 mismatch. Installer rejected and removed.");
            }
            deadline.Token.ThrowIfCancellationRequested();
            var target = System.IO.Path.Combine(directory, Guid.NewGuid().ToString("N") + "-" + asset.FileName);
            File.Move(temporary, target); temporary = null;
            return new("SHA256 verified. Ready to open the installer.", new(target, hash, asset.Size));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException or OperationCanceledException)
        { return new("Installer could not be downloaded and verified. Try again."); }
        finally
        {
            if (temporary is not null)
                try { File.Delete(temporary); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
    public static string? ReadChecksum(string text, string fileName)
    {
        var match = Regex.Match(text.Trim('\uFEFF', '\r', '\n', ' '), @"\A([0-9a-fA-F]{64})[ \t]+\*?([^\r\n]+)\z");
        return match.Success && match.Groups[2].Value == fileName ? match.Groups[1].Value.ToUpperInvariant() : null;
    }
    private async Task<HttpResponseMessage> GetAssetAsync(Uri uri, CancellationToken token)
    {
        for (var hop = 0; hop < 4; hop++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("LyricsChatbox/0.5.0");
            var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (response.StatusCode is HttpStatusCode.Moved or HttpStatusCode.Redirect or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
            {
                var next = response.Headers.Location;
                response.Dispose();
                if (next is null || !next.IsAbsoluteUri || next.Scheme != "https" || !next.IsDefaultPort || next.UserInfo.Length > 0 ||
                    next.Host != "release-assets.githubusercontent.com") throw new HttpRequestException("Unexpected asset redirect");
                uri = next; continue;
            }
            if (!response.IsSuccessStatusCode) { response.Dispose(); throw new HttpRequestException("Asset download failed"); }
            return response;
        }
        throw new HttpRequestException("Too many asset redirects");
    }
    private static async Task CopyBounded(Stream source, Stream destination, long maximum, IProgress<double>? progress, CancellationToken token)
    {
        var chunk = new byte[65536]; long length = 0; int count; var previous = -1;
        while ((count = await source.ReadAsync(chunk, token)) > 0)
        {
            length += count;
            if (length > maximum) throw new IOException("Asset exceeds expected size");
            await destination.WriteAsync(chunk.AsMemory(0, count), token);
            var percentage = (int)(100 * length / maximum);
            if (percentage != previous) { progress?.Report(percentage); previous = percentage; }
        }
    }
}
