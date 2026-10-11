using Windows.UI;

namespace InternalsViewer.UI.App.Models.Query.Simulation;

public sealed record SimulationStepNode(string Name,
                                   int Depth,
                                   Color Colour,
                                   int OuterInputNodeId = -1,
                                   int InnerInputNodeId = -1);
