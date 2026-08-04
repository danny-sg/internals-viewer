namespace InternalsViewer.Execution.AccessPaths.Definitions;

/// <summary>
/// Describes a nested loops join, an outer input driving an inner input that restarts for every outer row
/// </summary>
/// <remarks>
/// The inner side is a <see cref="SeekDefinition"/> when the outer row supplies key values, which covers both a loop join and a key
/// lookup, or a <see cref="HeapFetchDefinition"/> when it supplies a row identifier, which is a RID lookup.
/// </remarks>
public sealed record NestedLoopsDefinition(IteratorDefinition Outer, IteratorDefinition Inner) : JoinDefinition;
