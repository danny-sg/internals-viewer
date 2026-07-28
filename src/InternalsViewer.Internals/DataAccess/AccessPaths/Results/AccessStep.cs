using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.DataAccess.AccessPaths.Results;

/// <summary>
/// A single observable action taken by an access path
/// </summary>
public abstract record AccessStep(StepLayer Layer, SeekPhase SeekPhase)
{
    /// <summary>
    /// Totals as they stood immediately after this step was taken
    /// </summary>
    public AccessCounters Counters { get; init; }

    /// <summary>
    /// A page was read and is now being examined
    /// </summary>
    public sealed record ReadPage(PageAddress PageAddress, byte Level, bool IsLeaf, int SlotCount)
        : AccessStep(StepLayer.Page, SeekPhase.Descent);

    public sealed record ProbeStart(int SlotCount) : AccessStep(StepLayer.Search, SeekPhase.Descent)
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
    public sealed record Probe(int Low, int High, int Middle, int Comparison) : AccessStep(StepLayer.Search, SeekPhase.Descent)
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
    public sealed record Descend(int Slot, PageAddress ChildPage) : AccessStep(StepLayer.Tree, SeekPhase.Descent);

    /// <summary>
    /// The slot the walk begins from once the leaf has been located
    /// </summary>
    public sealed record ProbeResult(int Slot, bool PastEnd) : AccessStep(StepLayer.Search, SeekPhase.Position)
    {
        public SeekRule? Rule { get; init; }

        public AccessKey Target { get; init; }

        public int Width { get; init; }
    }

    /// <summary>
    /// A row was examined
    /// </summary>
    public sealed record Row(int Slot, RowOutcome Outcome) : AccessStep(StepLayer.Row, SeekPhase.Walk);

    /// <summary>
    /// A key failed the trailing boundary test, ending the range
    /// </summary>
    public sealed record RangeEnd(int Slot) : AccessStep(StepLayer.Row, SeekPhase.Walk)
    {
        public AccessKey Key { get; init; }

        public AccessKey Boundary { get; init; }

        public int Width { get; init; }

        public int Comparison { get; init; }
    }

    /// <summary>
    /// A leaf level page link was followed
    /// </summary>
    public sealed record LeafLink(PageAddress FromPage, PageAddress ToPage) : AccessStep(StepLayer.Page, SeekPhase.Descent);

    /// <summary>
    /// The access path stopped producing rows
    /// </summary>
    public sealed record Stopped(StopReason Reason) : AccessStep(StepLayer.Access, SeekPhase.Complete);
}
