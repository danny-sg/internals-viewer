using System;

namespace InternalsViewer.UI.App.Controls.HexView;

/// <summary>
/// Where a byte sits in the hex dump, which is a fixed number of bytes to a line
/// </summary>
public static class HexLayout
{
    // 16 bytes per line is the conventional way of displaying hex
    public const int BytesPerLine = 16;

    // Bytes are represented by 2 characters and a space
    public const int CharactersPerByte = 2;

    /// <summary>
    /// Characters a line of the text takes, being its bytes, the spaces between them and the line break
    /// </summary>
    public static decimal CharactersPerLine => (BytesPerLine * CharactersPerByte)
                                               + BytesPerLine - 1
                                               + Environment.NewLine.Length;

    /// <summary>
    /// Lines the text holds, a short last line among them
    /// </summary>
    public static int GetLineCount(int length) => Math.Max(1, (length + BytesPerLine - 1) / BytesPerLine);

    /// <summary>
    /// The rendered line pitch, which drifts from the nominal line height by enough to matter down a long page
    /// </summary>
    /// <remarks>
    /// Taken over the lines the text holds. Counting a line either way tilts every position below the first by a
    /// fraction of the error, which reads as a drawing sitting a row out by the bottom rather than as a pitch
    /// being wrong.
    /// </remarks>
    public static double GetLineHeight(double renderedHeight, int length, double nominalHeight)
        => renderedHeight > 0 ? renderedHeight / GetLineCount(length) : nominalHeight;

    /// <summary>
    /// Converts a byte position to a position in the hex text
    /// </summary>
    public static int ToRunPosition(int position)
    {
        // Bytes are represented by 2 characters and a space
        const int charactersPerByte = 3;

        var lineNumber = position / BytesPerLine;

        return position * charactersPerByte + lineNumber * (Environment.NewLine.Length - 1);
    }

    /// <summary>
    /// Converts a position in the hex text to a byte position
    /// </summary>
    public static int FromRunPosition(int position, decimal charactersPerLine)
    {
        var lineNumber = (int)Math.Floor(position / charactersPerLine);

        var linePosition = position % charactersPerLine;

        var bytePosition = Math.Round(linePosition / (CharactersPerByte + 1));

        return lineNumber * BytesPerLine + (int)bytePosition;
    }
}
