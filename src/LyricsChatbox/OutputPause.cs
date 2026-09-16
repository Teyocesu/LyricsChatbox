namespace LyricsChatbox;

public enum OutputPauseKind { None, Timed, UntilTrackChanges, UntilResumed }

public sealed record OutputPauseState(int Version = 1, string Kind = "None",
    DateTimeOffset? ExpiresUtc = null, string? TriggerTrackKey = null)
{
    public static OutputPauseState None { get; } = new();
    public OutputPauseKind Mode => Kind switch
    {
        nameof(OutputPauseKind.Timed) => OutputPauseKind.Timed,
        nameof(OutputPauseKind.UntilTrackChanges) => OutputPauseKind.UntilTrackChanges,
        nameof(OutputPauseKind.UntilResumed) => OutputPauseKind.UntilResumed,
        _ => OutputPauseKind.None
    };

    public static OutputPauseState Normalize(OutputPauseState? state, DateTimeOffset nowUtc)
    {
        if (state is not { Version: 1 }) return None;
        return state.Mode switch
        {
            OutputPauseKind.Timed when state.ExpiresUtc is { } expires && expires.ToUniversalTime() > nowUtc.ToUniversalTime() =>
                new(1, nameof(OutputPauseKind.Timed), expires.ToUniversalTime()),
            OutputPauseKind.UntilTrackChanges when ValidKey(state.TriggerTrackKey) =>
                new(1, nameof(OutputPauseKind.UntilTrackChanges), TriggerTrackKey: state.TriggerTrackKey!.ToUpperInvariant()),
            OutputPauseKind.UntilResumed => new(1, nameof(OutputPauseKind.UntilResumed)),
            _ => None
        };
    }

    private static bool ValidKey(string? key) => key is { Length: 64 } && key.All(Uri.IsHexDigit);
}

public readonly record struct OutputPauseChange(bool Applied, bool BecamePaused);
public readonly record struct OutputPauseEntryEffects(bool ClearText, bool ClearTyping)
{
    public static OutputPauseEntryEffects For(OutputPauseChange change, bool outputEnabled) =>
        change is { Applied: true, BecamePaused: true } && outputEnabled ? new(true, true) : new(false, false);
    public void Apply(Action clearText, Action clearTyping)
    {
        if (ClearText) clearText();
        if (ClearTyping) clearTyping();
    }
}

public sealed class OutputPauseController
{
    public OutputPauseState State { get; private set; }
    public OutputPauseKind Mode => State.Mode;
    public bool IsPaused(DateTimeOffset nowUtc) => Mode switch
    {
        OutputPauseKind.Timed => State.ExpiresUtc > nowUtc.ToUniversalTime(),
        OutputPauseKind.UntilTrackChanges or OutputPauseKind.UntilResumed => true,
        _ => false
    };

    public OutputPauseController(OutputPauseState? state, DateTimeOffset nowUtc) =>
        State = OutputPauseState.Normalize(state, nowUtc);

    public OutputPauseChange BeginTimed(DateTimeOffset nowUtc, TimeSpan duration)
    {
        if (duration != TimeSpan.FromMinutes(5) && duration != TimeSpan.FromMinutes(15) && duration != TimeSpan.FromMinutes(30))
            return new(false, false);
        return Replace(new(1, nameof(OutputPauseKind.Timed), nowUtc.ToUniversalTime().Add(duration)));
    }

    public OutputPauseChange BeginUntilTrackChanges(TrackIdentity? track) => track is null
        ? new(false, false)
        : Replace(new(1, nameof(OutputPauseKind.UntilTrackChanges), TriggerTrackKey: track.Key));

    public OutputPauseChange BeginUntilResumed() => Replace(new(1, nameof(OutputPauseKind.UntilResumed)));

    public bool Evaluate(DateTimeOffset nowUtc, TrackIdentity? acceptedTrack)
    {
        var expired = Mode == OutputPauseKind.Timed && State.ExpiresUtc <= nowUtc.ToUniversalTime();
        var changedTrack = Mode == OutputPauseKind.UntilTrackChanges && acceptedTrack is not null &&
            acceptedTrack.Key != State.TriggerTrackKey;
        if (!expired && !changedTrack) return false;
        State = OutputPauseState.None;
        return true;
    }

    public bool Resume()
    {
        if (Mode == OutputPauseKind.None) return false;
        State = OutputPauseState.None;
        return true;
    }

    public string Summary(DateTimeOffset nowUtc)
    {
        if (!IsPaused(nowUtc)) return "Active";
        return Mode switch
        {
            OutputPauseKind.Timed => "Paused · " + Math.Max(1, Math.Ceiling((State.ExpiresUtc!.Value - nowUtc.ToUniversalTime()).TotalMinutes)) + " min remaining",
            OutputPauseKind.UntilTrackChanges => "Paused · Until next track",
            OutputPauseKind.UntilResumed => "Paused · Until resumed",
            _ => "Active"
        };
    }

    private OutputPauseChange Replace(OutputPauseState next)
    {
        var becamePaused = Mode == OutputPauseKind.None;
        State = next;
        return new(true, becamePaused);
    }
}

public static class OutputEligibility
{
    public static bool CanSend(bool enabled, bool paused) => enabled && !paused;
}
