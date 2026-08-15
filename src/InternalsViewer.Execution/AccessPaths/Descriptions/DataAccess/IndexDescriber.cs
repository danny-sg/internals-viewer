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
                ? "Access path that descends the index to the first key in range, then walks the leaf level in key order reading only " +
                  "the rows the range covers"
                : "Access path that reads the whole leaf level of an index in key order, with no bounds to stop it early",
            IsStreaming = true,
            Phases = strategy?.Phases ?? []
        };
    }
}
