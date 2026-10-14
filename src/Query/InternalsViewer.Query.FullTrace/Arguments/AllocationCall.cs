namespace InternalsViewer.Query.FullTrace.Arguments;

internal readonly record struct AllocationCall(ulong Pointer, double Start, double End, double ConstructedBy, string? Allocator);
