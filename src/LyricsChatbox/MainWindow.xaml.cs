using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;

namespace LyricsChatbox;

public partial class MainWindow : Window
{
    private static readonly System.Windows.Media.FontFamily TextFont = new("Segoe UI"), LayoutFont = new("Consolas");
    private readonly LocalData data = new(LocalData.DefaultRoot);
    private readonly HttpClient http = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false });
    private readonly PlaybackSourceCoordinator playback;
    private readonly SynchronizationEngine engine = new();
    private readonly ChatboxScheduler scheduler = new();
    private readonly ChatboxOutput output = new();
    private readonly ManualChat manual = new();
    private readonly TypingSignal typing = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly LyricsResolver resolver;
    private readonly NetEaseLyricsProvider secondary;
    private readonly Dictionary<string, LyricsResolution> loaded = new();
    private readonly HashSet<Task> pending = new();
    private CancellationTokenSource? lookup;
    private AppSettings settings;
    private bool ready, closing, closed;
    private int receiverPid;
    private double nextReceiverCheck;
    private long acceptedPlaybackRevision = -1;

    public MainWindow()
    {
        secondary = new(http);
        resolver = new(http, data, secondary);
        settings = data.ReadSettings();
        playback = new(PlaybackSourceSetting.Parse(settings.PlaybackSource));
        profiles = data.ReadProfiles(settings);
        settings = profiles.Selected.Apply(settings);
        contextMode = profiles.Selected.ContextMode;
        quickMessages = data.ReadQuickMessages();
        ThemeColors.Apply(Application.Current.Resources, settings.Appearance);
        InitializeComponent();
        InitializeAppearance();
        engine.Enabled = settings.Enabled; engine.Offset = settings.Offset;
        EnabledBox.IsChecked = settings.Enabled; OffsetSlider.Value = settings.Offset;
        PlaybackSourceBox.ItemsSource = new[] { "Apple Music", "Spotify", "Automatic" };
        PlaybackSourceBox.SelectedItem = PlaybackSourceSetting.Parse(settings.PlaybackSource) switch
        {
            PlaybackSourceMode.Spotify => "Spotify", PlaybackSourceMode.Automatic => "Automatic", _ => "Apple Music"
        };
        HostBox.Text = settings.Host; PortBox.Text = settings.Port.ToString(CultureInfo.InvariantCulture);
        PresetBox.ItemsSource = ChatboxComposer.Presets; PresetBox.SelectedItem = settings.Preset;
        CustomAlignmentBox.ItemsSource = ManualAlignmentBox.ItemsSource = MessageLayout.Alignments;
        CustomAlignmentBox.SelectedItem = settings.CustomAlignment; ManualAlignmentBox.SelectedItem = settings.ManualAlignment;
        CustomAsciiBox.ItemsSource = ManualAsciiBox.ItemsSource = StatusAsciiBox.ItemsSource = MessageLayout.Templates;
        CustomAsciiBox.SelectedIndex = ManualAsciiBox.SelectedIndex = StatusAsciiBox.SelectedIndex = 0;
        TemplateBox.Text = settings.CustomTemplate; TemplateBox.IsEnabled = settings.Preset == "Custom";
        CustomPanel.Visibility = settings.Preset == "Custom" ? Visibility.Visible : Visibility.Collapsed;
        StatusPanel.Visibility = settings.Preset is "Custom" or "Status / Time" ? Visibility.Visible : Visibility.Collapsed;
        MessageBox.Text = settings.Message; CompactBox.IsChecked = settings.Compact;
        TypingBox.IsChecked = settings.TypingIndicator; LiveBox.IsChecked = settings.LiveEdit;
        OffsetText.Text = settings.Offset.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " s";
        output.Configure(settings.Host, settings.Port);
        playback.Observed += OnObserved;
        playback.ArtworkAvailable += OnArtwork;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        timer.Tick += (_, _) => Tick();
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); Save(); };
        Closing += OnClosing;
        Deactivated += (_, _) => { manual.Focus(false); Tick(); };
        InitializeLifecycle();
        InitializeUpdates();
        InitializeDiscovery();
        InitializePresentation();
        LoadCorrection();
        diagnostics.Add(DiagnosticCategory.Lifecycle, "Application started");
        ready = true; timer.Start(); playback.Start();
    }

    private void OnObserved(PlaybackSnapshot? snapshot, string status, long revision)
    {
        if (closing) return;
        _ = Dispatcher.InvokeAsync(() =>
        {
            if (closing || revision != playback.Revision) return;
            acceptedPlaybackRevision = revision;
            SourceText.Text = PresentationText.PlaybackStatus(playback.Mode, playback.ActiveKind, snapshot, status);
            ChoosePlaybackSourceButton.Visibility = playback.Ambiguous ? Visibility.Visible : Visibility.Collapsed;
            if (status == "Refreshing playback source" || playback.ActiveKind is null)
            {
                currentMusicVolume = null; volumeGeneration++; MusicVolumeSlider.IsEnabled = false;
                nextTransportRefresh = nextVolumeRefresh = 0;
                TransportStatus.Text = ""; TransportStatus.Visibility = Visibility.Collapsed;
            }
            if (engine.Observe(snapshot))
            {
                lookup?.Cancel();
                ClearArtwork();
                CancelManualMatch();
                ForgetMatchButton.IsEnabled = snapshot is not null && data.ReadManualAssociation(snapshot.Track) is not null;
                recordingIgnored = snapshot is not null && data.IsIgnored(snapshot.Track);
                LoadCorrection();
                diagnostics.Add(DiagnosticCategory.Playback, snapshot is null ? "Session/recording invalidated" : "Recording changed");
                // Invalidated state is rendered/scheduled before any lookup can start.
                Tick();
                if (engine.Track is not null) StartLookup(false);
            }
            Tick();
        });
    }

    private void StartLookup(bool force)
    {
        if (engine.Track is not { } track || closing) return;
        lookup?.Cancel(); lookup?.Dispose(); lookup = new();
        var epoch = engine.Epoch;
        recordingIgnored = data.IsIgnored(track);
        if (recordingIgnored) { engine.Complete(epoch, new(null, "Lyrics ignored for this recording", Outcome: LyricsOutcome.Ignored)); Tick(); return; }
        if (!force && loaded.TryGetValue(track.Key, out var memory)) { engine.Complete(epoch, memory with { FromCache = true }); return; }
        engine.BeginRetry();
        var task = ResolveAsync(track, epoch, lookup.Token);
        pending.Add(task);
        _ = task.ContinueWith(_ => Dispatcher.InvokeAsync(() => pending.Remove(task)), TaskScheduler.Default);
    }

    private async Task ResolveAsync(TrackIdentity track, long epoch, CancellationToken token)
    {
        var elapsed = Stopwatch.StartNew();
        try
        {
            // Disk parsing and HTTP run off the dispatcher; completion returns to the dispatcher.
            var result = await Task.Run(() => resolver.ResolveAsync(track, token, status =>
                _ = Dispatcher.InvokeAsync(() =>
                {
                    if (!closing && !token.IsCancellationRequested && engine.ReportProgress(epoch, status)) Tick();
                })), token);
            if (closing || token.IsCancellationRequested || !engine.Complete(epoch, result)) return;
            diagnostics.Add(DiagnosticCategory.Lyrics, result.Status + " · " + elapsed.ElapsedMilliseconds + " ms");
            if (result.Timeline is not null || result.Outcome == LyricsOutcome.Instrumental)
            {
                if (loaded.Count >= 20) loaded.Clear();
                loaded[track.Key] = result;
            }
            Tick();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (!closing && !token.IsCancellationRequested)
                engine.Complete(epoch, new(null, "Lyrics unavailable", DateTimeOffset.UtcNow.AddSeconds(60)));
        }
    }

    private void Tick()
    {
        if (closing) return;
        var now = MonotonicClock.Now;
        // A native metadata event can arrive before its dispatcher callback; never send the old track in that gap.
        if (acceptedPlaybackRevision != playback.Revision)
        {
            if (engine.Observe(null)) { lookup?.Cancel(); ClearArtwork(); CancelManualMatch(); LoadCorrection(); recordingIgnored = false; ForgetMatchButton.IsEnabled = false; }
        }
        if (now >= nextReceiverCheck)
        {
            nextReceiverCheck = now + 2;
            try
            {
                var processes = Process.GetProcessesByName("VRChat");
                var pid = processes.FirstOrDefault()?.Id ?? 0;
                foreach (var process in processes) process.Dispose();
                if (pid != receiverPid) { receiverPid = pid; scheduler.ReceiverChanged(); typing.Reset(); DiscoveryReceiverChanged(); }
            }
            catch (InvalidOperationException) { }
        }
        TickDiscovery(now);
        if (engine.RetryAt is { } retry && retry <= DateTimeOffset.UtcNow) StartLookup(true);
        var lyric = engine.Current(now);
        ApplyPlaybackView(lyric);
        RefreshTransport(now);
        var position = engine.Position(now);
        ApplyProgressView(now, position);
        UpdateLyricsDetails();
        var structured = settings.Preset is "Lyrics Only" or "Song + Lyrics";
        var automatic = structured ? LyricContextComposer.Compose(engine.Context(now), engine.Track, settings.Preset, contextMode, settings.Compact)
            : ChatboxComposer.Compose(ChatboxComposer.Template(settings.Preset, settings.CustomTemplate),
                engine.Track, lyric, settings.Message, DateTimeOffset.Now, position, true);
        var desired = manual.Desired(automatic, now);
        var preserveLayout = manual.IsManual || settings.Preset is "Custom" or "Status / Time";
        var alignment = manual.IsManual ? settings.ManualAlignment : settings.CustomAlignment;
        if (desired is not null && preserveLayout) desired = MessageLayout.Align(desired, alignment);
        var payload = ChatboxFormatter.Format(desired ?? MessageLayout.Align(manual.Draft, settings.ManualAlignment), settings.Compact, preserveLayout);
        var visible = ChatboxFormatter.Visible(payload);
        ApplyPreviewView(visible, preserveLayout, payload.Length, structured, desired);
        var manualLayout = MessageLayout.Align(manual.Draft, settings.ManualAlignment);
        var manualPayload = ChatboxFormatter.Format(manualLayout, settings.Compact, true);
        ApplyManualView(manualLayout, manualPayload, desired, now);
        scheduler.Set(engine.Epoch, desired ?? "", engine.Enabled && desired is not null, settings.Compact, manual.PendingSend, preserveLayout);
        if (scheduler.Take(now) is { } packet && packet.Epoch == engine.Epoch && engine.Enabled)
        {
            var sent = output.Send(packet.Text);
            scheduler.Complete(packet, sent);
            if (sent) manual.Sent(now);
        }
        if (typing.Take(engine.Enabled && settings.TypingIndicator && manual.Typing(now), now) is { } typingState)
            output.SendTyping(typingState);
        ApplyOscView();
    }

    private void HomeSectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ready) WindowLayout.Apply(this, ContentRoot.ActualHeight);
    }

    private void MinimizeWindow(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void ToggleMaximizeWindow(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();

    private void HeroSurfaceSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ((FrameworkElement)sender).Clip = new System.Windows.Media.RectangleGeometry(
            new Rect(new Point(), e.NewSize), 11, 11);
    }

    private void Navigate(object sender, RoutedEventArgs e)
    {
        if (!ready || sender is not RadioButton { Tag: string page }) return;
        HomePage.Visibility = page == "Home" ? Visibility.Visible : Visibility.Collapsed;
        DisplayPage.Visibility = page == "Display" ? Visibility.Visible : Visibility.Collapsed;
        ManualPage.Visibility = page == "Manual" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = page == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = page;
        WindowLayout.Apply(this, ContentRoot.ActualHeight);
    }
    private void DraftKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control && engine.Enabled)
        { manual.Send(); Tick(); e.Handled = true; }
    }

    private void Save()
    {
        settings = settings with { Enabled = engine.Enabled };
        if (!data.SaveSettings(settings)) ErrorText.Text = "Could not save settings; this session still works.";
        if (!data.SaveProfiles(profiles)) ProfileStatus.Text = "Could not save profiles. Changes apply only to this session.";
    }
    private async void PlaybackSourceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ready) return;
        var mode = (PlaybackSourceBox.SelectedItem as string) switch
        {
            "Spotify" => PlaybackSourceMode.Spotify,
            "Automatic" => PlaybackSourceMode.Automatic,
            _ => PlaybackSourceMode.AppleMusic
        };
        if (mode == playback.Mode) return;
        settings = settings with { PlaybackSource = mode.ToString() };
        Save();
        await playback.SetModeAsync(mode);
        Tick();
    }
    private void GoToPlaybackSourceSettings(object sender, RoutedEventArgs e)
    {
        SettingsNav.IsChecked = true;
        PlaybackSourceBox.Focus();
    }
    private void SetError(string message) => ErrorText.Text = message;
    private void ClearError() => ErrorText.Text = "";
    private void EnabledChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        engine.Enabled = EnabledBox.IsChecked == true;
        if (!engine.Enabled) manual.Resume();
        Save(); UpdateTray(); Tick();
    }
    private void DisplayChanged(object sender, RoutedEventArgs e)
    {
        if (!ready || changingProfiles) return;
        settings = settings with { Preset = PresetBox.SelectedItem as string ?? "Lyrics Only", CustomTemplate = TemplateBox.Text,
            Message = MessageBox.Text, Compact = CompactBox.IsChecked == true, TypingIndicator = TypingBox.IsChecked == true,
            LiveEdit = LiveBox.IsChecked == true, CustomAlignment = CustomAlignmentBox.SelectedItem as string ?? "Left",
            ManualAlignment = ManualAlignmentBox.SelectedItem as string ?? "Left" };
        TemplateBox.IsEnabled = settings.Preset == "Custom";
        CustomPanel.Visibility = settings.Preset == "Custom" ? Visibility.Visible : Visibility.Collapsed;
        StatusPanel.Visibility = settings.Preset is "Custom" or "Status / Time" ? Visibility.Visible : Visibility.Collapsed;
        RememberProfileChanges();
        UpdateTray();
        saveTimer.Stop(); saveTimer.Start(); Tick();
    }
    private void DraftFocused(object sender, RoutedEventArgs e) { if (ready) { manual.Focus(true); Tick(); } }
    private void DraftUnfocused(object sender, RoutedEventArgs e) { if (ready) { manual.Focus(false); Tick(); } }
    private void DraftChanged(object sender, RoutedEventArgs e)
    {
        if (!ready || loadingQuickDraft) return;
        manual.Focus(DraftBox.IsKeyboardFocusWithin);
        manual.Edit(DraftBox.Text, settings.LiveEdit, MonotonicClock.Now); Tick();
    }
    private void LiveChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        manual.LiveChanged(LiveBox.IsChecked == true); DisplayChanged(sender, e);
    }
    private void SendManual(object sender, RoutedEventArgs e) { manual.Send(); Tick(); }
    private void InsertAscii(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string target }) return;
        var box = target == "Manual" ? DraftBox : target == "Custom" ? TemplateBox : MessageBox;
        var selector = target == "Manual" ? ManualAsciiBox : target == "Custom" ? CustomAsciiBox : StatusAsciiBox;
        if (selector.SelectedItem is not AsciiTemplate template) return;
        var text = target == "Custom" ? template.Custom : template.Manual;
        var available = box.MaxLength - (box.Text.Length - box.SelectionLength);
        if (text.Length > available) { ErrorText.Text = "Not enough editor space for this template. Select text to replace it."; return; }
        box.SelectedText = text;
        ErrorText.Text = "";
        box.Focus();
    }
    private void ClearManual(object sender, RoutedEventArgs e) { DraftBox.Clear(); manual.Edit("", settings.LiveEdit, MonotonicClock.Now); manual.Send(); Tick(); }
    private void ResumeAutomatic(object sender, RoutedEventArgs e) { manual.Resume(); Tick(); }
    private void OffsetChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready || applyingCorrection) return;
        engine.Offset = Math.Round(e.NewValue, 1);
        UpdateCorrectionLabel(); Tick();
    }
    private void ApplyOsc(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(PortBox.Text, out var port)) port = 0;
        var next = settings with { Host = HostBox.Text.Trim(), Port = port, AutoDiscoverOsc = false };
        if (!next.IsValid) { ErrorText.Text = "Use an IP address or localhost, and a port from 1 to 65535."; return; }
        try
        {
            output.SendTyping(false); output.Configure(next.Host, next.Port); settings = next;
            discoveryCancellation?.Cancel(); destinationSelection.SetMode(false); configuredDestination = new(next.Host, next.Port);
            AutoOscBox.IsChecked = false; DiscoveryStatus.Text = "Manual destination"; ConfigureEffectiveDestination();
            scheduler.ReceiverChanged(); typing.Reset(); ClearError(); Save(); Tick();
        }
        catch (Exception ex) when (ex is System.Net.Sockets.SocketException or ArgumentException) { ErrorText.Text = "Could not configure OSC destination."; }
    }
    private async void ImportLrc(object sender, RoutedEventArgs e)
    {
        if (engine.Track is not { } track) return;
        var dialog = new OpenFileDialog { Filter = "Synchronized lyrics (*.lrc)|*.lrc", Title = "Choose lyrics for " + track.Title };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > LrcParser.MaxCharacters * 4) throw new IOException();
            var text = await File.ReadAllTextAsync(dialog.FileName);
            if (!data.SaveLocal(track, text)) { ErrorText.Text = "No usable timestamps, or the local file could not be saved."; return; }
            loaded.Remove(track.Key);
            if (engine.Track == track) StartLookup(true);
            ErrorText.Text = "Local LRC saved for this recording.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ErrorText.Text = "Could not read the LRC file."; }
    }
    private void RetryLyrics(object sender, RoutedEventArgs e) { ClearError(); StartLookup(true); }
    private void OpenData(object sender, RoutedEventArgs e)
    {
        try { Directory.CreateDirectory(data.Root); Process.Start(new ProcessStartInfo(data.Root) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception) { ErrorText.Text = "Could not open data folder."; }
    }
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (closed) return;
        e.Cancel = true;
        if (LifecyclePolicy.Close(settings, exitRequested) == WindowAction.Hide) { Hide(); return; }
        if (closing) return;
        closing = true; ready = false; timer.Stop(); saveTimer.Stop(); Save();
        DisposeTray();
        lifetime.Cancel();
        CancelManualMatch();
        ClearArtwork();
        playback.ArtworkAvailable -= OnArtwork;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        engine.Enabled = false; lookup?.Cancel(); playback.Observed -= OnObserved;
        manual.Resume(); output.SendTyping(false);
        await playback.DisposeAsync();
        try { await Task.WhenAll(pending.ToArray()); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { diagnostics.Add(DiagnosticCategory.Lifecycle, "Pending work ended during shutdown"); }
        lookup?.Dispose(); resolver.Dispose(); secondary.Dispose(); http.Dispose(); output.Dispose();
        discoveryCancellation?.Dispose(); discoveryHttp.Dispose();
        downloadCancellation?.Dispose(); installerHttp.Dispose();
        closed = true; Close();
    }
}
