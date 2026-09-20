namespace LyricsChatbox;

public readonly record struct TextInsertionResult(bool CanInsert, string Text, int CaretIndex);
public readonly record struct DecorationInsertionPreview(bool CanInsert, bool WouldTruncate, int VisibleUnits, int Limit);

public static class TextInsertion
{
    public static TextInsertionResult Insert(string text, int selectionStart, int selectionLength, int maximumLength, string content)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(content);
        if (selectionStart < 0 || selectionLength < 0 || selectionStart + selectionLength > text.Length)
            throw new ArgumentOutOfRangeException(nameof(selectionStart));
        var length = text.Length - selectionLength + content.Length;
        if (maximumLength > 0 && length > maximumLength) return new(false, text, selectionStart);
        return new(true, text[..selectionStart] + content + text[(selectionStart + selectionLength)..], selectionStart + content.Length);
    }

    public static DecorationInsertionPreview Preview(TextInsertionResult insertion, string rawOutput, bool compact, bool preserveLayout = true)
    {
        var analysis = ChatboxFormatter.Analyze(rawOutput, compact, preserveLayout);
        return new(insertion.CanInsert, insertion.CanInsert && analysis.WouldTruncate,
            insertion.CanInsert ? analysis.VisibleUnits : 0, analysis.Limit);
    }
}
