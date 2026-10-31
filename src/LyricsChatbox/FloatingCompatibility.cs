namespace LyricsChatbox;

// Release policy, not client detection. Saved preferences stay untouched, so a future verified
// technique can reactivate them by changing Available alone.
public static class FloatingCompatibility
{
    public const bool Available = false;

    public static bool Effective(bool savedPreference, bool available = Available) => savedPreference && available;

    public static string SummaryLabel(bool savedPreference, bool available = Available) =>
        !savedPreference ? "" : available ? " · Floating" : " · Floating unavailable";
}
