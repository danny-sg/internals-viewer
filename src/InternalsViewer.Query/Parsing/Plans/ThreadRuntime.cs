namespace InternalsViewer.Query.Parsing.Plans;

public readonly record struct ThreadRuntime(long RowsProcessed, long ElapsedUs);