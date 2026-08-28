using System.Data;
using InternalsViewer.Execution.BatchMode.Normalization;
using InternalsViewer.Execution.BatchMode.Vectors;
using InternalsViewer.Internals.Columnstore.Decoding;

namespace InternalsViewer.UI.App.Models.Query.Trace.Batch;

/// <summary>
/// One vector of the batch, shown as a column
/// </summary>
public sealed class BatchColumnView
{
    public required int Ordinal { get; set; }

    public required string Name { get; set; }

    public required BatchColumn Column { get; set; }

    public SegmentReader? Source { get; set; }

    public bool IsInScope { get; set; } = true;

    public SqlDbType DataType => Column.DataType;

    public BatchSlotDomain Domain => Column.Domain;
}
