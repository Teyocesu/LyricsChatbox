using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace LyricsChatbox;

public partial class MainWindow
{
    private void InitializeAbout()
    {
        AboutVersionText.Text = "v" + ProductIdentity.DisplayVersion;
    }

    private void OpenAboutLink(string url)
    {
        AboutStatus.Text = "";
        ExternalLinks.TryOpen(url, message => AboutStatus.Text = message);
    }

    private void OpenAuthorGitHub(object sender, RoutedEventArgs e) => OpenAboutLink(ProductIdentity.AuthorGitHubUrl);
    private void OpenVrChatProfile(object sender, RoutedEventArgs e) => OpenAboutLink(ProductIdentity.VrChatProfileUrl);
    private void ViewRepository(object sender, RoutedEventArgs e) => OpenAboutLink(ProductIdentity.Repository);
    private void ViewReleases(object sender, RoutedEventArgs e) => OpenAboutLink(ProductIdentity.ReleasesUrl);
    private void ReportIssue(object sender, RoutedEventArgs e) => OpenAboutLink(ProductIdentity.ReportIssueUrl);
    private void ViewNoticesOnGitHub(object sender, RoutedEventArgs e) => OpenAboutLink(ProductIdentity.ThirdPartyNoticesUrl);

    private void CopyDiscordName(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(ProductIdentity.DiscordUsername);
            AboutStatus.Text = "Discord username copied.";
        }
        catch (ExternalException) { AboutStatus.Text = "Could not copy the Discord username."; }
    }

    private void OpenThirdPartyNotices(object sender, RoutedEventArgs e)
    {
        if (!ExternalLinks.TryResolveExistingNotice(AppContext.BaseDirectory, out var path))
        {
            AboutStatus.Text = "Third-party notices are included with packaged builds.";
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            AboutStatus.Text = "";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            AboutStatus.Text = "Could not open third-party notices.";
        }
    }
}
