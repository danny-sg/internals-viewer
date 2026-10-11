using System.Collections.Generic;

namespace InternalsViewer.UI.App.Models.Query.Simulation;

public sealed record OperatorSides(int OuterNodeId, int InnerNodeId, HashSet<string> OuterTables, HashSet<string> InnerTables);
