using System.Collections.Generic;
using InternalsViewer.Internals.Annotations;
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

    /// <summary>
    /// The first marker of a type, which is how a selection is found again after the markers are rebuilt
    /// </summary>
    /// <remarks>
    /// A rebuild replaces every marker, so a caller holding one from before has nothing the tree or the hex view
    /// still knows about. The type is what survives, each selection marking its own kind of thing.
    /// </remarks>
    public static Marker? FindByType(IEnumerable<Marker>? markers, ItemType type)
    {
        if (markers is null)
        {
            return null;
        }

        foreach (var marker in markers)
        {
            if (marker.Type == type)
            {
                return marker;
            }

            if (FindByType(marker.Children, type) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    public static (int Start, int End)? GetRange(Marker? marker)
        => marker is null ? null : (marker.StartPosition, marker.EndPosition);
}
