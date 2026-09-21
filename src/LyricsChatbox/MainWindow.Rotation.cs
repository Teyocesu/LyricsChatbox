using System.Windows;
using System.Windows.Controls;

namespace LyricsChatbox;

public enum RotationEditorMode { None, Adding, Editing }

public readonly record struct RotationEditorState(RotationEditorMode Mode, string? MessageId = null,
    bool ConfirmingRemoval = false)
{
    public static RotationEditorState None => new(RotationEditorMode.None);
    public static RotationEditorState Adding => new(RotationEditorMode.Adding);
    public static RotationEditorState Editing(string id) => new(RotationEditorMode.Editing, id);
    public bool ShowsEditor => Mode != RotationEditorMode.None;
    public bool CanRemove => Mode == RotationEditorMode.Editing && MessageId is not null;
    public RotationEditorState ConfirmRemoval() => CanRemove ? this with { ConfirmingRemoval = true } : this;
    public RotationEditorState CancelRemoval() => this with { ConfirmingRemoval = false };
}

public partial class MainWindow
{
    private sealed record RotationIntervalChoice(int Seconds, string Label);
    private sealed record RotationMessageView(RotatingMessage Message, int Index, int Count)
    {
        public string Id => Message.Id;
        public string Preview
        {
            get
            {
                var text = string.Join(" ", Message.Text.Replace("\r\n", "\n").Replace('\r', '\n')
                    .Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0));
                var elements = System.Globalization.StringInfo.ParseCombiningCharacters(text);
                return elements.Length <= 72 ? text : text[..elements[71]] + "…";
            }
        }
        public bool Enabled => Message.Enabled;
        public bool CanMoveUp => Index > 0;
        public bool CanMoveDown => Index + 1 < Count;
    }

    private static readonly RotationIntervalChoice[] RotationIntervals =
    [
        new(5, "5 seconds"), new(10, "10 seconds"), new(15, "15 seconds"),
        new(30, "30 seconds"), new(60, "1 minute"), new(120, "2 minutes")
    ];

    private readonly MessageRotator messageRotator = new();
    private bool changingRotation;
    private RotationEditorState rotationEditorState = RotationEditorState.None;
    private string? lastRotationHint;
    private MessageRotationCurrent activeRotationMessage;

    private void InitializeRotation()
    {
        RotationIntervalBox.ItemsSource = RotationIntervals;
        RotationIntervalBox.DisplayMemberPath = nameof(RotationIntervalChoice.Label);
        RotationIntervalBox.SelectedValuePath = nameof(RotationIntervalChoice.Seconds);
        RefreshRotationEditor();
    }

    private MessageRotation SelectedRotation() =>
        profiles.Selected.Rotation ?? MessageRotation.FromLegacy(profiles.Selected.Message);

    private void RefreshRotationEditor(RotationEditorState? nextState = null, bool preserveDraft = false)
    {
        if (nextState is { } requested) rotationEditorState = requested;
        var rotation = SelectedRotation();
        var items = rotation.Items!;
        var views = items.Select((item, index) => new RotationMessageView(item, index, items.Count)).ToArray();
        var selected = rotationEditorState.Mode == RotationEditorMode.Editing
            ? views.FirstOrDefault(view => view.Id == rotationEditorState.MessageId)
            : null;
        if (rotationEditorState.Mode == RotationEditorMode.Editing && selected is null)
            rotationEditorState = RotationEditorState.None;
        changingRotation = true;
        try
        {
            RotationEnabledBox.IsChecked = rotation.Enabled;
            RotationIntervalBox.SelectedValue = rotation.IntervalSeconds;
            RotationList.ItemsSource = views;
            AddRotationMessageButton.IsEnabled = items.Count < MessageRotation.Maximum;
            RotationList.SelectedItem = selected;
            ShowRotationEditor(selected?.Message, preserveDraft);
        }
        finally { changingRotation = false; }
        RotationCountText.Text = $"{items.Count} / {MessageRotation.Maximum} messages";
    }

    private void ShowRotationEditor(RotatingMessage? message, bool preserveDraft = false)
    {
        RotationEditorPanel.Visibility = rotationEditorState.ShowsEditor ? Visibility.Visible : Visibility.Collapsed;
        if (!rotationEditorState.ShowsEditor) return;
        if (rotationEditorState.Mode == RotationEditorMode.Editing && message is not null)
        {
            RotationEditorTitle.Text = "Selected message";
            if (!preserveDraft) RotationMessageBox.Text = message.Text;
            RemoveRotationMessageButton.Visibility = Visibility.Visible;
        }
        else
        {
            RotationEditorTitle.Text = "New message";
            if (!preserveDraft) RotationMessageBox.Clear();
            RemoveRotationMessageButton.Visibility = Visibility.Collapsed;
        }
        RotationEditorActions.Visibility = rotationEditorState.ConfirmingRemoval
            ? Visibility.Collapsed : Visibility.Visible;
        RotationRemoveConfirmation.Visibility = rotationEditorState.ConfirmingRemoval
            ? Visibility.Visible : Visibility.Collapsed;
        if (!preserveDraft) RotationEditStatus.Text = "";
    }

    private void RotationSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (changingRotation || RotationList.SelectedItem is not RotationMessageView view) return;
        rotationEditorState = RotationEditorState.Editing(view.Id);
        ShowRotationEditor(view.Message);
    }

    private void RotationEnabledChanged(object sender, RoutedEventArgs e)
    {
        if (!ready || changingRotation) return;
        ApplyRotationMutation(rotation => rotation.SetEnabled(RotationEnabledBox.IsChecked == true));
    }

    private void RotationIntervalChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || changingRotation || RotationIntervalBox.SelectedValue is not int seconds) return;
        ApplyRotationMutation(rotation => rotation.SetInterval(seconds));
    }

    private void AddRotationMessage(object sender, RoutedEventArgs e)
    {
        if (SelectedRotation().Items!.Count >= MessageRotation.Maximum)
        {
            RotationEditStatus.Text = "Up to 16 messages can be saved.";
            return;
        }
        rotationEditorState = RotationEditorState.Adding;
        changingRotation = true;
        RotationList.SelectedItem = null;
        changingRotation = false;
        ShowRotationEditor(null);
        RotationMessageBox.Focus();
    }

    private void SaveRotationMessage(object sender, RoutedEventArgs e)
    {
        var text = RotationMessageBox.Text;
        var adding = rotationEditorState.Mode == RotationEditorMode.Adding;
        var id = adding ? "message-" + Guid.NewGuid().ToString("N") : rotationEditorState.MessageId;
        if (id is null) return;
        var saved = ApplyRotationMutation(rotation => adding
            ? rotation.Add(new RotatingMessage(id, text))
            : rotation.Edit(id, text), RotationEditorState.Editing(id));
        if (!saved)
        {
            RotationEditStatus.Text = "Use non-empty text within 512 characters and 9 lines.";
            return;
        }
        RotationEditStatus.Text = "Message saved.";
    }

    private void RemoveRotationMessage(object sender, RoutedEventArgs e)
    {
        if (!rotationEditorState.CanRemove) return;
        rotationEditorState = rotationEditorState.ConfirmRemoval();
        ShowRotationEditor(SelectedRotation().Items!.FirstOrDefault(item => item.Id == rotationEditorState.MessageId),
            preserveDraft: true);
    }

    private void CancelRotationMessageRemoval(object sender, RoutedEventArgs e)
    {
        rotationEditorState = rotationEditorState.CancelRemoval();
        ShowRotationEditor(SelectedRotation().Items!.FirstOrDefault(item => item.Id == rotationEditorState.MessageId),
            preserveDraft: true);
    }

    private void ConfirmRemoveRotationMessage(object sender, RoutedEventArgs e)
    {
        if (rotationEditorState.MessageId is not { } id) return;
        ApplyRotationMutation(rotation => rotation.Delete(id), RotationEditorState.None);
    }

    private void RotationItemEnabledChanged(object sender, RoutedEventArgs e)
    {
        if (changingRotation || sender is not CheckBox { DataContext: RotationMessageView view } checkBox) return;
        var nextState = rotationEditorState.MessageId == view.Id
            ? RotationEditorState.Editing(view.Id) : rotationEditorState;
        ApplyRotationMutation(rotation => rotation.SetItemEnabled(view.Id, checkBox.IsChecked == true), nextState,
            preserveDraft: true);
    }

    private void MoveRotationMessage(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: RotationMessageView view, Tag: string direction }) return;
        ApplyRotationMutation(rotation => rotation.Move(view.Id, direction == "Up" ? -1 : 1),
            RotationEditorState.Editing(view.Id), preserveDraft: true);
    }

    private bool ApplyRotationMutation(Func<MessageRotation, MessageRotation?> mutation,
        RotationEditorState? nextState = null, bool preserveDraft = true)
    {
        if (mutation(SelectedRotation()) is not { } rotation) return false;
        var profile = CurrentProfile() with { Rotation = rotation };
        if (profiles.Save(profile) is not { } next)
        {
            ProfileStatus.Text = "Could not apply this message change.";
            return false;
        }
        profiles = next;
        settings = settings with { Message = profiles.Selected.Message };
        if (nextState is { } state) rotationEditorState = state;
        RefreshProfiles();
        RefreshRotationEditor(preserveDraft: preserveDraft);
        RefreshDisplayDerivedState();
        ProfileStatus.Text = "Changes save automatically. Appearance stays global.";
        Save();
        UpdateTray();
        Tick();
        return true;
    }

    private void ApplyRotationRuntimeView(MessageRotationCurrent current, bool eligible)
    {
        activeRotationMessage = current;
        if (StatusPanel.Visibility != Visibility.Visible) return;
        var rotation = SelectedRotation();
        var enabled = rotation.Items!.Where(item => item.Enabled).ToArray();
        var hint = !rotation.Enabled ? "Rotation off"
            : enabled.Length < 2 ? "Add another enabled message to rotate."
            : !eligible ? "Rotation paused while automatic output is unavailable"
            : $"Current message {Array.FindIndex(enabled, item => item.Id == current.ItemId) + 1} of {enabled.Length} · Next message in {Math.Ceiling(current.RemainingSeconds):0}s";
        if (hint == lastRotationHint) return;
        lastRotationHint = hint;
        RotationRuntimeText.Text = hint;
    }
}
