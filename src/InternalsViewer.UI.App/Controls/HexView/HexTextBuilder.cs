using InternalsViewer.Internals.Helpers;
using System;
using System.Collections.Generic;
using System.Text;

namespace InternalsViewer.UI.App.Controls.HexView;

/// <summary>
/// Builds the hex dump text
/// </summary>
internal static class HexTextBuilder
{
    private static readonly string[] HexByValue = CreateHexTable();

    public static string Build(IReadOnlyList<byte> data, int bytesPerLine)
    {
        var stringBuilder = new StringBuilder();

        var position = 0;

        // A page divides into whole lines but a blob is any length, so the last line is rounded up rather than lost
        var lineCount = (data.Count + bytesPerLine - 1) / bytesPerLine;

        for (var line = 0; line < lineCount; line++)
        {
            for (var byteIndex = 0; byteIndex < bytesPerLine && position < data.Count; byteIndex++)
            {
                stringBuilder.Append(HexByValue[data[position]]);

                position++;

                // A space separates bytes, so the last of a line has none, nor does the last of a short final line
                if (byteIndex != bytesPerLine - 1 && position < data.Count)
                {
                    stringBuilder.Append(' ');
                }
            }

            // The newline separates lines rather than ending them, so the block does not run on past its last byte
            if (line < lineCount - 1)
            {
                stringBuilder.Append(Environment.NewLine);
            }
        }

        return stringBuilder.ToString();
    }

    private static string[] CreateHexTable()
    {
        var table = new string[256];

        for (var i = 0; i < table.Length; i++)
        {
            table[i] = StringHelpers.ToHexString((byte)i);
        }

        return table;
    }
}
