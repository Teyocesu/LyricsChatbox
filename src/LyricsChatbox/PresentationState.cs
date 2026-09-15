namespace LyricsChatbox;

public sealed class PresentationChangeGate<T>
{
    private bool initialized;
    private T? current;
    public int AppliedCount { get; private set; }

    public bool ShouldApply(T next)
    {
        if (initialized && EqualityComparer<T>.Default.Equals(current, next)) return false;
        initialized = true;
        current = next;
        AppliedCount++;
        return true;
    }

    public void Reset() { initialized = false; current = default; }
}

public sealed class PresentationCadence(double intervalSeconds)
{
    private double next = double.NegativeInfinity;
    public bool IsDue(double now)
    {
        if (!double.IsFinite(now) || now < next) return false;
        next = now + intervalSeconds;
        return true;
    }
    public void Reset() => next = double.NegativeInfinity;
}
