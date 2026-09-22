namespace LyricsChatbox;

public sealed record OutputSidebarState(string Primary, string Secondary, bool ShowPause, string PauseContent, bool ShowResume);

public static class OutputSidebarPresentation
{
    public const string ScheduledHint = "Pause remains scheduled";

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
}
