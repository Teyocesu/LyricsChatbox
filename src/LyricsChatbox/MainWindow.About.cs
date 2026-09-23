using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace LyricsChatbox;

public partial class MainWindow
{
    private void InitializeAbout()
    {
        AboutVersionText.Text = ProductIdentity.DisplayVersion;
        UpdateCurrentVersion.Text = ProductIdentity.DisplayVersion;
        UpdateLastChecked.Text = "Not checked yet";
    }

    private void OpenAboutLink(string url, TextBlock status)
    {
        status.Text = "";
        ExternalLinks.TryOpen(url, message => status.Text = message);
    }

    private void OpenAuthorGitHub(object sender, RoutedEventArgs e) => OpenAboutLink(ProductIdentity.AuthorGitHubUrl, CommunityStatus);
    private void OpenVrChatProfile(object sender, RoutedEventArgs e) => OpenAboutLink(ProductIdentity.VrChatProfileUrl, CommunityStatus);
    private void ViewRepository(object sender, RoutedEventArgs e) => OpenAboutLink(ProductIdentity.Repository, ProjectStatus);
    private void ViewReleases(object sender, RoutedEventArgs e) => OpenAboutLink(ProductIdentity.ReleasesUrl, ProjectStatus);
    private void ReportIssue(object sender, RoutedEventArgs e) => OpenAboutLink(ProductIdentity.ReportIssueUrl, ProjectStatus);
    private void CopyDiscordName(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(ProductIdentity.DiscordUsername);
            CommunityStatus.Text = "Discord username copied.";
        }
        catch (ExternalException) { CommunityStatus.Text = "Could not copy the Discord username."; }
    }

    private void OpenThirdPartyNotices(object sender, RoutedEventArgs e)
    {
        if (!ExternalLinks.TryResolveExistingNotice(AppContext.BaseDirectory, out var path))
        {
            ProjectStatus.Text = "Third-party notices are included with packaged builds.";
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            ProjectStatus.Text = "";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            ProjectStatus.Text = "Could not open third-party notices.";
        }
    }
}
