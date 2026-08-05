using System.Collections.Generic;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Models.Trace;

public sealed class TraceBlobPalette
{
    private readonly Dictionary<int, (SolidColorBrush Brush, Windows.UI.Color Colour)> _brushes = [];

    private HashSet<int>? _active;

    public SolidColorBrush For(int nodeId, Windows.UI.Color colour)
    {
        if (!_brushes.TryGetValue(nodeId, out var entry))
        {
            entry = (new SolidColorBrush(ColourFor(nodeId, colour)), colour);

            _brushes[nodeId] = entry;
        }

        return entry.Brush;
    }

    public void SetActive(int? nodeId) => SetActiveSet(nodeId is { } id ? [id] : null);

    public void SetActiveSet(IReadOnlyCollection<int>? nodeIds)
    {
        if (nodeIds is null && _active is null)
        {
            return;
        }

        if (nodeIds is not null && _active is not null && _active.Count == nodeIds.Count && _active.IsSupersetOf(nodeIds))
        {
            return;
        }

        _active = nodeIds is null ? null : [.. nodeIds];

        foreach (var (brushNodeId, entry) in _brushes)
        {
            entry.Brush.Color = ColourFor(brushNodeId, entry.Colour);
        }
    }

    private Windows.UI.Color ColourFor(int nodeId, Windows.UI.Color colour)
        => _active is null || _active.Contains(nodeId)
            ? colour
            : Windows.UI.Color.FromArgb((byte)(colour.A * 0.3), colour.R, colour.G, colour.B);
}
