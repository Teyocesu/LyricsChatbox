namespace LyricsChatbox;

public sealed record RotatingMessage(string Id, string Text, bool Enabled = true)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsValid => MessageRotation.ValidId(Id) && MessageRotation.ValidText(Text);
}

public sealed record MessageRotation(int Version = 1, bool Enabled = false, int IntervalSeconds = 15,
    IReadOnlyList<RotatingMessage>? Items = null)
{
    public const int Maximum = 16;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsValid => Version == 1 && IntervalSeconds is 5 or 10 or 15 or 30 or 60 or 120 && Items is { Count: <= Maximum } &&
        Items.All(item => item is { IsValid: true }) &&
        Items.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() == Items.Count;

    [System.Text.Json.Serialization.JsonIgnore]
    public string LegacyMessage => Items?.FirstOrDefault(item => item.Enabled)?.Text ?? "";

    public static MessageRotation FromLegacy(string? message) => new(Items:
        ValidText(message) ? [new RotatingMessage("legacy-message", message!)] : []);

    public static bool ValidId(string? id) => LocalContentValidation.ValidId(id);

    public static bool ValidText(string? text) => LocalContentValidation.ValidText(text, 512, 9);

    public MessageRotation? SetEnabled(bool enabled) => Valid(this with { Enabled = enabled });
    public MessageRotation? SetInterval(int seconds) => Valid(this with { IntervalSeconds = seconds });
    public MessageRotation? Add(RotatingMessage item) => Valid(this with { Items = [.. Items ?? [], item] });
    public MessageRotation? Edit(string id, string text) => Items is { } items && items.Any(item => item.Id == id)
        ? Valid(this with { Items = items.Select(item => item.Id == id ? item with { Text = text } : item).ToArray() }) : null;
    public MessageRotation? Delete(string id) => Items is { } items && items.Any(item => item.Id == id)
        ? Valid(this with { Items = items.Where(item => item.Id != id).ToArray() }) : null;
    public MessageRotation? SetItemEnabled(string id, bool enabled) => Items is { } items && items.Any(item => item.Id == id)
        ? Valid(this with { Items = items.Select(item => item.Id == id ? item with { Enabled = enabled } : item).ToArray() }) : null;
    public MessageRotation? Move(string id, int offset)
    {
        if (offset is not (-1 or 1) || Items is null) return null;
        var items = Items.ToArray();
        var from = Array.FindIndex(items, item => item.Id == id);
        var to = from + offset;
        if (from < 0 || to < 0 || to >= items.Length) return null;
        (items[from], items[to]) = (items[to], items[from]);
        return Valid(this with { Items = items });
    }

    private static MessageRotation? Valid(MessageRotation rotation) => rotation.IsValid ? rotation : null;
}

public readonly record struct MessageRotationCurrent(string? ItemId, string Text, double RemainingSeconds);

public sealed class MessageRotator
{
    private sealed class State(string? currentId, double remainingSeconds, double now,
        MessageRotation rotation, RotatingMessage[] enabled, string[] enabledIds)
    {
        public string? CurrentId = currentId;
        public double RemainingSeconds = remainingSeconds;
        public double LastNow = now;
        public bool WasEligible;
        public MessageRotation Rotation = rotation;
        public RotatingMessage[] Enabled = enabled;
        public bool RotationEnabled = rotation.Enabled;
        public int IntervalSeconds = rotation.IntervalSeconds;
        public string[] EnabledIds = enabledIds;
    }

    private readonly Dictionary<string, State> states = [];
    private string? activeProfileId;

    public static bool IsEligible(bool consumesMessage, bool outputEnabled, bool outputPaused, bool automaticAvailable) =>
        consumesMessage && outputEnabled && !outputPaused && automaticAvailable;

    public MessageRotationCurrent Current(string profileId, MessageRotation rotation, string legacyMessage,
        double now, bool isEligible)
    {
        if (profileId is not { Length: > 0 and <= 64 }) throw new ArgumentException("Invalid profile ID.", nameof(profileId));
        if (!rotation.IsValid) throw new ArgumentException("Invalid message rotation.", nameof(rotation));
        if (!double.IsFinite(now)) throw new ArgumentOutOfRangeException(nameof(now));

        if (activeProfileId is not null && activeProfileId != profileId && states.TryGetValue(activeProfileId, out var previous))
        {
            Advance(previous, now);
            previous.WasEligible = false;
        }
        activeProfileId = profileId;

        RotatingMessage[] enabled;
        string[] enabledIds;
        if (!states.TryGetValue(profileId, out var state))
        {
            enabled = rotation.Items!.Where(item => item.Enabled).ToArray();
            enabledIds = enabled.Select(item => item.Id).ToArray();
            state = new(enabledIds.FirstOrDefault(), rotation.IntervalSeconds, now,
                rotation, enabled, enabledIds);
            states.Add(profileId, state);
        }
        else
        {
            Advance(state, now);
            if (ReferenceEquals(state.Rotation, rotation))
            {
                enabled = state.Enabled;
                enabledIds = state.EnabledIds;
            }
            else
            {
                enabled = rotation.Items!.Where(item => item.Enabled).ToArray();
                enabledIds = enabled.Select(item => item.Id).ToArray();
                Reconcile(state, rotation, enabledIds);
                state.Rotation = rotation;
                state.Enabled = enabled;
            }
        }

        state.WasEligible = rotation.Enabled && isEligible && enabledIds.Length > 1;
        state.LastNow = Math.Max(state.LastNow, now);
        var current = enabled.FirstOrDefault(item => item.Id == state.CurrentId) ?? enabled.FirstOrDefault();
        return new(current?.Id, current?.Text ?? legacyMessage ?? "", state.RemainingSeconds);
    }

    private static void Advance(State state, double now)
    {
        var elapsed = Math.Max(0, now - state.LastNow);
        state.LastNow = Math.Max(state.LastNow, now);
        if (!state.WasEligible || elapsed == 0) return;
        if (elapsed >= state.RemainingSeconds)
        {
            state.CurrentId = Next(state.CurrentId, state.EnabledIds);
            state.RemainingSeconds = state.IntervalSeconds;
        }
        else state.RemainingSeconds -= elapsed;
    }

    private static void Reconcile(State state, MessageRotation rotation, string[] enabledIds)
    {
        if (enabledIds.Length == 0)
        {
            state.CurrentId = null;
            state.RemainingSeconds = rotation.IntervalSeconds;
        }
        else if (!rotation.Enabled || !state.RotationEnabled)
        {
            state.CurrentId = enabledIds[0];
            state.RemainingSeconds = rotation.IntervalSeconds;
        }
        else
        {
            var reset = false;
            if (state.CurrentId is null)
            {
                state.CurrentId = enabledIds[0];
                reset = true;
            }
            else if (!enabledIds.Contains(state.CurrentId, StringComparer.Ordinal))
            {
                state.CurrentId = NextAfterRemoval(state.CurrentId, state.EnabledIds, enabledIds);
                reset = true;
            }
            if (state.IntervalSeconds != rotation.IntervalSeconds) reset = true;
            if (reset) state.RemainingSeconds = rotation.IntervalSeconds;
        }

        state.RotationEnabled = rotation.Enabled;
        state.IntervalSeconds = rotation.IntervalSeconds;
        state.EnabledIds = enabledIds;
    }

    private static string? Next(string? currentId, string[] enabledIds)
    {
        if (enabledIds.Length == 0) return null;
        var index = Array.IndexOf(enabledIds, currentId);
        return enabledIds[index < 0 || index == enabledIds.Length - 1 ? 0 : index + 1];
    }

    private static string NextAfterRemoval(string currentId, string[] previousIds, string[] enabledIds)
    {
        var previousIndex = Array.IndexOf(previousIds, currentId);
        if (previousIndex >= 0)
        {
            var next = enabledIds.FirstOrDefault(id => Array.IndexOf(previousIds, id) > previousIndex);
            if (next is not null) return next;
        }
        return enabledIds[0];
    }
}
