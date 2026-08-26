using System.Collections.Generic;

namespace InternalsViewer.UI.App.Models.Query.Trace.Columnstore;

public sealed class ScanRowGroup
{
    public int RowGroupId { get; init; }

    public int TotalRows { get; init; }

    public IReadOnlyList<ScanSegment> Segments { get; init; } = [];

    public bool IsEliminated { get; set; }

    public bool IsVisited { get; set; }
}
