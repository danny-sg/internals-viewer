using System.Collections.Generic;
using InternalsViewer.UI.App.Models.Query.Trace;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed class TraceLayout
{
    public required IReadOnlyList<TraceOperatorViewModel> Tabs { get; init; }

    public required IReadOnlyDictionary<int, TraceNodeContext> Nodes { get; init; }

    public required TraceBlobPalette Palette { get; init; }
}
