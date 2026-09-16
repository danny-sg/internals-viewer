using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.BatchMode.Vectors;
using InternalsViewer.Internals.Columnstore.Decoding;

namespace InternalsViewer.Execution.Iterators.BatchMode.DataAccess;

/// <summary>
/// A columnstore column bound to its segment reader for a rowgroup, with its batch column and any compressed data filter
/// </summary>
internal sealed record ScanColumn(SegmentReader Reader,
                                  BatchColumn Column,
                                  bool HasDictionary,
                                  CompressedDataFilter? Filter);
