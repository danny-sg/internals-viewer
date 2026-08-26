namespace InternalsViewer.Query.Plans.Model;

public readonly record struct ThreadRuntime(long RowsProcessed, long ElapsedUs, ExecutionMode ExecutionMode, long BatchCount);