using System.Diagnostics;
using System.Windows;

namespace LyricsChatbox;

public partial class MainWindow
{
    private readonly CancellationTokenSource lifetime = new();
    private UpdateChecker? updates;
    private Uri? releaseLink;
    private bool updateBusy;
    private void InitializeUpdates()
    {
        updates = new(http);
        AutoUpdateBox.IsChecked = settings.AutomaticUpdateChecks;
        UpdateStatus.Text = "Version " + typeof(App).Assembly.GetName().Version!.ToString(3);
        if (settings.AutomaticUpdateChecks) Loaded += CheckUpdatesOnce;
    }
    private void CheckUpdatesOnce(object sender, RoutedEventArgs e)
    {
        Loaded -= CheckUpdatesOnce;
        if (settings.AutomaticUpdateChecks) CheckUpdates(sender, e);
    }
    private void AutomaticUpdatesChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        settings = settings with { AutomaticUpdateChecks = AutoUpdateBox.IsChecked == true };
        Save();
    }
    private async void CheckUpdates(object sender, RoutedEventArgs e)
    {
        if (closing || updateBusy || updates is null) return;
        updateBusy = true; CheckUpdatesButton.IsEnabled = false; UpdateStatus.Text = "Checking GitHub…";
        try
        {
            var request = updates.CheckAsync(typeof(App).Assembly.GetName().Version!, lifetime.Token);
            pending.Add(request);
            UpdateResult result;
            try { result = await request; }
            finally { pending.Remove(request); }
            if (closing) return;
            UpdateStatus.Text = result.Status; releaseLink = result.Release;
            ViewReleaseButton.Visibility = releaseLink is null ? Visibility.Collapsed : Visibility.Visible;
        }
        catch (OperationCanceledException) { }
        finally { updateBusy = false; if (!closing) CheckUpdatesButton.IsEnabled = true; }
    }
    private void ViewRelease(object sender, RoutedEventArgs e)
    {
        if (releaseLink is null) return;
        try { Process.Start(new ProcessStartInfo(releaseLink.AbsoluteUri) { UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception) { UpdateStatus.Text = "Could not open your browser."; }
    }
}
