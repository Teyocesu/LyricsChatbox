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
        ApplyReportGuideView();
    }

    private void ApplyReportGuideView()
    {
        ReportGuideButton.Visibility = settings.SkipReportGuide ? Visibility.Visible : Visibility.Collapsed;
        System.Windows.Automation.AutomationProperties.SetName(ReportIssueButton,
            settings.SkipReportGuide ? "Report an issue on GitHub" : "Open the issue report guide");
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
    private void ReportIssue(object sender, RoutedEventArgs e)
    {
        if (settings.SkipReportGuide) OpenAboutLink(ProductIdentity.ReportIssueUrl, ProjectStatus);
        else OpenReportGuide(sender, e);
    }

    private void OpenReportGuide(object sender, RoutedEventArgs e)
    {
        ProjectStatus.Text = "";
        var guide = new BugReportWindow(DiagnosticText, settings.SkipReportGuide) { Owner = this };
        try { guide.ShowDialog(); }
        finally
        {
            if (guide.SkipGuide != settings.SkipReportGuide)
            {
                settings = settings with { SkipReportGuide = guide.SkipGuide };
                Save();
                ApplyReportGuideView();
            }
            (sender is UIElement { IsVisible: true } origin ? origin : ReportIssueButton).Focus();
        }
    }
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
            ProjectStatus.Text = "Third-party notices are not available beside this build.";
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
