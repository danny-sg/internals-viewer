using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Execution.AccessPaths.Results.Steps;

public abstract partial record AccessStep
{
    public sealed record IamRead(PageAddress PageAddress, int ExtentCount, int SinglePageCount) : AccessStep(AccessPhase.Allocation);

    public sealed record IamLink(PageAddress FromPage, PageAddress ToPage) : AccessStep(AccessPhase.Allocation);

    public sealed record PfsRead(PageAddress PageAddress, int IntervalStartPage) : AccessStep(AccessPhase.Allocation);

    public sealed record PfsCheck(PageAddress PageAddress, bool IsAllocated) : AccessStep(AccessPhase.Allocation)
    {
        public string Status { get; init; } = string.Empty;
    }

    public sealed record Advance(string Description) : AccessStep(AccessPhase.Allocation);

    public sealed record ExtentStart(PageAddress FirstPage, int ExtentIndex) : AccessStep(AccessPhase.Allocation);

    public sealed record PageSkipped(PageAddress PageAddress, PageSkipReason Reason) : AccessStep(AccessPhase.Allocation);
}
