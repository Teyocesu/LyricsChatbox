using System.IO;
using System.Windows;
using Microsoft.Win32;
using Windows.Storage.Streams;

namespace LyricsChatbox;

public partial class MainWindow
{
    private readonly ArtworkState artworkState = new();
    private CancellationTokenSource? artworkCancellation;
    private void ClearArtwork()
    {
        ClearArtworkTint();
        artworkCancellation?.Cancel(); artworkCancellation?.Dispose(); artworkCancellation = null;
        artworkState.Reset(); ArtImage.Source = null; ArtFallback.Visibility = Visibility.Visible;
    }
    private void OnArtwork(TrackIdentity track, long revision, IRandomAccessStreamReference? reference)
    {
        _ = Dispatcher.InvokeAsync(() =>
        {
            if (closing || revision != playback.Revision || track.Key != engine.Track?.Key) return;
            ClearArtwork();
            if (reference is null) return;
            artworkCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            var generation = artworkState.Reset();
            var task = LoadArtworkAsync(reference, generation, revision, artworkCancellation.Token);
            pending.Add(task);
            _ = task.ContinueWith(_ => Dispatcher.InvokeAsync(() => pending.Remove(task)), TaskScheduler.Default);
        });
    }
    private async Task LoadArtworkAsync(IRandomAccessStreamReference reference, long generation, long revision, CancellationToken token)
    {
        try
        {
            var image = await Task.Run(() => Artwork.LoadAsync(async ct =>
            {
                var stream = await reference.OpenReadAsync().AsTask(ct);
                return stream.AsStreamForRead(); // Disposing this adapter owns the WinRT stream.
            }, token), token);
            if (closing || token.IsCancellationRequested || revision != playback.Revision || !artworkState.Complete(generation, image)) return;
            ArtImage.Source = image; ArtFallback.Visibility = image is null ? Visibility.Visible : Visibility.Collapsed;
            RefreshArtworkTint();
        }
        catch (OperationCanceledException) { }
    }
    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode is not (PowerModes.Suspend or PowerModes.Resume)) return;
        _ = Dispatcher.InvokeAsync(() =>
        {
            if (closing) return;
            lookup?.Cancel(); engine.Observe(null); ClearArtwork(); CancelManualMatch(); LoadCorrection();
            scheduler.ReceiverChanged(); typing.Reset(); manual.Resume(); output.SendTyping(false);
            playback.ReanchorAfterResume();
            diagnostics.Add(DiagnosticCategory.Lifecycle, e.Mode == PowerModes.Resume ? "Resuming; awaiting fresh playback" : "Suspending playback state");
            Tick();
        });
    }
}
