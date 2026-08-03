namespace InternalsViewer.Execution.AccessPaths.Definitions;

/// <summary>
/// Describes a merge join, two inputs read once each in the same key order
/// </summary>
public sealed record MergeJoinDefinition(JoinInputDefinition Outer, JoinInputDefinition Inner) : JoinDefinition;
