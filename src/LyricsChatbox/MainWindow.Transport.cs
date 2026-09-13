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
    private readonly System.Windows.Threading.DispatcherTimer volumeSendTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private bool volumeTimerInitialized;
    private void RefreshTransport(double now)
    {
        if (now < nextTransportRefresh) return;
        nextTransportRefresh = now + 0.5;
        var state = playback.GetControls();
        PlayPauseButton.IsEnabled = !changingTransport && state.PlayPause;
        PreviousButton.IsEnabled = !changingTransport && state.Previous;
        NextButton.IsEnabled = !changingTransport && state.Next;
        ShuffleButton.IsEnabled = !changingTransport && state.Shuffle && state.Shuffling.HasValue;
        RepeatButton.IsEnabled = !changingTransport && state.Repeat && state.RepeatMode.HasValue;
        PlayPauseButton.Content = state.Playing ? "\uE769" : "\uE768";
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
            var volume = await TrackManualTask(Task.Run(AppleMusicVolume.Read));
            if (closing || generation != volumeGeneration || writingVolume) return;
            currentMusicVolume = volume;
            MusicVolumeSlider.IsEnabled = volume is not null;
            MusicVolumeSlider.ToolTip = volume is null ? "Apple Music audio session unavailable" : "Apple Music volume";
            if (volume is not null && !MusicVolumeSlider.IsMouseCaptureWithin && !MusicVolumeSlider.IsKeyboardFocusWithin)
            {
                updatingVolumeSlider = true;
                try { MusicVolumeSlider.Value = volume.Level; }
                finally { updatingVolumeSlider = false; }
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
            volumeSendTimer.Tick += (_, _) => { volumeSendTimer.Stop(); _ = SetMusicVolumeAsync(); };
        }
        volumeGeneration++;
        volumeSendTimer.Stop(); volumeSendTimer.Start();
    }
    private void VolumeReleased(object sender, MouseButtonEventArgs e) => _ = SetMusicVolumeAsync();
    private void VolumeKeyReleased(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Home or Key.End)
            _ = SetMusicVolumeAsync();
    }
    private async Task SetMusicVolumeAsync()
    {
        if (closing || writingVolume || currentMusicVolume is not { } expected) return;
        writingVolume = true;
        var generation = ++volumeGeneration;
        var value = (float)MusicVolumeSlider.Value;
        try
        {
            var accepted = await TrackManualTask(Task.Run(() => AppleMusicVolume.Set(expected, value)));
            if (closing) return;
            TransportStatus.Text = accepted ? "" : "Apple Music volume unavailable; try again.";
            TransportStatus.Visibility = accepted ? Visibility.Collapsed : Visibility.Visible;
        }
        finally
        {
            writingVolume = false;
            nextVolumeRefresh = 0;
            if (!closing && generation != volumeGeneration)
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
            var accepted = await playback.ControlAsync(command, playback.Revision);
            if (closing) return;
            TransportStatus.Text = accepted ? "" : "Apple Music did not accept the command.";
            TransportStatus.Visibility = accepted ? Visibility.Collapsed : Visibility.Visible;
        }
        finally { changingTransport = false; nextTransportRefresh = 0; }
    }
}
