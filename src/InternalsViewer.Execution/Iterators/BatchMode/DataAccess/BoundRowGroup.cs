using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Columnstore.Metadata;

namespace InternalsViewer.Execution.Iterators.BatchMode.DataAccess;

/// <summary>
/// A rowgroup the cursor has opened and bound, ready for the scan to read batches from
/// </summary>
internal sealed record BoundRowGroup(RowGroup RowGroup, RowGroupReader Reader, List<ScanColumn> Columns);