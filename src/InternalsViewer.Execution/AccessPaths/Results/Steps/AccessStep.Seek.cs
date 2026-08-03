using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Results;

public abstract partial record AccessStep
{
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
    /// A run of consecutive binary search probes, grouped for display
    /// </summary>
    public sealed record ProbeRun(IReadOnlyList<Probe> Probes) : AccessStep(AccessPhase.Descent);

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
    /// A key failed the trailing boundary test, ending the range
    /// </summary>
    public sealed record RangeEnd(int Slot) : AccessStep(AccessPhase.Walk)
    {
        public AccessKey Key { get; init; }

        public AccessKey Boundary { get; init; }

        public int Width { get; init; }

        public int Comparison { get; init; }
    }

    public sealed record Reseek(int RangeNumber, int RangeCount) : AccessStep(AccessPhase.Descent)
    {
        public SeekBounds Bounds { get; init; } = SeekBounds.All;
    }
}
