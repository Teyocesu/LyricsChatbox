using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32;

namespace LyricsChatbox;

public partial class MainWindow
{
    private readonly Diagnostics diagnostics = new();
    private string DiagnosticText() => diagnostics.Export(engine.Snapshot, settings, engine.LyricsStatus, output.Status, engine.Offset);
    private void CopyDiagnostics(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(DiagnosticText()); DiagnosticsStatus.Text = "Copied. Review before sharing."; }
        catch (ExternalException) { DiagnosticsStatus.Text = "Clipboard is busy. Try exporting instead."; }
    }
    private async void ExportDiagnostics(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Diagnostic JSON|*.json", FileName = "LyricsChatbox-diagnostics.json" };
        if (dialog.ShowDialog(this) != true) return;
        try { await File.WriteAllTextAsync(dialog.FileName, DiagnosticText()); DiagnosticsStatus.Text = "Exported. Review before sharing."; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { DiagnosticsStatus.Text = "Could not save diagnostics."; }
    }
}
