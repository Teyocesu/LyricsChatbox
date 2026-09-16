using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LyricsChatbox;

public partial class MainWindow
{
    private double nextTransportRefresh;
    private bool changingTransport;
    private bool readingVolume, writingVolume;
    private MusicVolume? currentMusicVolume;
    private double nextVolumeRefresh;
    private long volumeGeneration;
    private bool updatingVolumeSlider;
    private readonly DebouncedCommitState volumeCommit = new();
    private sealed record TransportView(MediaControls Controls, bool Busy);
    private readonly PresentationChangeGate<TransportView> transportView = new();
    private readonly System.Windows.Threading.DispatcherTimer volumeSendTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private bool volumeTimerInitialized;
    private void RefreshTransport(double now)
    {
        if (now < nextTransportRefresh) return;
        nextTransportRefresh = now + 0.5;
        var state = playback.GetControls();
        var view = new TransportView(state, changingTransport);
        if (transportView.ShouldApply(view))
        {
            PlayPauseButton.IsEnabled = !view.Busy && state.PlayPause;
            PreviousButton.IsEnabled = !view.Busy && state.Previous;
            NextButton.IsEnabled = !view.Busy && state.Next;
            ShuffleButton.IsEnabled = !view.Busy && state.Shuffle && state.Shuffling.HasValue;
            RepeatButton.IsEnabled = !view.Busy && state.Repeat && state.RepeatMode.HasValue;
            PlayPauseButton.Content = state.Playing ? "\uE769" : "\uE768";
        }
        if (now >= nextVolumeRefresh && !readingVolume && !writingVolume)
        {
            nextVolumeRefresh = now + 2;
            _ = RefreshVolumeAsync();
        }
    }
    private async Task RefreshVolumeAsync()
    {
        readingVolume = true;
        var generation = volumeGeneration;
        try
        {
            var provider = playback.Volume;
            var sourceRevision = playback.Revision;
            var volume = provider is null ? null : await TrackManualTask(Task.Run(provider.Read));
            if (closing || generation != volumeGeneration || writingVolume || sourceRevision != playback.Revision) return;
            currentMusicVolume = volume;
            var enabled = volume is not null;
            if (MusicVolumeSlider.IsEnabled != enabled) MusicVolumeSlider.IsEnabled = enabled;
            var player = playback.ActiveKind == PlaybackSourceKind.Spotify ? "Spotify" : "Apple Music";
            var tooltip = enabled ? player + " volume" : player + " audio session unavailable";
            if (!Equals(MusicVolumeSlider.ToolTip, tooltip)) MusicVolumeSlider.ToolTip = tooltip;
            if (volume is not null && !MusicVolumeSlider.IsMouseCaptureWithin && !MusicVolumeSlider.IsKeyboardFocusWithin)
            {
                if (Math.Abs(MusicVolumeSlider.Value - volume.Level) > 0.0001)
                {
                    updatingVolumeSlider = true;
                    try { MusicVolumeSlider.Value = volume.Level; }
                    finally { updatingVolumeSlider = false; }
                }
            }
        }
        finally { readingVolume = false; }
    }
    private void MusicVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready || closing || updatingVolumeSlider || currentMusicVolume is null) return;
        if (!volumeTimerInitialized)
        {
            volumeTimerInitialized = true;
            volumeSendTimer.Tick += (_, _) => { volumeSendTimer.Stop(); if (volumeCommit.Consume()) _ = SetMusicVolumeAsync(); };
        }
        volumeGeneration++;
        volumeCommit.Schedule();
        volumeSendTimer.Stop(); volumeSendTimer.Start();
    }
    private void CommitMusicVolume()
    {
        volumeSendTimer.Stop();
        if (volumeCommit.Consume()) _ = SetMusicVolumeAsync();
    }
    private void VolumeReleased(object sender, MouseButtonEventArgs e) => CommitMusicVolume();
    private void VolumeKeyReleased(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Home or Key.End)
            CommitMusicVolume();
    }
    private async Task SetMusicVolumeAsync()
    {
        if (closing || currentMusicVolume is not { } expected) return;
        if (writingVolume) { volumeCommit.Schedule(); return; }
        writingVolume = true;
        var generation = ++volumeGeneration;
        var value = (float)MusicVolumeSlider.Value;
        try
        {
            var provider = playback.Volume;
            var sourceRevision = playback.Revision;
            var accepted = provider is not null && await TrackManualTask(Task.Run(() =>
                sourceRevision == playback.Revision && ReferenceEquals(provider, playback.Volume) && provider.Set(expected, value)));
            if (closing || generation != volumeGeneration || sourceRevision != playback.Revision) return;
            TransportStatus.Text = accepted ? "" : (playback.ActiveKind == PlaybackSourceKind.Spotify ? "Spotify" : "Apple Music") + " volume unavailable; try again.";
            TransportStatus.Visibility = accepted ? Visibility.Collapsed : Visibility.Visible;
        }
        finally
        {
            writingVolume = false;
            nextVolumeRefresh = 0;
            if (!closing && (generation != volumeGeneration || volumeCommit.Pending))
            {
                volumeSendTimer.Stop();
                volumeSendTimer.Start();
            }
        }
    }
    private async void TransportClicked(object sender, RoutedEventArgs e)
    {
        if (changingTransport || sender is not Button { Tag: string tag } || !Enum.TryParse<MediaCommand>(tag, out var command)) return;
        changingTransport = true;
        try
        {
            var sourceRevision = playback.Revision;
            var accepted = await playback.ControlAsync(command, sourceRevision);
            if (closing || sourceRevision != playback.Revision) return;
            TransportStatus.Text = accepted ? "" : (playback.ActiveKind == PlaybackSourceKind.Spotify ? "Spotify" : "Apple Music") + " did not accept the command.";
            TransportStatus.Visibility = accepted ? Visibility.Collapsed : Visibility.Visible;
        }
        finally { changingTransport = false; nextTransportRefresh = 0; }
    }
}
