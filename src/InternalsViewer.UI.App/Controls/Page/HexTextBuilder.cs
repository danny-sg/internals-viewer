using InternalsViewer.Internals.Helpers;
using System;
using System.Collections.Generic;
using System.Text;

namespace InternalsViewer.UI.App.Controls.Page;

/// <summary>
/// A stretch of hex dump text sharing the same formatting
/// </summary>
internal readonly record struct HexRun(string Text, bool IsSelected);

/// <summary>
/// Builds the hex dump text, split into runs either side of the selected marker
/// </summary>
internal static class HexTextBuilder
{
    private static readonly string[] HexByValue = CreateHexTable();

    public static List<HexRun> Build(IReadOnlyList<byte> data, int bytesPerLine, int? selectionStart, int? selectionEnd)
    {
        var runs = new List<HexRun>();

        var stringBuilder = new StringBuilder();

        var position = 0;

        for (var line = 0; line < data.Count / bytesPerLine; line++)
        {
            for (var byteIndex = 0; byteIndex < bytesPerLine; byteIndex++)
            {
                if (position == selectionStart)
                {
                    runs.Add(Flush(stringBuilder, false));
                }

                stringBuilder.Append(HexByValue[data[position]]);

                if (position == selectionEnd)
                {
                    runs.Add(Flush(stringBuilder, true));
                }

                // Add a space between bytes, but not for the last byte of the line
                if (byteIndex != bytesPerLine - 1)
                {
                    stringBuilder.Append(' ');
                }

                position++;
            }

            stringBuilder.Append(Environment.NewLine);
        }

        runs.Add(Flush(stringBuilder, false));

        return runs;
    }

    private static HexRun Flush(StringBuilder stringBuilder, bool isSelected)
    {
        var run = new HexRun(stringBuilder.ToString(), isSelected);

        stringBuilder.Clear();

        return run;
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
