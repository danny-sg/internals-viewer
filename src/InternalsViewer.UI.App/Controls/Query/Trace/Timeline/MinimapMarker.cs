using Windows.UI;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline;

public readonly record struct MinimapMarker(double Position, Color Colour, float Width = 2f);
