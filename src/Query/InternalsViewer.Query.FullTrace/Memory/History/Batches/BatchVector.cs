namespace InternalsViewer.Query.FullTrace.Memory.History.Batches;

public sealed record BatchVector(int Id, ulong Buffer, bool IsPure, ulong[] Slots, bool[] Known)
{
    public ulong CurrentDeepPage { get; init; }

    public ulong FirstDeepPage { get; init; }

    public bool HasDeepData => CurrentDeepPage != 0 || FirstDeepPage != 0;

    public bool IsDeepReference(int row) => HasDeepData && Known[row] && DeepDataReader.IsReference(Slots[row]);
}
