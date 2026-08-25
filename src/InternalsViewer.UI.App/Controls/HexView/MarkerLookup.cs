using System.Collections.Generic;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.Controls.HexView;

/// <summary>
/// Finding the marker a byte belongs to, markers sitting inside one another
/// </summary>
public static class MarkerLookup
{
    /// <summary>
    /// The narrowest marker covering an offset, a field being wanted ahead of the section it sits in
    /// </summary>
    public static Marker? FindAt(IEnumerable<Marker>? markers, int offset)
    {
        Marker? found = null;

        if (markers is null)
        {
            return null;
        }

        foreach (var marker in markers)
        {
            if (marker.StartPosition > offset || marker.EndPosition < offset)
            {
                continue;
            }

            var candidate = FindAt(marker.Children, offset) ?? marker;

            if (found is null || candidate.Length < found.Length)
            {
                found = candidate;
            }
        }

        return found;
    }

    public static (int Start, int End)? GetRange(Marker? marker)
        => marker is null ? null : (marker.StartPosition, marker.EndPosition);
}
