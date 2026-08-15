using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Descriptions.DataAccess;

public static class AllocationScanDescriber
{
    public static OperatorDescription Describe(AccessStrategy? strategy)
    {
        return new OperatorDescription
        {
            Summary = "Access path that follows the allocation unit's IAM chain and reads its pages in allocation order, which is the " +
                      "order they sit in the file rather than any key order",
            IsStreaming = true,
            Phases = strategy?.Phases ?? []
        };
    }
}
