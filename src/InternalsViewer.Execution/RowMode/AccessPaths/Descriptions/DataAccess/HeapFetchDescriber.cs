using InternalsViewer.Execution.Common.AccessPaths.Search;
using InternalsViewer.Execution.Common.AccessPaths;

namespace InternalsViewer.Execution.RowMode.AccessPaths.Descriptions.DataAccess;

public static class HeapFetchDescriber
{
    public static OperatorDescription Describe(AccessStrategy? strategy)
    {
        return new OperatorDescription
        {
            Summary = "Data access that reads a row via page address and slot.",
            IsStreaming = true,
            Phases = strategy?.Phases ?? []
        };
    }
}
