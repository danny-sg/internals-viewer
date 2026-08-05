using System.Collections.Generic;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Models.Trace;

public sealed class TraceBlobPalette
{
    private readonly Dictionary<int, (SolidColorBrush Brush, Windows.UI.Color Colour)> _brushes = [];

    private int? _activeNodeId;

    public SolidColorBrush For(int nodeId, Windows.UI.Color colour)
    {
        if (!_brushes.TryGetValue(nodeId, out var entry))
        {
            entry = (new SolidColorBrush(ColourFor(nodeId, colour)), colour);

            _brushes[nodeId] = entry;
        }

        return entry.Brush;
    }

    public void SetActive(int? nodeId)
    {
        if (_activeNodeId == nodeId)
        {
            return;
        }

        _activeNodeId = nodeId;

        foreach (var (brushNodeId, entry) in _brushes)
        {
            entry.Brush.Color = ColourFor(brushNodeId, entry.Colour);
        }
    }

    private Windows.UI.Color ColourFor(int nodeId, Windows.UI.Color colour)
        => _activeNodeId is null || _activeNodeId == nodeId
            ? colour
            : Windows.UI.Color.FromArgb((byte)(colour.A * 0.3), colour.R, colour.G, colour.B);
}
