using System.Text;

namespace LyricsChatbox;

// Builds the copyable issue text. It never reads diagnostics itself: the caller passes them only after the user opts in.
public static class BugReport
{
    public const int MaximumFieldLength = 4000;
    private const string Missing = "_Not provided_";

    public static string Clean(string? text)
    {
        var normalized = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
        var clean = new string(normalized.Where(c => !char.IsControl(c) || c == '\n').ToArray()).Trim();
        return clean.Length <= MaximumFieldLength ? clean : clean[..MaximumFieldLength];
    }

    public static string Compose(string? happened, string? expected, string? steps, string version, string windows,
        string? diagnostics = null)
    {
        static string Section(string title, string? body)
        {
            var clean = Clean(body);
            return "## " + title + "\n\n" + (clean.Length == 0 ? Missing : clean) + "\n\n";
        }
        var report = new StringBuilder()
            .Append(Section("What happened", happened))
            .Append(Section("What I expected", expected))
            .Append(Section("Steps to reproduce", steps))
            .Append("## Environment\n\n- LyricsChatbox ").Append(Clean(version)).Append("\n- ").Append(Clean(windows)).Append("\n\n")
            .Append("## Screenshot\n\nPaste or drag a screenshot into this issue if it helps.\n");
        if (!string.IsNullOrWhiteSpace(diagnostics))
            report.Append("\n## Diagnostics\n\n<details><summary>Sanitized diagnostics</summary>\n\n````json\n")
                .Append(diagnostics.Replace("\r\n", "\n").Trim()).Append("\n````\n\n</details>\n");
        return report.ToString();
    }
}
