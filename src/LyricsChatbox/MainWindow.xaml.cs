using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace LyricsChatbox;

public partial class MainWindow : Window
{
    private readonly LocalData data = new(LocalData.DefaultRoot);
    private readonly HttpClient http = new();
    private readonly AppleMusicPlayback playback = new();
    private readonly SynchronizationEngine engine = new();
    private readonly ChatboxScheduler scheduler = new();
    private readonly ChatboxOutput output = new();
    private readonly ManualChat manual = new();
    private readonly TypingSignal typing = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly LyricsResolver resolver;
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
        resolver = new(http, data);
        settings = data.ReadSettings();
        InitializeComponent();
        engine.Enabled = settings.Enabled; engine.Offset = settings.Offset;
        EnabledBox.IsChecked = settings.Enabled; OffsetSlider.Value = settings.Offset;
        HostBox.Text = settings.Host; PortBox.Text = settings.Port.ToString(CultureInfo.InvariantCulture);
        PresetBox.ItemsSource = ChatboxComposer.Presets; PresetBox.SelectedItem = settings.Preset;
        TemplateBox.Text = settings.CustomTemplate; TemplateBox.IsEnabled = settings.Preset == "Custom";
        MessageBox.Text = settings.Message; CompactBox.IsChecked = settings.Compact;
        TypingBox.IsChecked = settings.TypingIndicator; LiveBox.IsChecked = settings.LiveEdit;
        OffsetText.Text = settings.Offset.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " s";
        output.Configure(settings.Host, settings.Port);
        playback.Observed += OnObserved;
        timer.Tick += (_, _) => Tick();
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); Save(); };
        Closing += OnClosing;
        Deactivated += (_, _) => { manual.Focus(false); Tick(); };
        ready = true; timer.Start(); playback.Start();
    }

    private void OnObserved(PlaybackSnapshot? snapshot, string status, long revision)
    {
        if (closing) return;
        _ = Dispatcher.InvokeAsync(() =>
        {
            if (closing || revision != playback.Revision) return;
            acceptedPlaybackRevision = revision;
            SourceText.Text = status;
            if (engine.Observe(snapshot))
            {
                lookup?.Cancel();
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
        if (!force && loaded.TryGetValue(track.Key, out var memory)) { engine.Complete(epoch, memory); return; }
        engine.BeginRetry();
        var task = ResolveAsync(track, epoch, lookup.Token);
        pending.Add(task);
        _ = task.ContinueWith(_ => Dispatcher.InvokeAsync(() => pending.Remove(task)), TaskScheduler.Default);
    }

    private async Task ResolveAsync(TrackIdentity track, long epoch, CancellationToken token)
    {
        try
        {
            // Disk parsing and HTTP run off the dispatcher; completion returns to the dispatcher.
            var result = await Task.Run(() => resolver.ResolveAsync(track, token), token);
            if (closing || token.IsCancellationRequested || !engine.Complete(epoch, result)) return;
            if (result.RetryAt is null)
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
            if (engine.Observe(null)) lookup?.Cancel();
        }
        if (now >= nextReceiverCheck)
        {
            nextReceiverCheck = now + 2;
            try
            {
                var processes = Process.GetProcessesByName("VRChat");
                var pid = processes.FirstOrDefault()?.Id ?? 0;
                foreach (var process in processes) process.Dispose();
                if (pid != receiverPid) { receiverPid = pid; scheduler.ReceiverChanged(); typing.Reset(); }
            }
            catch (InvalidOperationException) { }
        }
        if (engine.RetryAt is { } retry && retry <= DateTimeOffset.UtcNow) StartLookup(true);
        var lyric = engine.Current(now);
        TrackText.Text = engine.Track?.Title ?? "Waiting for a track";
        ArtistText.Text = engine.Track?.Artist ?? "";
        LyricText.Text = lyric.Length > 0 ? lyric : "—";
        LyricsStatusText.Text = engine.LyricsStatus;
        var position = engine.Position(now);
        PositionText.Text = position.HasValue ? $"{TimeSpan.FromSeconds(position.Value):m\\:ss} / {TimeSpan.FromSeconds(Math.Clamp(engine.Track?.Duration ?? 0, 0, 86400)):m\\:ss}" : "Waiting for a fresh playback timeline";
        ImportButton.IsEnabled = engine.Track is not null;
        var automatic = ChatboxComposer.Compose(ChatboxComposer.Template(settings.Preset, settings.CustomTemplate),
            engine.Track, lyric, settings.Message, DateTimeOffset.Now, position);
        var desired = manual.Desired(automatic, now);
        var payload = ChatboxFormatter.Format(desired ?? manual.Draft, settings.Compact);
        var visible = ChatboxFormatter.Visible(payload);
        PreviewText.Text = visible.Length > 0 ? visible : "—";
        BudgetText.Text = payload.Length + " / 144";
        OwnerText.Text = !engine.Enabled ? "Output off · preview" : manual.IsManual
            ? desired is null ? "Manual · unsent draft" : "Manual · preview" : "Automatic · preview";
        SendButton.IsEnabled = ClearButton.IsEnabled = engine.Enabled;
        scheduler.Set(engine.Epoch, desired ?? "", engine.Enabled && desired is not null, settings.Compact, manual.PendingSend);
        if (scheduler.Take(now) is { } packet && packet.Epoch == engine.Epoch && engine.Enabled)
        {
            output.Send(packet.Text); manual.Sent(now);
        }
        if (typing.Take(engine.Enabled && settings.TypingIndicator && manual.Typing(now), now) is { } typingState)
            output.SendTyping(typingState);
        OscText.Text = output.Status;
    }

    private void Save()
    {
        settings = settings with { Enabled = engine.Enabled, Offset = engine.Offset };
        if (!data.SaveSettings(settings)) ErrorText.Text = "Could not save settings; this session still works.";
    }
    private void EnabledChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        engine.Enabled = EnabledBox.IsChecked == true;
        if (!engine.Enabled) manual.Resume();
        Save(); Tick();
    }
    private void DisplayChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        settings = settings with { Preset = PresetBox.SelectedItem as string ?? "Lyrics Only", CustomTemplate = TemplateBox.Text,
            Message = MessageBox.Text, Compact = CompactBox.IsChecked == true, TypingIndicator = TypingBox.IsChecked == true,
            LiveEdit = LiveBox.IsChecked == true };
        TemplateBox.IsEnabled = settings.Preset == "Custom";
        saveTimer.Stop(); saveTimer.Start(); Tick();
    }
    private void DraftFocused(object sender, RoutedEventArgs e) { if (ready) { manual.Focus(true); Tick(); } }
    private void DraftUnfocused(object sender, RoutedEventArgs e) { if (ready) { manual.Focus(false); Tick(); } }
    private void DraftChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        manual.Focus(DraftBox.IsKeyboardFocusWithin);
        manual.Edit(DraftBox.Text, settings.LiveEdit, MonotonicClock.Now); Tick();
    }
    private void LiveChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        manual.LiveChanged(LiveBox.IsChecked == true); DisplayChanged(sender, e);
    }
    private void SendManual(object sender, RoutedEventArgs e) { manual.Send(); Tick(); }
    private void ClearManual(object sender, RoutedEventArgs e) { DraftBox.Clear(); manual.Edit("", settings.LiveEdit, MonotonicClock.Now); manual.Send(); Tick(); }
    private void ResumeAutomatic(object sender, RoutedEventArgs e) { manual.Resume(); Tick(); }
    private void OffsetChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready) return;
        engine.Offset = Math.Round(e.NewValue, 1);
        OffsetText.Text = engine.Offset.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " s";
        saveTimer.Stop(); saveTimer.Start(); Tick();
    }
    private void ApplyOsc(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(PortBox.Text, out var port)) port = 0;
        var next = settings with { Host = HostBox.Text.Trim(), Port = port };
        if (!next.IsValid) { ErrorText.Text = "Use an IP address or localhost, and a port from 1 to 65535."; return; }
        try { output.SendTyping(false); output.Configure(next.Host, next.Port); settings = next; scheduler.ReceiverChanged(); typing.Reset(); ErrorText.Text = ""; Save(); Tick(); }
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
    private void RetryLyrics(object sender, RoutedEventArgs e) => StartLookup(true);
    private void OpenData(object sender, RoutedEventArgs e)
    {
        try { Directory.CreateDirectory(data.Root); Process.Start(new ProcessStartInfo(data.Root) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception) { ErrorText.Text = "Could not open data folder."; }
    }
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (closed) return;
        e.Cancel = true;
        if (closing) return;
        closing = true; ready = false; timer.Stop(); saveTimer.Stop(); Save();
        engine.Enabled = false; lookup?.Cancel(); playback.Observed -= OnObserved;
        manual.Resume(); output.SendTyping(false);
        await playback.DisposeAsync();
        await Task.WhenAll(pending.ToArray());
        lookup?.Dispose(); resolver.Dispose(); http.Dispose(); output.Dispose();
        closed = true; Close();
    }
}
