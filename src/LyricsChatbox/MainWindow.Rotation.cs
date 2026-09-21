using System.Windows;
using System.Windows.Controls;

namespace LyricsChatbox;

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
    private bool changingRotation, addingRotation;
    private string? editingRotationId;
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

    private void RefreshRotationEditor(string? selectId = null, bool resetEditor = false)
    {
        var rotation = SelectedRotation();
        var items = rotation.Items!;
        var views = items.Select((item, index) => new RotationMessageView(item, index, items.Count)).ToArray();
        changingRotation = true;
        try
        {
            RotationEnabledBox.IsChecked = rotation.Enabled;
            RotationIntervalBox.SelectedValue = rotation.IntervalSeconds;
            RotationList.ItemsSource = views;
            AddRotationMessageButton.IsEnabled = items.Count < MessageRotation.Maximum;
            if (resetEditor) { addingRotation = false; editingRotationId = null; }
            if (!addingRotation)
            {
                var selected = views.FirstOrDefault(view => view.Id == (selectId ?? editingRotationId)) ?? views.FirstOrDefault();
                RotationList.SelectedItem = selected;
                ShowRotationEditor(selected?.Message);
            }
        }
        finally { changingRotation = false; }
        RotationCountText.Text = $"{items.Count} / {MessageRotation.Maximum} messages";
    }

    private void ShowRotationEditor(RotatingMessage? message)
    {
        editingRotationId = message?.Id;
        RotationEditorPanel.Visibility = message is null && !addingRotation ? Visibility.Collapsed : Visibility.Visible;
        if (message is not null)
        {
            RotationEditorTitle.Text = "Selected message";
            RotationMessageBox.Text = message.Text;
            RemoveRotationMessageButton.Visibility = Visibility.Visible;
        }
        else if (addingRotation)
        {
            RotationEditorTitle.Text = "New message";
            RotationMessageBox.Clear();
            RemoveRotationMessageButton.Visibility = Visibility.Collapsed;
        }
        RotationEditStatus.Text = "";
    }

    private void RotationSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (changingRotation || RotationList.SelectedItem is not RotationMessageView view) return;
        addingRotation = false;
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
        addingRotation = true;
        editingRotationId = null;
        changingRotation = true;
        RotationList.SelectedItem = null;
        changingRotation = false;
        ShowRotationEditor(null);
        RotationMessageBox.Focus();
    }

    private void SaveRotationMessage(object sender, RoutedEventArgs e)
    {
        var text = RotationMessageBox.Text;
        var id = addingRotation ? "message-" + Guid.NewGuid().ToString("N") : editingRotationId;
        if (id is null) return;
        var saved = ApplyRotationMutation(rotation => addingRotation
            ? rotation.Add(new RotatingMessage(id, text))
            : rotation.Edit(id, text), id);
        if (!saved)
        {
            RotationEditStatus.Text = "Use non-empty text within 512 characters and 9 lines.";
            return;
        }
        addingRotation = false;
        editingRotationId = id;
        RotationEditStatus.Text = "Message saved.";
    }

    private void RemoveRotationMessage(object sender, RoutedEventArgs e)
    {
        if (editingRotationId is not { } id) return;
        if (System.Windows.MessageBox.Show(this, "Remove this status message?", "Remove message",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        addingRotation = false;
        editingRotationId = null;
        if (ApplyRotationMutation(rotation => rotation.Delete(id), resetEditor: true))
            RotationEditStatus.Text = "Message removed.";
    }

    private void RotationItemEnabledChanged(object sender, RoutedEventArgs e)
    {
        if (changingRotation || sender is not CheckBox { DataContext: RotationMessageView view } checkBox) return;
        ApplyRotationMutation(rotation => rotation.SetItemEnabled(view.Id, checkBox.IsChecked == true), view.Id);
    }

    private void MoveRotationMessage(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: RotationMessageView view, Tag: string direction }) return;
        ApplyRotationMutation(rotation => rotation.Move(view.Id, direction == "Up" ? -1 : 1), view.Id);
    }

    private bool ApplyRotationMutation(Func<MessageRotation, MessageRotation?> mutation, string? selectId = null,
        bool resetEditor = false)
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
        RefreshProfiles();
        RefreshRotationEditor(selectId, resetEditor);
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
