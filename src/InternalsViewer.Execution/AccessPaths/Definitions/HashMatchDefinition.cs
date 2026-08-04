namespace InternalsViewer.Execution.AccessPaths.Definitions;

/// <summary>
/// Describes a hash match, one input read into a hash table and a second read against it
/// </summary>
public sealed record HashMatchDefinition(JoinInputDefinition Build, JoinInputDefinition Probe) : JoinDefinition
{
    /// <summary>
    /// Overrides the bucket count, which is otherwise sized from the row estimate on the build side
    /// </summary>
    public int? BucketBits { get; init; }
}
