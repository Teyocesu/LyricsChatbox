using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LyricsChatbox;

public record UpdateResult(string Status, Uri? Release = null);
public sealed class UpdateChecker(HttpClient http)
{
    public static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);
    private DateTimeOffset retryAfter;
    public static Version? StableVersion(string? tag)
    {
        if (tag is null || !Regex.IsMatch(tag, @"^v?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$")) return null;
        return Version.TryParse(tag.TrimStart('v'), out var version) ? version : null;
    }
    public Task<UpdateResult> CheckAtStartupAsync(bool enabled, Version current, CancellationToken token) => enabled
        ? CheckAsync(current, token) : Task.FromResult(new UpdateResult("Automatic checks are off"));
    public async Task<UpdateResult> CheckAsync(Version current, CancellationToken token)
    {
        if (DateTimeOffset.UtcNow < retryAfter) return new("GitHub is busy. Try again later.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(Deadline);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/Teyocesu/LyricsChatbox/releases/latest");
            request.Headers.UserAgent.ParseAdd("LyricsChatbox/" + current.ToString(3));
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            {
                retryAfter = response.Headers.RetryAfter?.Date ?? DateTimeOffset.UtcNow.Add(response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(15));
                if (retryAfter < DateTimeOffset.UtcNow.AddMinutes(1)) retryAfter = DateTimeOffset.UtcNow.AddMinutes(1);
                return new("GitHub is busy. Try again later.");
            }
            response.EnsureSuccessStatusCode();
            const int limit = 524288;
            if (response.Content.Headers.ContentLength > limit) return new("Could not read release information.");
            using var buffer = new MemoryStream();
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            var chunk = new byte[8192]; int count;
            while ((count = await stream.ReadAsync(chunk, deadline.Token)) > 0)
            {
                if (buffer.Length + count > limit) return new("Could not read release information.");
                buffer.Write(chunk, 0, count);
            }
            using var document = JsonDocument.Parse(buffer.ToArray());
            var root = document.RootElement;
            if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return new("No newer stable release.");
            var tag = root.GetProperty("tag_name").GetString();
            var latest = StableVersion(tag);
            if (latest is null) return new("Could not read release information.");
            if (latest <= new Version(current.Major, current.Minor, Math.Max(0, current.Build))) return new("You're up to date.");
            // Build a known project URL; never open a URL supplied by untrusted JSON.
            return new("LyricsChatbox " + tag + " is available", new Uri("https://github.com/Teyocesu/LyricsChatbox/releases/tag/" + tag));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException or IOException or JsonException or InvalidOperationException or KeyNotFoundException)
        { return new("Could not check for updates. Try again later."); }
    }
}
