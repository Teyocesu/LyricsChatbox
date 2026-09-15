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
            ? "Lyric context does not apply to Custom or Status layouts."
            : contextMode == "Adaptive" ? "Adds nearby lyrics when they fit. The current lyric always stays."
            : contextMode == "Previous + current + next" ? "Shows all three lyrics when they fit. The current lyric always stays."
            : "The current lyric always stays when space is limited.";
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
        if (profiles.Duplicate(CurrentProfile()) is { } next) ApplyProfileLibrary(next);
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
        var localIsAuthoritative = resolution?.Provider == "Local LRC" && resolution.Timeline is not null;
        var canSearch = hasTrack && track!.Duration is > 0 and <= 3600 && !recordingIgnored && !localIsAuthoritative;
        var searchHint = localIsAuthoritative ? "Imported Local LRC lyrics are authoritative. Remove the file from the data folder before choosing a remote match." : null;
        var lookingUp = engine.LyricsStatus is "Looking up synced lyrics" or "Searching another source…";
        var recovery = PresentationText.Recovery(hasTrack, engine.Timeline is not null, recordingIgnored, resolution?.Outcome, lookingUp);
        var source = resolution?.Provider ?? "—";
        var cache = resolution is null ? "—" : resolution.FromCache ? "Saved / loaded"
            : resolution.Provider == "Local LRC" ? "Local import" : resolution.Timeline is not null ? "Resolved this session" : "No usable lyrics";
        var match = recordingIgnored ? "Ignored" : resolution?.ManualMatch == true ? "User-selected recording"
            : resolution?.Provider == "Local LRC" ? "User-imported LRC" : resolution?.Outcome == LyricsOutcome.Found ? "Automatic match" : FriendlyLyricsStatus();
        static string Duration(double? seconds) => DurationFormatter.Format(seconds) is { Length: > 0 } text && seconds > 0 ? text : "—";
        static string Offset(double seconds) => seconds.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " s";
        var view = new LyricsDetailsView(hasTrack, canSearch, searchHint,
            recordingIgnored ? "Resume this recording" : "Ignore this recording", recovery, source, cache, match,
            Duration(track?.Duration) + " / " + Duration(resolution?.CandidateDuration), Offset(settings.Offset),
            savedCorrection is double correction ? Offset(correction) : "None", Offset(engine.Offset),
            ForgetMatchButton.IsEnabled ? "Saved choice" : "None", recordingIgnored ? "Yes · lookup paused" : "No");
        if (!lyricsDetailsView.ShouldApply(view)) return;
        ImportButton.IsEnabled = view.HasTrack;
        InspectorSearchButton.IsEnabled = view.CanSearch; InspectorSearchButton.ToolTip = view.SearchHint;
        HomeImportButton.IsEnabled = view.HasTrack;
        IgnoreButton.IsEnabled = view.HasTrack; IgnoreButton.Content = view.IgnoreContent;
        RecoveryCard.Visibility = view.Recovery.Visible ? Visibility.Visible : Visibility.Collapsed;
        RecoveryTitle.Text = view.Recovery.Title; RecoveryHint.Text = view.Recovery.Hint;
        HomeRetryButton.Visibility = view.Recovery.Retry ? Visibility.Visible : Visibility.Collapsed;
        InspectorSearchButton.Visibility = view.Recovery.Visible && !view.Recovery.Search ? Visibility.Collapsed : Visibility.Visible;
        HomeImportButton.Visibility = view.Recovery.Visible && !view.Recovery.Import ? Visibility.Collapsed : Visibility.Visible;
        IgnoreButton.Visibility = view.Recovery.Visible && !(view.Recovery.Ignore || view.Recovery.Resume) ? Visibility.Collapsed : Visibility.Visible;
        InspectorSource.Text = view.Source; InspectorCache.Text = view.Cache; InspectorMatch.Text = view.Match;
        InspectorDuration.Text = view.Duration; InspectorGlobalOffset.Text = view.GlobalOffset;
        InspectorSavedOffset.Text = view.SavedOffset; InspectorEffectiveOffset.Text = view.EffectiveOffset;
        InspectorManual.Text = view.Manual; InspectorIgnored.Text = view.Ignored;
    }
    private void ToggleIgnore(object sender, RoutedEventArgs e)
    {
        if (engine.Track is not { } track) return;
        var ignored = !recordingIgnored;
        if (!data.SetIgnored(track, ignored)) { SetError("Could not save this recording preference."); return; }
        ClearError();
        recordingIgnored = ignored; lookup?.Cancel(); CancelManualMatch(); loaded.Remove(track.Key);
        engine.InvalidateLyrics(ignored ? "Lyrics ignored for this recording" : "Looking up synced lyrics");
        Tick(); StartLookup(true);
    }
}
