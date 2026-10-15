namespace InternalsViewer.Query.FullTrace.Batches;

internal readonly record struct RawDeepValue(uint Slot, uint Data, uint Length, uint Captured);
