namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;

public readonly record struct TraceHashPage(ulong PointerPage, int PointerIndex, ulong BucketPage, int FirstBucket);
