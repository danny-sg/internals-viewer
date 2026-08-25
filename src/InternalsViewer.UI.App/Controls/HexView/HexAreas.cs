using System.Collections.Generic;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.Controls.HexView;

/// <summary>
/// Which named area of the data a line falls in, the areas running back to back in order
/// </summary>
public static class HexAreas
{
    public static string NameAt(IReadOnlyList<HexArea> areas, int offset)
    {
        var name = string.Empty;

        foreach (var area in areas)
        {
            if (area.Start > offset)
            {
                break;
            }

            name = area.Name;
        }

        return name;
    }

    /// <summary>
    /// The names to write over a window of lines, one where its area starts rather than one on every line
    /// </summary>
    /// <remarks>
    /// Written this way the column reads as a map of where the window has reached rather than a wall of repeated
    /// text. An area starting before the window still names its first line, because that line is where the reader
    /// meets it.
    /// </remarks>
    public static IEnumerable<HexAreaLabel> GetLabels(IReadOnlyList<HexArea> areas, int baseAddress, int lineCount)
    {
        var previous = string.Empty;

        for (var line = 0; line < lineCount; line++)
        {
            var name = NameAt(areas, baseAddress + (line * HexLayout.BytesPerLine));

            if (name.Length > 0 && name != previous)
            {
                yield return new HexAreaLabel(name, line);
            }

            previous = name;
        }
    }
}

/// <summary>
/// One area name and the line it is written against
/// </summary>
public readonly record struct HexAreaLabel(string Name, int Line);
