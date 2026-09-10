using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;

namespace LyricsChatbox;

public partial class MainWindow
{
    private readonly CancellationTokenSource lifetime = new();
    private UpdateChecker? updates;
    private Uri? releaseLink;
    private bool updateBusy;
    private readonly HttpClient installerHttp = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false });
    private CancellationTokenSource? downloadCancellation;
    private InstallerAsset? installerAsset;
    private VerifiedInstaller? verifiedInstaller;
    private string? availableTag;
    private bool downloading;
    private void InitializeUpdates()
    {
        updates = new(http);
        AutoUpdateBox.IsChecked = settings.AutomaticUpdateChecks;
        UpdateStatus.Text = "Version " + typeof(App).Assembly.GetName().Version!.ToString(3);
        if (settings.AutomaticUpdateChecks) Loaded += CheckUpdatesOnce;
    }
    private async void CheckUpdatesOnce(object sender, RoutedEventArgs e)
    {
        Loaded -= CheckUpdatesOnce;
        if (settings.AutomaticUpdateChecks) await RunUpdateCheck(true);
    }
    private void AutomaticUpdatesChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        settings = settings with { AutomaticUpdateChecks = AutoUpdateBox.IsChecked == true };
        Save();
    }
    private async void CheckUpdates(object sender, RoutedEventArgs e)
        => await RunUpdateCheck(false);
    private async Task RunUpdateCheck(bool automatic)
    {
        if (closing || updateBusy || downloading || updates is null) return;
        updateBusy = true; CheckUpdatesButton.IsEnabled = false; UpdateStatus.Text = "Checking GitHub…";
        try
        {
            var request = automatic ? updates.CheckAtStartupAsync(settings.AutomaticUpdateChecks, typeof(App).Assembly.GetName().Version!, lifetime.Token, settings.SkippedUpdateVersion)
                : updates.CheckAsync(typeof(App).Assembly.GetName().Version!, lifetime.Token);
            pending.Add(request);
            UpdateResult result;
            try { result = await request; }
            finally { pending.Remove(request); }
            if (closing) return;
            UpdateStatus.Text = result.Status; releaseLink = result.Release;
            availableTag = result.Tag; installerAsset = result.Installer; verifiedInstaller = null;
            UpdateNotes.Text = result.Notes;
            ReleaseDetails.Visibility = result.Release is null ? Visibility.Collapsed : Visibility.Visible;
            DownloadInstallerButton.IsEnabled = installerAsset is not null;
            DownloadStatus.Text = result.Release is not null && installerAsset is null ? "No verified installer asset is available. View the release on GitHub." : "";
            LaunchInstallerButton.Visibility = Visibility.Collapsed;
            ViewReleaseButton.Visibility = releaseLink is null ? Visibility.Collapsed : Visibility.Visible;
        }
        catch (OperationCanceledException) { }
        finally { updateBusy = false; if (!closing) CheckUpdatesButton.IsEnabled = true; }
    }
    private void SkipUpdate(object sender, RoutedEventArgs e)
    {
        if (availableTag is null || downloading) return;
        settings = settings with { SkippedUpdateVersion = availableTag }; Save();
        ReleaseDetails.Visibility = Visibility.Collapsed;
        UpdateStatus.Text = "Skipped " + availableTag + ". Check now will still show it.";
    }
    private async void DownloadInstaller(object sender, RoutedEventArgs e)
    {
        if (installerAsset is null || downloading || closing) return;
        downloading = true; DownloadInstallerButton.IsEnabled = CheckUpdatesButton.IsEnabled = SkipUpdateButton.IsEnabled = false;
        verifiedInstaller = null; LaunchInstallerButton.Visibility = Visibility.Collapsed;
        CancelDownloadButton.Visibility = DownloadProgress.Visibility = Visibility.Visible; DownloadProgress.Value = 0;
        DownloadStatus.Text = "Downloading checksum and installer from GitHub…";
        downloadCancellation?.Dispose(); downloadCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var token = downloadCancellation.Token;
        try
        {
            var progress = new Progress<double>(value => { if (!closing && !token.IsCancellationRequested) DownloadProgress.Value = value; });
            var task = new InstallerDownloader(installerHttp).DownloadAsync(installerAsset, Path.Combine(data.Root, "downloads"), progress, token);
            pending.Add(task); InstallerDownloadResult result;
            try { result = await task; }
            finally { pending.Remove(task); }
            if (closing || token.IsCancellationRequested) return;
            verifiedInstaller = result.Installer; DownloadStatus.Text = result.Status;
            LaunchInstallerButton.Visibility = result.Installer is null ? Visibility.Collapsed : Visibility.Visible;
        }
        catch (OperationCanceledException) { if (!closing) DownloadStatus.Text = "Download cancelled. Nothing was installed."; }
        finally
        {
            downloading = false;
            if (!closing)
            {
                DownloadInstallerButton.IsEnabled = installerAsset is not null; CheckUpdatesButton.IsEnabled = SkipUpdateButton.IsEnabled = true;
                CancelDownloadButton.Visibility = DownloadProgress.Visibility = Visibility.Collapsed;
            }
        }
    }
    private void CancelInstallerDownload(object sender, RoutedEventArgs e) => downloadCancellation?.Cancel();
    private async void LaunchInstaller(object sender, RoutedEventArgs e)
    {
        if (verifiedInstaller is not { } installer || closing) return;
        LaunchInstallerButton.IsEnabled = false;
        try
        {
            using var file = await installer.OpenVerifiedAsync(lifetime.Token);
            if (closing) return;
            if (file is null)
            {
                verifiedInstaller = null; LaunchInstallerButton.Visibility = Visibility.Collapsed;
                DownloadStatus.Text = "Installer changed or is missing. Download and verify it again."; return;
            }
            // Only this explicit click launches; no silent arguments, runas verb or application-file replacement.
            Process.Start(new ProcessStartInfo(installer.Path) { UseShellExecute = true });
            DownloadStatus.Text = "Installer opened. Follow its steps to update LyricsChatbox.";
        }
        catch (OperationCanceledException) { }
        catch (System.ComponentModel.Win32Exception) { DownloadStatus.Text = "Could not open the installer. Nothing was installed by LyricsChatbox."; }
        finally { if (!closing) LaunchInstallerButton.IsEnabled = true; }
    }
    private void ViewRelease(object sender, RoutedEventArgs e)
    {
        if (releaseLink is null) return;
        try { Process.Start(new ProcessStartInfo(releaseLink.AbsoluteUri) { UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception) { UpdateStatus.Text = "Could not open your browser."; }
    }
}
