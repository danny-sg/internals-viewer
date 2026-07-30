using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Internals.DataAccess.AccessPaths.Results;

/// <summary>
/// A single observable action taken by an access path
/// </summary>
public abstract record AccessStep(AccessPhase AccessPhase)
{
    /// <summary>
    /// Totals as they stood immediately after this step was taken
    /// </summary>
    public AccessCounters Counters { get; init; }

    /// <summary>
    /// Identifies which access path produced the step when paths are composed, 0 for a single path
    /// </summary>
    public int Source { get; init; }

    /// <summary>
    /// A page was read and is now being examined
    /// </summary>
    public sealed record ReadPage(PageAddress PageAddress, byte Level, bool IsRoot, bool IsLeaf, int SlotCount)
        : AccessStep(AccessPhase.Descent)
    {
        public bool IsHeap { get; init; }
    }

    public sealed record ProbeStart(int SlotCount) : AccessStep(AccessPhase.Descent)
    {
        public SeekRule? Rule { get; init; }

        public AccessKey Target { get; init; }

        public int Width { get; init; }

        public ScanDirection Direction { get; init; }

        public bool IsLeaf { get; init; }
    }

    /// <summary>
    /// A binary search probe, showing the search window narrowing
    /// </summary>
    public sealed record Probe(int Low, int High, int Middle, int Comparison) : AccessStep(AccessPhase.Descent)
    {
        public AccessKey Key { get; init; }

        public AccessKey Target { get; init; }

        public int Width { get; init; }

        public bool SearchRight { get; init; }

        public int SlotCount { get; init; }
    }

    /// <summary>
    /// A non leaf slot was chosen and its child page will be read next
    /// </summary>
    public sealed record Descend(int Slot, PageAddress ChildPage) : AccessStep(AccessPhase.Descent);

    /// <summary>
    /// The slot the walk begins from once the leaf has been located
    /// </summary>
    public sealed record ProbeResult(int Slot, bool PastEnd) : AccessStep(AccessPhase.Position)
    {
        public SeekRule? Rule { get; init; }

        public AccessKey Target { get; init; }

        public int Width { get; init; }
    }

    /// <summary>
    /// A row was examined
    /// </summary>
    public sealed record Row(int Slot, RowOutcome Outcome) : AccessStep(AccessPhase.Walk)
    {
        public bool HasResidual { get; init; }

        public bool HasRange { get; init; } = true;

        public IRecord? EmittedRecord { get; init; }
    }

    public sealed record RowRun(int FromSlot, int ToSlot, RowOutcome Outcome) : AccessStep(AccessPhase.Walk)
    {
        public int Count { get; init; }

        public bool HasResidual { get; init; }

        public bool HasRange { get; init; } = true;

        public int EmitCount { get; init; }
    }

    /// <summary>
    /// A key failed the trailing boundary test, ending the range
    /// </summary>
    public sealed record RangeEnd(int Slot) : AccessStep(AccessPhase.Walk)
    {
        public AccessKey Key { get; init; }

        public AccessKey Boundary { get; init; }

        public int Width { get; init; }

        public int Comparison { get; init; }
    }

    /// <summary>
    /// A leaf level page link was followed
    /// </summary>
    public sealed record LeafLink(PageAddress FromPage, PageAddress ToPage) : AccessStep(AccessPhase.Descent)
    {
        public ScanDirection Direction { get; init; }
    }

    public sealed record Reseek(int RangeNumber, int RangeCount) : AccessStep(AccessPhase.Descent)
    {
        public SeekBounds Bounds { get; init; } = SeekBounds.All;
    }

    /// <summary>
    /// A correlated seek value was bound from an outer row and the inner path will descend for it
    /// </summary>
    public sealed record Rebind(int RebindNumber, AccessKey Key) : AccessStep(AccessPhase.Descent);

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

    /// <summary>
    /// The access path stopped producing rows
    /// </summary>
    public sealed record Stopped(StopReason Reason) : AccessStep(AccessPhase.Complete);

    public sealed record Truncated(long Count) : AccessStep(AccessPhase.Walk);
}
