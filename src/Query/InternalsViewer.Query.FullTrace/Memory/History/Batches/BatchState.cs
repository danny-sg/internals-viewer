namespace InternalsViewer.Query.FullTrace.Memory.History.Batches;

public sealed record BatchState(ulong Address,
                                int Capacity,
                                int ActiveRowCount,
                                ulong VectorArrayAddress,
                                ulong AllocationBitmapAddress,
                                ulong PureBitmapAddress,
                                ulong SelectionAddress,
                                ushort[] Selection,
                                int RowCount,
                                List<BatchVector> Vectors)
{
    public int[] GetSelectionIndexes()
    {
        var indexes = new int[RowCount];

        Array.Fill(indexes, -1);

        for (var index = 0; index < Selection.Length; index++)
        {
            if (Selection[index] < indexes.Length)
            {
                indexes[Selection[index]] = index;
            }
        }

        return indexes;
    }
}
