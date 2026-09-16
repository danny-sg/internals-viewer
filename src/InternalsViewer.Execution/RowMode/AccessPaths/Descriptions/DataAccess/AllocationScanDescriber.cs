using InternalsViewer.Execution.Common.AccessPaths.Search;
using InternalsViewer.Execution.Common.AccessPaths;

namespace InternalsViewer.Execution.RowMode.AccessPaths.Descriptions.DataAccess;

public static class AllocationScanDescriber
{
    public static OperatorDescription Describe(AccessStrategy? strategy)
    {
        return new OperatorDescription
        {
            Summary = "Data access that follows the allocation unit IAM chain and reads pages then record slots in allocation order.",
            IsStreaming = true,
            Phases = strategy?.Phases ?? []
        };
    }
}
