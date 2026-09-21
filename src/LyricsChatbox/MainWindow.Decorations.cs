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
            "Rotation" => RotationMessageBox,
            "Manual" => DraftBox,
            _ => null
        };
        if (target is null) return;
        var picker = new DecorationPicker(decorationCatalog, decorationState, decorationLibrary,
            SaveDecorationState, content => PreviewDecorationInsertion(target, content),
            target == RotationMessageBox ? DecorationPreviewWording.ActiveMessage : DecorationPreviewWording.CurrentOutput)
            { Owner = this };
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

        var monotonicNow = MonotonicClock.Now;
        var wallNow = DateTimeOffset.Now;
        string Compose(string text)
        {
            if (target == DraftBox) return MessageLayout.Align(text, settings.ManualAlignment);
            var template = target == TemplateBox ? text : settings.CustomTemplate;
            var message = target == RotationMessageBox ? text : activeRotationMessage.Text ?? profiles.Selected.Message;
            var raw = LyricContextComposer.ComposeProfile(engine.Context(monotonicNow), engine.Track, settings.Preset, template,
                message, contextMode, settings.Compact, wallNow, engine.Position(monotonicNow), settings.CustomAlignment);
            return MessageLayout.Align(raw, settings.CustomAlignment);
        }
        return TextInsertion.Preview(insertion, Compose(insertion.Text), settings.Compact, currentRawOutput: Compose(target.Text));
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
