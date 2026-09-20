using System.Windows;
using System.Windows.Controls;

namespace LyricsChatbox;

public partial class MainWindow
{
    private DecorationCatalog decorationCatalog = null!;
    private DecorationState decorationState = null!;
    private DecorationLibrary decorationLibrary = null!;

    private void InitializeDecorations()
    {
        decorationCatalog = DecorationCatalog.LoadBuiltIn();
        decorationState = data.ReadDecorationState();
        decorationLibrary = DecorationLibrary.Create(decorationCatalog, decorationState);
    }

    private void OpenDecorationPicker(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string targetName }) return;
        var target = targetName switch
        {
            "Custom" => TemplateBox,
            "Status" => MessageBox,
            "Manual" => DraftBox,
            _ => null
        };
        if (target is null) return;

        var picker = new DecorationPicker(decorationCatalog, decorationState, decorationLibrary,
            SaveDecorationState, content => PreviewDecorationInsertion(target, content)) { Owner = this };
        try
        {
            if (picker.ShowDialog() == true && picker.SelectedContent is { } content)
                ApplyDecorationInsertion(target, content);
        }
        finally { target.Focus(); }
    }

    private bool SaveDecorationState(DecorationState next)
    {
        decorationState = next;
        decorationLibrary = DecorationLibrary.Create(decorationCatalog, decorationState);
        return data.SaveDecorationState(next);
    }

    private DecorationInsertionPreview PreviewDecorationInsertion(TextBox target, string content)
    {
        var insertion = TextInsertion.Insert(target.Text, target.SelectionStart, target.SelectionLength, target.MaxLength, content);
        if (!insertion.CanInsert) return TextInsertion.Preview(insertion, "", settings.Compact);

        string raw;
        if (target == DraftBox)
            raw = MessageLayout.Align(insertion.Text, settings.ManualAlignment);
        else
        {
            var template = target == TemplateBox ? insertion.Text : settings.CustomTemplate;
            var message = target == MessageBox ? insertion.Text : settings.Message;
            var now = MonotonicClock.Now;
            raw = LyricContextComposer.ComposeProfile(engine.Context(now), engine.Track, settings.Preset, template,
                message, contextMode, settings.Compact, DateTimeOffset.Now, engine.Position(now), settings.CustomAlignment);
            raw = MessageLayout.Align(raw, settings.CustomAlignment);
        }
        return TextInsertion.Preview(insertion, raw, settings.Compact);
    }

    private void ApplyDecorationInsertion(TextBox target, string content)
    {
        var result = TextInsertion.Insert(target.Text, target.SelectionStart, target.SelectionLength, target.MaxLength, content);
        if (!result.CanInsert) { SetError("Not enough editor space for this item."); return; }
        target.Text = result.Text;
        target.Select(result.CaretIndex, 0);
        ClearError();
    }
}
