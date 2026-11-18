using Microsoft.UI;
using Windows.UI;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed record MemoryStructureBadge(string Text, Color Colour)
{
    public Color TextColour { get; init; } = Colors.Black;
}
