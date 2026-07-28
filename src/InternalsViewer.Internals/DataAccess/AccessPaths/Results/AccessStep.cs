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
    public sealed record EnterPage(PageAddress PageAddress, byte Level, bool IsLeaf, int SlotCount)
        : AccessStep(StepLayer.Page, SeekPhase.Descent);

    /// <summary>
    /// A binary search probe, showing the search window narrowing
    /// </summary>
    public sealed record Probe(int Low, int High, int Middle, int Comparison) : AccessStep(StepLayer.Search, SeekPhase.Descent);

    /// <summary>
    /// A non leaf slot was chosen and its child page will be read next
    /// </summary>
    public sealed record Descend(int Slot, PageAddress ChildPage) : AccessStep(StepLayer.Tree, SeekPhase.Descent);

    /// <summary>
    /// The slot the walk begins from once the leaf has been located
    /// </summary>
    public sealed record EntryPoint(int Slot, bool PastEnd) : AccessStep(StepLayer.Search, SeekPhase.Position);

    /// <summary>
    /// A row was examined
    /// </summary>
    public sealed record Row(int Slot, RowOutcome Outcome) : AccessStep(StepLayer.Row, SeekPhase.Walk);

    /// <summary>
    /// A key failed the trailing boundary test, ending the range
    /// </summary>
    public sealed record RangeEnd(int Slot) : AccessStep(StepLayer.Row, SeekPhase.Walk);

    /// <summary>
    /// A leaf level page link was followed
    /// </summary>
    public sealed record LeafLink(PageAddress FromPage, PageAddress ToPage) : AccessStep(StepLayer.Page, SeekPhase.Descent);

    /// <summary>
    /// The access path stopped producing rows
    /// </summary>
    public sealed record Stopped(StopReason Reason) : AccessStep(StepLayer.Access, SeekPhase.Complete);
}
