namespace LyricsChatbox;

public sealed class DebouncedCommitState
{
    public bool Pending { get; private set; }
    public void Schedule() => Pending = true;
    public bool Consume()
    {
        if (!Pending) return false;
        Pending = false;
        return true;
    }
}
