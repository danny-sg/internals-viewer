namespace InternalsViewer.Query.FullTrace.Batches;

internal readonly record struct RawVector(uint FirstSlot,
                                               uint SlotCount,
                                               uint FirstDeepValue,
                                               uint DeepValueCount,
                                               ushort VectorId,
                                               ushort Flags);
