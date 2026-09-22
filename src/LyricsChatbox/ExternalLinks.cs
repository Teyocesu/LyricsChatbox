using System.Diagnostics;
using System.IO;

namespace LyricsChatbox;

public static class ExternalLinks
{
    public const string BundledNoticeFileName = "THIRD_PARTY_NOTICES.md";

    public static bool IsAllowed(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return false;
        return uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("vrchat.com", StringComparison.OrdinalIgnoreCase);
    }

    public static string ResolveBundledNoticePath(string baseDirectory) =>
        Path.Combine(baseDirectory, BundledNoticeFileName);

    public static bool TryResolveExistingNotice(string baseDirectory, out string path)
    {
        path = ResolveBundledNoticePath(baseDirectory);
        try { return File.Exists(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    public static bool TryOpen(string? url, Action<string> report)
    {
        if (!IsAllowed(url)) { report("That link is not available."); return false; }
        try
        {
            Process.Start(new ProcessStartInfo(url!) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            report("Could not open your browser.");
            return false;
        }
    }
}
