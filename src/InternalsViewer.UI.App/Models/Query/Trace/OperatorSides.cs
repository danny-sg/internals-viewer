using System.Collections.Generic;

namespace InternalsViewer.UI.App.Models.Query.Trace;

public sealed record OperatorSides(int OuterNodeId, int InnerNodeId, HashSet<string> OuterTables, HashSet<string> InnerTables);
