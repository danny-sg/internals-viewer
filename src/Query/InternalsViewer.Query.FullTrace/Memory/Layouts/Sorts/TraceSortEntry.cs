namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Sorts;

public sealed record TraceSortEntry(int Index, ulong Address, ulong Record, ulong Key, int RecordLength, int KeyLength, ulong Next)
{
    public int Run { get; init; } = -1;

    public int SortedIndex { get; init; } = -1;
}
