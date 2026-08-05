using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Results.Steps;

public abstract partial record AccessStep
{
    /// <summary>
    /// A merge loop compared the current key on each side and chose which side to advance
    /// </summary>
    public sealed record MergeCompare(int Comparison) : AccessStep(AccessPhase.Walk)
    {
        public AccessKey OuterKey { get; init; }

        public AccessKey InnerKey { get; init; }

        public string Action { get; init; } = string.Empty;

        public JoinDecision? Decision { get; init; }
    }

    /// <summary>
    /// A run of consecutive merge comparisons that advanced the same side, grouped for display
    /// </summary>
    public sealed record MergeCompareRun(int Comparison, int Count) : AccessStep(AccessPhase.Walk)
    {
        public AccessKey OuterFrom { get; init; }

        public AccessKey OuterTo { get; init; }

        public AccessKey InnerFrom { get; init; }

        public AccessKey InnerTo { get; init; }

        public string Action { get; init; } = string.Empty;

        public JoinDecision? Decision { get; init; }
    }
}
