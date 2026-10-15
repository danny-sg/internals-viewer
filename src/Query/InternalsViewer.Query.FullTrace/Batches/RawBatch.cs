namespace InternalsViewer.Query.FullTrace.Batches;

internal readonly record struct RawBatch(ulong Function,
                                              ulong Instance,
                                              ulong Iterator,
                                              ulong Batch,
                                              ulong Consumer,
                                              ulong Id,
                                              uint Call,
                                              uint FirstSelected,
                                              uint SelectedCount,
                                              uint FirstVector,
                                              uint VectorCount,
                                              uint Flags,
                                              uint Capacity = 0);
