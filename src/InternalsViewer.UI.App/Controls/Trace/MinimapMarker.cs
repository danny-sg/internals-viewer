using Windows.UI;

namespace InternalsViewer.UI.App.Controls.Trace;

public readonly record struct MinimapMarker(double Position, Color Colour, int Lane = -1, int Depth = -1);
