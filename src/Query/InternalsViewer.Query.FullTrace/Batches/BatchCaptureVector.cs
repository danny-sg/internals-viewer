namespace InternalsViewer.Query.FullTrace.Batches;

public sealed record BatchCaptureVector(int VectorId,
                                        BatchVectorFlags Flags,
                                        ulong[] Slots,
                                        IReadOnlyDictionary<int, BatchDeepValue> DeepValues)
{
    public bool IsPure => Flags.HasFlag(BatchVectorFlags.Pure);

    public bool IsReadable => (Flags & (BatchVectorFlags.Unreadable | BatchVectorFlags.Missing)) == 0;

    public ulong? GetSlot(int row)
    {
        var index = GetSlotIndex(row);

        return index >= 0 && index < Slots.Length ? Slots[index] : null;
    }

    public BatchDeepValue? GetDeepValue(int row) => DeepValues.GetValueOrDefault(GetSlotIndex(row));

    private int GetSlotIndex(int row) => IsPure ? 0 : row;
}
