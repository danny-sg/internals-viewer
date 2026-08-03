namespace InternalsViewer.Execution.AccessPaths.Definitions;

/// <summary>
/// Describes a TOP, which stops asking its input for rows once it has the number it was given
/// </summary>
/// <remarks>
/// A row goal pushed into an access path is a different thing, and the two coexist. The goal lets a seek stop early on its own, while this
/// is the operator that actually counts and closes the input, so it is what a trace shows stopping the walk.
/// </remarks>
public sealed record TopDefinition(IteratorDefinition Source) : UnaryDefinition(Source)
{
    public long RowCount { get; init; }

    /// <summary>
    /// The count is a percentage of the input rather than a number of rows, which is not simulated
    /// </summary>
    public bool IsPercent { get; init; }
}
