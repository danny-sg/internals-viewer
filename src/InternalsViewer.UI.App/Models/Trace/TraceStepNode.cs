using Windows.UI;

namespace InternalsViewer.UI.App.Models.Trace;

public sealed record TraceStepNode(string Name, int Depth, Color Colour, int OuterInputNodeId = -1, int InnerInputNodeId = -1);
