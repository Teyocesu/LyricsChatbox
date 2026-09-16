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
    private double? pausedHold;
    public double? RemainingHold(double now) => IsManual && double.IsFinite(resumeAt) ? Math.Max(0, resumeAt - now) : null;

    public void Focus(bool value)
    {
        focused = value;
        if (value) { IsManual = true; resumeAt = double.PositiveInfinity; pausedHold = null; }
    }
    public void Edit(string draft, bool live, double now)
    {
        Draft = draft; IsManual = true; lastEdit = now;
        resumeAt = double.PositiveInfinity; pausedHold = null; pendingSend = false;
        desired = live ? draft : null;
    }
    public void PrepareDraft(string draft)
    {
        Draft = draft; IsManual = true; desired = null; pendingSend = false;
        focused = false; lastEdit = double.NegativeInfinity; resumeAt = double.PositiveInfinity; pausedHold = null;
    }
    public void LiveChanged(bool live)
    {
        if (IsManual) { desired = live ? Draft : null; pendingSend = false; resumeAt = double.PositiveInfinity; pausedHold = null; }
    }
    public void Send()
    {
        IsManual = true; desired = Draft; pendingSend = true;
        lastEdit = double.NegativeInfinity; resumeAt = double.PositiveInfinity; pausedHold = null;
    }
    public void Sent(double now)
    {
        if (IsManual && pendingSend) { pendingSend = false; resumeAt = now + HoldSeconds; }
    }
    public void SuppressOutput()
    {
        desired = null; pendingSend = false;
    }
    public void PauseOutput(double now)
    {
        if (IsManual && pausedHold is null && double.IsFinite(resumeAt)) pausedHold = Math.Max(0, resumeAt - now);
        resumeAt = double.PositiveInfinity;
        SuppressOutput();
    }
    public void ResumeOutput(double now)
    {
        if (IsManual && pausedHold is { } remaining) resumeAt = now + remaining;
        pausedHold = null;
    }
    public void Resume()
    {
        IsManual = false; desired = null; pendingSend = false;
        lastEdit = double.NegativeInfinity; resumeAt = double.PositiveInfinity; pausedHold = null;
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
    public void Suppress(double now) { last = false; nextRefresh = now + 2; }
    public bool? Take(bool typing, double now)
    {
        if (typing == last && (!typing || now < nextRefresh)) return null;
        last = typing; nextRefresh = now + 2;
        return typing;
    }
}
