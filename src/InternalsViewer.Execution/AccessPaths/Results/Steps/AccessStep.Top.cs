using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.AccessPaths.Results.Steps;

public abstract partial record AccessStep
{
    /// <summary>
    /// A TOP announced how many rows it will ask its input for
    /// </summary>
    public sealed record TopStart(long RowCount) : AccessStep(AccessPhase.Ranges);

    /// <summary>
    /// A row reached the TOP and was counted against its limit
    /// </summary>
    public sealed record TopRow(long Number, long RowCount) : AccessStep(AccessPhase.Walk)
    {
        public IRecord? EmittedRecord { get; init; }

        /// <summary>
        /// This row met the limit, so the input is closed rather than read any further
        /// </summary>
        public bool IsLast => Number >= RowCount;
    }
}
