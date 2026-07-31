namespace InternalsViewer.Execution.AccessPaths.Binding;

/// <summary>
/// Maps an inner seek key column to the outer row column that supplies its value on each rebind
/// </summary>
public sealed record CorrelationBinding(string SeekColumn, string OuterColumn);
