using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace LyricsChatbox;

public partial class MainWindow
{
    private ProfileLibrary profiles;
    private QuickMessageLibrary quickMessages;
    private string contextMode;
    private bool changingProfiles, loadingQuickDraft, recordingIgnored;

    private void InitializePresentation()
    {
        ContextBox.ItemsSource = LyricContextComposer.Modes;
        ContextBox.SelectedItem = contextMode;
        RefreshProfiles(); RefreshQuickMessages();
    }
    private void RefreshProfiles()
    {
        changingProfiles = true;
        try
        {
            ProfileCards.ItemsSource = profiles.Items;
            ProfileCards.SelectedValue = profiles.SelectedId;
            ProfileCards.ScrollIntoView(profiles.Selected);
            ProfileNameBox.Text = profiles.Selected.Name;
            DeleteProfileButton.IsEnabled = !profiles.Selected.BuiltIn;
        }
        finally { changingProfiles = false; }
    }
    private DisplayProfile CurrentProfile() => DisplayProfile.FromSettings(settings, profiles.SelectedId, profiles.Selected.Name)
        with { ContextMode = contextMode, BuiltIn = profiles.Selected.BuiltIn };
    private void RememberProfileChanges()
    {
        profiles = profiles.Save(CurrentProfile()) ?? profiles;
        ContextHint.Text = settings.Preset is "Custom" or "Status / Time"
            ? "Custom and status layouts use their template; lyric context does not apply."
            : contextMode == "Adaptive" ? "Fits current, next and previous lyrics in the available space."
            : "The current lyric takes priority when space is limited.";
        // Replacing immutable items updates card summaries without changing the selected profile.
        RefreshProfiles(); ProfileStatus.Text = "Changes save automatically. Appearance stays global.";
    }
    private void SelectProfile(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || changingProfiles || ProfileCards.SelectedValue is not string id || profiles.Select(id) is not { } next) return;
        ApplyProfileLibrary(next);
    }
    private void ApplyProfileLibrary(ProfileLibrary next)
    {
        profiles = next; settings = next.Selected.Apply(settings); contextMode = next.Selected.ContextMode;
        changingProfiles = true;
        try
        {
            PresetBox.SelectedItem = settings.Preset; TemplateBox.Text = settings.CustomTemplate;
            MessageBox.Text = settings.Message; CompactBox.IsChecked = settings.Compact;
            CustomAlignmentBox.SelectedItem = settings.CustomAlignment; ContextBox.SelectedItem = contextMode;
            TemplateBox.IsEnabled = settings.Preset == "Custom";
            CustomPanel.Visibility = settings.Preset == "Custom" ? Visibility.Visible : Visibility.Collapsed;
            StatusPanel.Visibility = settings.Preset is "Custom" or "Status / Time" ? Visibility.Visible : Visibility.Collapsed;
        }
        finally { changingProfiles = false; }
        RememberProfileChanges(); Save(); UpdateTray(); Tick();
    }
    private void ContextChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || changingProfiles || ContextBox.SelectedItem is not string mode) return;
        contextMode = mode; RememberProfileChanges(); saveTimer.Stop(); saveTimer.Start(); Tick();
    }
    private void NewProfile(object sender, RoutedEventArgs e)
    {
        if (profiles.Create("New profile", new("new", "New profile")) is { } next) ApplyProfileLibrary(next);
        else ProfileStatus.Text = "Up to 20 profiles can be saved. Remove a user profile first.";
        ShowPage("Display"); ProfileNameBox.Focus(); ProfileNameBox.SelectAll();
    }
    private void DuplicateProfile(object sender, RoutedEventArgs e)
    {
        var name = profiles.Selected.Name;
        if (profiles.Create(name[..Math.Min(name.Length, 34)] + " copy", CurrentProfile()) is { } next) ApplyProfileLibrary(next);
        else ProfileStatus.Text = "Up to 20 profiles can be saved. Remove a user profile first.";
    }
    private void RenameProfile(object sender, RoutedEventArgs e)
    {
        if (profiles.Save(CurrentProfile() with { Name = ProfileNameBox.Text.Trim() }) is not { } next)
        { ProfileStatus.Text = "Use a profile name from 1 to 40 characters."; return; }
        profiles = next; RefreshProfiles(); Save(); Tick();
    }
    private void DeleteProfile(object sender, RoutedEventArgs e)
    {
        if (profiles.Delete(profiles.SelectedId) is { } next) ApplyProfileLibrary(next);
    }
    private void ManageProfiles(object sender, RoutedEventArgs e) => ShowPage("Display");
    private void ManageQuickMessages(object sender, RoutedEventArgs e) => ShowPage("Manual");
    private void ShowPage(string page)
    {
        var item = NavigationPanel.Children.OfType<RadioButton>().FirstOrDefault(r => (string?)r.Tag == page);
        if (item is not null) item.IsChecked = true;
    }

    private void RefreshQuickMessages()
    {
        HomeQuickMessages.ItemsSource = ManualQuickMessages.ItemsSource = QuickEditBox.ItemsSource = quickMessages.Items;
        QuickEmptyText.Visibility = quickMessages.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void UseQuickMessage(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: QuickMessage message }) return;
        loadingQuickDraft = true;
        try
        {
            // TextChanged must not enter live edit while this reusable text is loaded.
            manual.PrepareDraft(message.Text); DraftBox.Text = message.Text;
            ShowPage("Manual"); DraftBox.Focus(); DraftBox.CaretIndex = DraftBox.Text.Length;
        }
        finally { loadingQuickDraft = false; }
        Tick();
    }
    private void EditQuickMessage(object sender, SelectionChangedEventArgs e)
    {
        if (QuickEditBox.SelectedItem is not QuickMessage message) return;
        QuickNameBox.Text = message.Name; QuickTextBox.Text = message.Text;
    }
    private void NewQuickMessage(object sender, RoutedEventArgs e)
    {
        QuickEditBox.SelectedItem = null; QuickNameBox.Clear(); QuickTextBox.Clear(); QuickNameBox.Focus();
    }
    private void SaveQuickMessage(object sender, RoutedEventArgs e)
    {
        var id = (QuickEditBox.SelectedItem as QuickMessage)?.Id ?? Guid.NewGuid().ToString("N");
        var next = quickMessages.Save(new(id, QuickNameBox.Text.Trim(), QuickTextBox.Text));
        if (next is null) { QuickStatus.Text = "Enter a name and message. Up to 16 messages can be saved."; return; }
        if (!data.SaveQuickMessages(next)) { QuickStatus.Text = "Could not save quick messages."; return; }
        quickMessages = next; RefreshQuickMessages(); QuickEditBox.SelectedItem = next.Items.First(q => q.Id == id);
        QuickStatus.Text = "Saved on this computer.";
    }
    private void DeleteQuickMessage(object sender, RoutedEventArgs e)
    {
        if (QuickEditBox.SelectedItem is not QuickMessage message) return;
        var next = quickMessages.Delete(message.Id);
        if (!data.SaveQuickMessages(next)) { QuickStatus.Text = "Could not remove this message."; return; }
        quickMessages = next; RefreshQuickMessages(); NewQuickMessage(sender, e); QuickStatus.Text = "Message removed.";
    }

    private string FriendlyLyricsStatus()
    {
        if (engine.Track is null || string.IsNullOrWhiteSpace(engine.Track.Title)) return "Play a song in Apple Music";
        if (recordingIgnored) return "Lyrics ignored for this recording";
        if (engine.LyricsStatus is "Looking up synced lyrics" or "Searching another source…")
            return engine.LyricsStatus == "Looking up synced lyrics" ? "Finding lyrics…" : engine.LyricsStatus;
        return engine.Resolution?.Outcome switch
        {
            LyricsOutcome.Found => "Synced · " + engine.Resolution.Provider,
            LyricsOutcome.Ambiguous or LyricsOutcome.Rejected => "No confident match found",
            LyricsOutcome.NotFound => "No synchronized lyrics found",
            LyricsOutcome.Instrumental => "Instrumental · no lyrics",
            LyricsOutcome.RateLimited => "Lyrics source busy · retrying later",
            LyricsOutcome.Timeout or LyricsOutcome.Unavailable => "Lyrics unavailable · retrying later",
            _ => engine.LyricsStatus
        };
    }
    private void UpdateLyricsDetails()
    {
        var track = engine.Track; var resolution = engine.Resolution;
        var hasTrack = !string.IsNullOrWhiteSpace(track?.Title);
        var canSearch = hasTrack && track!.Duration is > 0 and <= 3600 && !recordingIgnored;
        ImportButton.IsEnabled = hasTrack;
        ChooseMatchButton.IsEnabled = InspectorSearchButton.IsEnabled = canSearch;
        RecoveryImportButton.IsEnabled = hasTrack;
        RecoveryRetryButton.IsEnabled = hasTrack && !recordingIgnored;
        IgnoreButton.IsEnabled = hasTrack;
        IgnoreButton.Content = recordingIgnored ? "Resume this recording" : "Ignore this recording";
        RecoveryCard.Visibility = hasTrack && engine.Timeline is null ? Visibility.Visible : Visibility.Collapsed;
        RecoveryTitle.Text = FriendlyLyricsStatus();
        RecoveryHint.Text = recordingIgnored ? "Saved for this recording. Imported lyrics are kept."
            : "Retry, choose a recording yourself, or add a synchronized LRC file.";
        InspectorSource.Text = resolution?.Provider ?? "—";
        InspectorCache.Text = resolution is null ? "—" : resolution.FromCache ? "Saved / loaded"
            : resolution.Provider == "Local LRC" ? "Local import" : resolution.Timeline is not null ? "Resolved this session" : "No usable lyrics";
        InspectorMatch.Text = recordingIgnored ? "Ignored" : resolution?.ManualMatch == true ? "User-selected recording"
            : resolution?.Provider == "Local LRC" ? "User-imported LRC" : resolution?.Outcome == LyricsOutcome.Found ? "Automatic match" : FriendlyLyricsStatus();
        static string Duration(double? seconds) => seconds is > 0 && double.IsFinite(seconds.Value)
            ? TimeSpan.FromSeconds(Math.Min(seconds.Value, 86400)).ToString(@"m\:ss", CultureInfo.InvariantCulture) : "—";
        InspectorDuration.Text = Duration(track?.Duration) + " / " + Duration(resolution?.CandidateDuration);
        static string Offset(double seconds) => seconds.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " s";
        InspectorGlobalOffset.Text = Offset(settings.Offset);
        InspectorSavedOffset.Text = savedCorrection is double correction ? Offset(correction) : "None";
        InspectorEffectiveOffset.Text = Offset(engine.Offset);
        InspectorManual.Text = ForgetMatchButton.IsEnabled ? "Saved choice" : "None";
        InspectorIgnored.Text = recordingIgnored ? "Yes · lookup paused" : "No";
    }
    private void ToggleIgnore(object sender, RoutedEventArgs e)
    {
        if (engine.Track is not { } track) return;
        var ignored = !recordingIgnored;
        if (!data.SetIgnored(track, ignored)) { ErrorText.Text = "Could not save this recording preference."; return; }
        recordingIgnored = ignored; lookup?.Cancel(); CancelManualMatch(); loaded.Remove(track.Key);
        engine.InvalidateLyrics(ignored ? "Lyrics ignored for this recording" : "Looking up synced lyrics");
        Tick(); StartLookup(true);
    }
}
