using System.Collections.Generic;
using InternalsViewer.Internals.Columnstore.Segments;

namespace InternalsViewer.UI.App.Models.Query.Trace.Columnstore;

public sealed class ScanSegment
{
    public int ColumnId { get; init; }

    public string ColumnName { get; init; } = string.Empty;

    public bool IsProjected { get; set; }

    public bool IsEliminated { get; set; }

    public bool IsOpened { get; set; }

    public IReadOnlyList<RleEntry> Runs { get; set; } = [];
}
