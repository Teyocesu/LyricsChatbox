namespace LyricsChatbox;

// Dispatcher-owned, two owners and one current draft. No history or deferred automatic messages.
public sealed class ManualChat
{
    public const double HoldSeconds = 8;
    public bool IsManual { get; private set; }
    public string Draft { get; private set; } = "";
    private string? desired;
    private bool focused;
    private bool pendingSend;
    public bool PendingSend => pendingSend;
    private double lastEdit = double.NegativeInfinity;
    private double resumeAt = double.PositiveInfinity;
    public double? RemainingHold(double now) => IsManual && double.IsFinite(resumeAt) ? Math.Max(0, resumeAt - now) : null;

    public void Focus(bool value)
    {
        focused = value;
        if (value) { IsManual = true; resumeAt = double.PositiveInfinity; }
    }
    public void Edit(string draft, bool live, double now)
    {
        Draft = draft; IsManual = true; lastEdit = now;
        resumeAt = double.PositiveInfinity; pendingSend = false;
        desired = live ? draft : null;
    }
    public void PrepareDraft(string draft)
    {
        Draft = draft; IsManual = true; desired = null; pendingSend = false;
        focused = false; lastEdit = double.NegativeInfinity; resumeAt = double.PositiveInfinity;
    }
    public void LiveChanged(bool live)
    {
        if (IsManual) { desired = live ? Draft : null; pendingSend = false; resumeAt = double.PositiveInfinity; }
    }
    public void Send()
    {
        IsManual = true; desired = Draft; pendingSend = true;
        lastEdit = double.NegativeInfinity; resumeAt = double.PositiveInfinity;
    }
    public void Sent(double now)
    {
        if (IsManual && pendingSend) { pendingSend = false; resumeAt = now + HoldSeconds; }
    }
    public void Resume()
    {
        IsManual = false; desired = null; pendingSend = false;
        lastEdit = double.NegativeInfinity; resumeAt = double.PositiveInfinity;
    }
    public string? Desired(string automatic, double now)
    {
        if (IsManual && now >= resumeAt) Resume();
        return IsManual ? desired : automatic;
    }
    public bool Typing(double now) => IsManual && focused && !pendingSend && Draft.Length > 0 && now - lastEdit < 3;
}

public sealed class TypingSignal
{
    private bool? last;
    private double nextRefresh;
    public void Reset() => last = null;
    public bool? Take(bool typing, double now)
    {
        if (typing == last && (!typing || now < nextRefresh)) return null;
        last = typing; nextRefresh = now + 2;
        return typing;
    }
}
