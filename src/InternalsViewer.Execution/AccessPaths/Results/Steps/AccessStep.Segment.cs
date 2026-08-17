using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.AccessPaths.Results.Steps;

public abstract partial record AccessStep
{
    public sealed record SegmentRow(long Number, bool IsNewSegment) : AccessStep(AccessPhase.Segment)
    {
        public IRecord? EmittedRecord { get; init; }

        public long SegmentCount { get; init; }

        public string Key { get; init; } = string.Empty;

        public string Column { get; init; } = string.Empty;
    }
}
