namespace InternalsViewer.Query.Plans.Model;

public readonly record struct ThreadRuntime(long RowsProcessed, long ElapsedUs);