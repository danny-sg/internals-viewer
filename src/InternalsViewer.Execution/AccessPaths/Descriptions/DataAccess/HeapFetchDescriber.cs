using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Descriptions.DataAccess;

public static class HeapFetchDescriber
{
    public static OperatorDescription Describe(AccessStrategy? strategy)
    {
        return new OperatorDescription
        {
            Summary = "Access path that reads one heap row from the file, page and slot a row identifier names, with no tree to descend",
            IsStreaming = true,
            Phases = strategy?.Phases ?? []
        };
    }
}
