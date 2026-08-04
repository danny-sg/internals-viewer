using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Execution.AccessPaths.Results;

public abstract partial record AccessStep
{
    /// <summary>
    /// A page was read and is now being examined
    /// </summary>
    public sealed record ReadPage(PageAddress PageAddress, byte Level, bool IsRoot, bool IsLeaf, int SlotCount)
        : AccessStep(AccessPhase.Descent)
    {
        public bool IsHeap { get; init; }
    }

    /// <summary>
    /// A non leaf slot was chosen and its child page will be read next
    /// </summary>
    public sealed record Descend(int Slot, PageAddress ChildPage) : AccessStep(AccessPhase.Descent);

    /// <summary>
    /// A leaf level page link was followed
    /// </summary>
    public sealed record LeafLink(PageAddress FromPage, PageAddress ToPage) : AccessStep(AccessPhase.Descent)
    {
        public ScanDirection Direction { get; init; }
    }

    /// <summary>
    /// The row identifier led to a stub left behind when the row outgrew its page, so the real row is on another page
    /// </summary>
    public sealed record ForwardedRecord(RowIdentifier From, RowIdentifier To) : AccessStep(AccessPhase.Descent);
}
