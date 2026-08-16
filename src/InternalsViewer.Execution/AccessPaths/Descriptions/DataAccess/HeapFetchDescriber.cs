using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Descriptions.DataAccess;

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
