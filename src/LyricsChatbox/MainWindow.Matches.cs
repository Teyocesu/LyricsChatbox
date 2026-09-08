using System.Windows;

namespace LyricsChatbox;

public partial class MainWindow
{
    private CancellationTokenSource? matchCancellation;
    private async Task<T> TrackManualTask<T>(Task<T> task)
    {
        pending.Add(task);
        try { return await task; }
        finally { pending.Remove(task); }
    }
    private void CancelManualMatch()
    {
        matchCancellation?.Cancel(); matchCancellation?.Dispose(); matchCancellation = null;
        MatchPanel.Visibility = Visibility.Collapsed; MatchChoices.ItemsSource = null;
        SearchMatchesButton.IsEnabled = UseMatchButton.IsEnabled = true;
    }
    private void OpenMatches(object sender, RoutedEventArgs e)
    {
        if (engine.Track is not { } track) return;
        MatchPanel.Visibility = Visibility.Visible; MatchQuery.Text = track.Title + " " + track.Artist;
        MatchStatus.Text = "Compare the recording, artist and duration. Your choice applies only to this song.";
    }
    private void CloseMatches(object sender, RoutedEventArgs e) => CancelManualMatch();
    private async void SearchMatches(object sender, RoutedEventArgs e)
    {
        if (engine.Track is not { } track || closing) return;
        matchCancellation?.Cancel(); matchCancellation?.Dispose();
        matchCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var token = matchCancellation.Token; var epoch = engine.Epoch; var query = MatchQuery.Text;
        SearchMatchesButton.IsEnabled = false; MatchChoices.ItemsSource = null; MatchStatus.Text = "Searching candidates…";
        try
        {
            var choices = await TrackManualTask(Task.Run(() => resolver.SearchManualAsync(track, query, token), token));
            if (closing || token.IsCancellationRequested || engine.Epoch != epoch) return;
            MatchChoices.ItemsSource = choices;
            MatchStatus.Text = choices.Count == 0 ? "No candidates available. Try different search words or import a local LRC." : "Choose the recording you want to use. NetEase timing is verified after selection.";
        }
        catch (OperationCanceledException) { }
        finally { if (!closing && !token.IsCancellationRequested) SearchMatchesButton.IsEnabled = true; }
    }
    private async void UseMatch(object sender, RoutedEventArgs e)
    {
        if (engine.Track is not { } track || MatchChoices.SelectedItem is not ManualCandidate choice || closing) return;
        matchCancellation?.Cancel(); matchCancellation?.Dispose();
        matchCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var token = matchCancellation.Token; var epoch = engine.Epoch;
        lookup?.Cancel(); UseMatchButton.IsEnabled = false; SearchMatchesButton.IsEnabled = false;
        MatchStatus.Text = "Checking synchronized lyrics…";
        try
        {
            var result = await TrackManualTask(Task.Run(() => resolver.FetchManualAsync(choice, token), token));
            if (closing || token.IsCancellationRequested || engine.Epoch != epoch) return;
            if (result.Outcome != LyricsOutcome.Found || result.Record is not { } record)
            { MatchStatus.Text = "That candidate has no available synchronized lyrics. Choose another."; return; }
            if (!data.SaveManualAssociation(track, choice, record)) { MatchStatus.Text = "Could not save the manual match."; return; }
            loaded.Remove(track.Key);
            engine.Complete(epoch, new(LrcParser.Parse(record.SyncedLyrics), "Synced lyrics loaded · manual match · " + choice.Provider, Provider: choice.Provider, Outcome: LyricsOutcome.Found));
            // Local imports remain authoritative, even after explicitly selecting a remote association.
            StartLookup(true); CancelManualMatch(); ForgetMatchButton.IsEnabled = true; Tick();
        }
        catch (OperationCanceledException) { }
        finally { if (!closing && !token.IsCancellationRequested) UseMatchButton.IsEnabled = SearchMatchesButton.IsEnabled = true; }
    }
    private void ForgetMatch(object sender, RoutedEventArgs e)
    {
        if (engine.Track is not { } track) return;
        CancelManualMatch();
        if (!data.ForgetManualAssociation(track)) { ErrorText.Text = "Could not forget the manual match."; return; }
        ForgetMatchButton.IsEnabled = false; loaded.Remove(track.Key); StartLookup(true);
    }
}
