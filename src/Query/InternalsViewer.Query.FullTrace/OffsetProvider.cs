using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;

namespace InternalsViewer.Query.FullTrace;

internal sealed class OffsetProvider(Version? serverVersion)
{
    private const int SqlServer2025 = 17;

    public PlanNodeLayout PlanNode { get; } = serverVersion?.Major < SqlServer2025
        ? PlanNodeLayout.SqlServer2022
        : PlanNodeLayout.SqlServer2025;
}
