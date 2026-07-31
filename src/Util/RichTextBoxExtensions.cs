namespace WindowsAssistant.Util;

internal static class RichTextBoxExtensions
{
    /// <summary>
    /// Removes excess lines from the beginning while preserving the requested number of newest lines.
    /// </summary>
    public static void RemoveFirstLines(this RichTextBox textBox, int maximumLineCount)
    {
        var lineCount = textBox.GetLineFromCharIndex(textBox.TextLength) + 1;
        if (lineCount > maximumLineCount)
        {
            textBox.Select(0, textBox.GetFirstCharIndexFromLine(lineCount - maximumLineCount));
            textBox.SelectedText = string.Empty;
        }
    }
}
