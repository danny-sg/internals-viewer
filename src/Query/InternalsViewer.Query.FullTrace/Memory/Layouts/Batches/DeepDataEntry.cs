namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Batches;

public sealed record DeepDataEntry(ulong Address, int Length, byte[] Data)
{
    public const int HeaderSize = 2 * sizeof(ushort);

    public int Size => HeaderSize + Length;
}
