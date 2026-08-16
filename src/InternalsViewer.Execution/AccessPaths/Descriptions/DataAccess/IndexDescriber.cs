using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Descriptions.DataAccess;

/// <summary>
/// Describes an index read in key order, which is a seek where its ranges bound the walk and a scan where they do not
/// </summary>
public static class IndexDescriber
{
    public static OperatorDescription Describe(RangeDefinition definition, AccessStrategy? strategy)
    {
        var isSeek = definition.Ranges.Any(r => r.HasStart || r.HasEnd);

        return new OperatorDescription
        {
            Summary = isSeek
                ? "Data access that descends from the index root to the first key in range, then walks the leaf level in key order " +
                  "reading only the rows the range covers."
                : "Data access that reads the whole leaf level of an index in key order. The read is unbound, so it runs to the end of " +
                  "the leaf level unless a row goal above stops it early.",
            IsStreaming = true,
            Phases = strategy?.Phases ?? []
        };
    }
}
