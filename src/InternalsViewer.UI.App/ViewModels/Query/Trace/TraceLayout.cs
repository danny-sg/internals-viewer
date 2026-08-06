using System.Collections.Generic;
using System.Drawing;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.UI.App.Models.Query.Trace;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed class TraceLayout
{
    public required IReadOnlyList<TraceOperatorViewModel> Tabs { get; init; }

    public required IReadOnlyDictionary<int, TraceRowStreamViewModel> Streams { get; init; }

    public required IReadOnlyDictionary<(int NodeId, int InputIndex), TraceHeldRowsViewModel> HeldRows { get; init; }

    public required IReadOnlyDictionary<int, TraceHashTableViewModel> HashTables { get; init; }

    public required IReadOnlyDictionary<int, IteratorDefinition> Definitions { get; init; }

    public required IReadOnlyDictionary<int, OperatorSides> Sides { get; init; }

    public required IReadOnlyDictionary<int, TraceVisualViewModel> VisualByOperator { get; init; }

    public required IReadOnlyDictionary<int, int> Depths { get; init; }

    public required IReadOnlyDictionary<int, Color> Colours { get; init; }

    public required IReadOnlyDictionary<int, (int Outer, int Inner)> InputNodes { get; init; }

    public required TraceBlobPalette Palette { get; init; }
}
