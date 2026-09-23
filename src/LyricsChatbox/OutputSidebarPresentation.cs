using System.Globalization;

namespace LyricsChatbox;

public sealed record OutputSidebarState(string Primary, string Secondary, bool ShowPause, string PauseContent, bool ShowResume);

public static class OutputSidebarPresentation
{
    public const string ScheduledHint = "Pause remains scheduled";
    public const string ActiveStatusText = "OSC Output Active";
    public const string SendingText = "Sending lyrics...";
    public const string ReadyText = "Ready";

    public static OutputSidebarState Describe(bool enabled, bool paused, string summary)
    {
        if (!enabled)
            return paused
                ? new("Off", ScheduledHint, false, "Pause", false)
                : new("Off", "", false, "Pause", false);
        return paused
            ? new(summary, "", true, "Change", true)
            : new("Active", "", true, "Pause", false);
    }

    // The automatic lyric pipeline is operating (never a delivery claim):
    // master Output enabled, no pause, selected source Playing, automatic output available.
    public static bool IsSending(bool enabled, bool paused, bool sourcePlaying, bool automaticAvailable) =>
        enabled && !paused && sourcePlaying && automaticAvailable;

    // Compact factual rendering of the configured OSC destination for the sidebar.
    // IPv6 literals are bracketed; no receiver presence is implied.
    public static string FormatDestination(string host, int port)
    {
        var literal = host.Contains(':') ? "[" + host + "]" : host;
        return "To " + literal + ":" + port.ToString(CultureInfo.InvariantCulture);
    }
}
