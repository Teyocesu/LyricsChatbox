using System.Runtime.InteropServices;
using System.Windows;

namespace LyricsChatbox;

public partial class BugReportWindow : Window
{
    private readonly Func<string> diagnostics;
    private readonly Func<string, bool> copy;
    private readonly Func<string, Action<string>, bool> open;

    public bool SkipGuide => SkipGuideBox.IsChecked == true;

    public BugReportWindow(Func<string> diagnostics, bool skipGuide, Func<string, bool>? copy = null,
        Func<string, Action<string>, bool>? open = null)
    {
        this.diagnostics = diagnostics;
        this.copy = copy ?? CopyToClipboard;
        this.open = open ?? ExternalLinks.TryOpen;
        InitializeComponent();
        SkipGuideBox.IsChecked = skipGuide;
        Loaded += (_, _) => HappenedBox.Focus();
    }

    public string Report() => BugReport.Compose(HappenedBox.Text, ExpectedBox.Text, StepsBox.Text,
        ProductIdentity.DisplayVersion, Environment.OSVersion.VersionString,
        IncludeDiagnosticsBox.IsChecked == true ? diagnostics() : null);

    private static bool CopyToClipboard(string text)
    {
        try { Clipboard.SetText(text); return true; }
        catch (ExternalException) { return false; }
    }

    private void ReviewDiagnostics(object sender, RoutedEventArgs e)
    {
        var showing = DiagnosticsBox.Visibility == Visibility.Visible;
        if (!showing) DiagnosticsBox.Text = diagnostics();
        DiagnosticsBox.Visibility = showing ? Visibility.Collapsed : Visibility.Visible;
        ReviewDiagnosticsButton.Content = showing ? "Review diagnostics" : "Hide diagnostics";
        if (!showing) DiagnosticsBox.Focus();
    }

    private void CopyDiagnostics(object sender, RoutedEventArgs e) => ReportStatus.Text = copy(diagnostics())
        ? "Diagnostics copied. Review them before sharing." : "Clipboard is busy. Try again.";

    private void CopyReport(object sender, RoutedEventArgs e) => ReportStatus.Text = copy(Report())
        ? "Report copied. Paste it into a new GitHub issue." : "Clipboard is busy. Try again.";

    private void CopyAndOpen(object sender, RoutedEventArgs e)
    {
        if (!copy(Report())) { ReportStatus.Text = "Clipboard is busy. Try again before opening GitHub."; return; }
        ReportStatus.Text = open(ProductIdentity.ReportIssueUrl, message => ReportStatus.Text = message + " The report is copied.")
            ? "Report copied. Paste it into the GitHub issue with Ctrl+V." : ReportStatus.Text;
    }

    private void WindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Escape) return;
        Close(); e.Handled = true;
    }

    private void ClosePressed(object sender, RoutedEventArgs e) => Close();
}
